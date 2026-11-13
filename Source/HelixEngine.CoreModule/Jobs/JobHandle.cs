using System;
using System.Threading;
using Helix.Collections;
using Helix.Collections.LowLevel.Unsafe;
using HelixEngine.Internal;

namespace Helix.Jobs
{
    /// <summary>A scheduled job (or a group of them): jobs scheduled with it as a dependency start after it.</summary>
    public struct JobHandle : IEquatable<JobHandle>
    {
        internal ulong m_Handle;

        internal JobHandle(ulong handle) { m_Handle = handle; }

        /// <summary>Waits until the job is done (the calling thread helps run jobs meanwhile).</summary>
        public void Complete()
        {
            if (m_Handle == 0)
                return;
            JobsNative.Complete(m_Handle);
            if (CollectionChecks.Enabled)
                JobDependencies.Sync(m_Handle);
            JobScheduler.ProcessPending();
        }

        public bool IsCompleted => JobsNative.IsCompleted(m_Handle);

        public static void CompleteAll(ref JobHandle job0, ref JobHandle job1)
        {
            job0.Complete();
            job1.Complete();
            job0 = default;
            job1 = default;
        }

        public static void CompleteAll(ref JobHandle job0, ref JobHandle job1, ref JobHandle job2)
        {
            job0.Complete();
            job1.Complete();
            job2.Complete();
            job0 = default;
            job1 = default;
            job2 = default;
        }

        public static void CompleteAll(NativeArray<JobHandle> jobs)
        {
            for (int i = 0; i < jobs.Length; i++)
                jobs[i].Complete();
        }

        /// <summary>The jobs scheduled so far start (they start at once here).</summary>
        public static void ScheduleBatchedJobs() { }

        public static JobHandle CombineDependencies(JobHandle job0, JobHandle job1)
        {
            ulong h = JobsNative.Combine(job0.m_Handle, job1.m_Handle);
            JobDependencies.Add(h, job0.m_Handle, job1.m_Handle);
            return new JobHandle(h);
        }

        public static JobHandle CombineDependencies(JobHandle job0, JobHandle job1, JobHandle job2) =>
            CombineDependencies(CombineDependencies(job0, job1), job2);

        public static unsafe JobHandle CombineDependencies(NativeArray<JobHandle> jobs)
        {
            var handles = (ulong*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(jobs);
            ulong h = JobsNative.CombineMany((ulong)handles, jobs.Length);
            var parents = new ulong[jobs.Length];
            for (int i = 0; i < parents.Length; i++)
                parents[i] = handles[i];
            JobDependencies.Add(h, parents);
            return new JobHandle(h);
        }

        public static JobHandle CombineDependencies(NativeSlice<JobHandle> jobs)
        {
            using var copy = new NativeArray<JobHandle>(jobs.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            jobs.CopyTo(copy);
            return CombineDependencies(copy);
        }

        /// <summary>Whether `dependsOn` is done or `jobHandle` waits for it.</summary>
        public static bool CheckFenceIsDependencyOrDidSyncFence(JobHandle jobHandle, JobHandle dependsOn) =>
            JobDependencies.IsDependency(dependsOn.m_Handle, jobHandle.m_Handle);

        public bool Equals(JobHandle other) => m_Handle == other.m_Handle;
        public override bool Equals(object obj) => obj is JobHandle other && Equals(other);
        public override int GetHashCode() => m_Handle.GetHashCode();
    }

    /// <summary>A job run once.</summary>
    public interface IJob
    {
        void Execute();
    }

    /// <summary>A job run for each index, one worker after another (Schedule) or in parallel batches (ScheduleParallel).</summary>
    public interface IJobFor
    {
        void Execute(int index);
    }

    /// <summary>A job run for each index in parallel batches.</summary>
    public interface IJobParallelFor
    {
        void Execute(int index);
    }

    public static class IJobExtensions
    {
        public static void EarlyJobInit<T>() where T : struct, IJob => JobInfo<T>.Init();

        public static JobHandle Schedule<T>(this T jobData, JobHandle dependsOn = default) where T : struct, IJob =>
            JobScheduler.Schedule(ref jobData, JobProducer<T>.Execute, JobScheduler.KindSingle, 1, 1, false, dependsOn);

        public static JobHandle ScheduleByRef<T>(ref this T jobData, JobHandle dependsOn = default) where T : struct, IJob =>
            JobScheduler.Schedule(ref jobData, JobProducer<T>.Execute, JobScheduler.KindSingle, 1, 1, false, dependsOn);

        public static void Run<T>(this T jobData) where T : struct, IJob => RunByRef(ref jobData);

        public static void RunByRef<T>(ref this T jobData) where T : struct, IJob
        {
            JobInfo<T>.Init();
            jobData.Execute();
        }

        unsafe struct JobProducer<T> where T : struct, IJob
        {
            internal static readonly IntPtr Execute = (IntPtr)(delegate*<byte*, uint, uint, void>)&Run;

            static void Run(byte* block, uint begin, uint end)
            {
                ref T job = ref JobScheduler.JobOf<T>(block);
                job.Execute();
            }
        }
    }

    public static class IJobForExtensions
    {
        public static void EarlyJobInit<T>() where T : struct, IJobFor => JobInfo<T>.Init();

        /// <summary>All indices on one worker, in order.</summary>
        public static JobHandle Schedule<T>(this T jobData, int arrayLength, JobHandle dependency) where T : struct, IJobFor =>
            JobScheduler.Schedule(ref jobData, JobProducer<T>.Sequential, JobScheduler.KindSequential, arrayLength, 1, false, dependency);

        public static JobHandle ScheduleByRef<T>(ref this T jobData, int arrayLength, JobHandle dependency) where T : struct, IJobFor =>
            JobScheduler.Schedule(ref jobData, JobProducer<T>.Sequential, JobScheduler.KindSequential, arrayLength, 1, false, dependency);

        /// <summary>Batches of innerloopBatchCount indices on the workers.</summary>
        public static JobHandle ScheduleParallel<T>(this T jobData, int arrayLength, int innerloopBatchCount, JobHandle dependency) where T : struct, IJobFor =>
            JobScheduler.Schedule(ref jobData, JobProducer<T>.Parallel, JobScheduler.KindParallel, arrayLength, innerloopBatchCount, true, dependency);

        public static JobHandle ScheduleParallelByRef<T>(ref this T jobData, int arrayLength, int innerloopBatchCount, JobHandle dependency) where T : struct, IJobFor =>
            JobScheduler.Schedule(ref jobData, JobProducer<T>.Parallel, JobScheduler.KindParallel, arrayLength, innerloopBatchCount, true, dependency);

        public static void Run<T>(this T jobData, int arrayLength) where T : struct, IJobFor => RunByRef(ref jobData, arrayLength);

        public static void RunByRef<T>(ref this T jobData, int arrayLength) where T : struct, IJobFor
        {
            JobInfo<T>.Init();
            for (int i = 0; i < arrayLength; i++)
                jobData.Execute(i);
        }

        unsafe struct JobProducer<T> where T : struct, IJobFor
        {
            internal static readonly IntPtr Sequential = (IntPtr)(delegate*<byte*, uint, uint, void>)&RunAll;
            internal static readonly IntPtr Parallel = (IntPtr)(delegate*<byte*, uint, uint, void>)&RunRange;

            static void RunAll(byte* block, uint begin, uint end)
            {
                ref T job = ref JobScheduler.JobOf<T>(block);
                int count = JobScheduler.CountOf(block);
                for (int i = 0; i < count; i++)
                    job.Execute(i);
            }

            static void RunRange(byte* block, uint begin, uint end)
            {
                T job = JobScheduler.BatchCopy<T>(block, begin, end);
                for (int i = (int)begin; i < (int)end; i++)
                    job.Execute(i);
            }
        }
    }

    public static class IJobParallelForExtensions
    {
        public static void EarlyJobInit<T>() where T : struct, IJobParallelFor => JobInfo<T>.Init();

        public static JobHandle Schedule<T>(this T jobData, int arrayLength, int innerloopBatchCount, JobHandle dependsOn = default) where T : struct, IJobParallelFor =>
            JobScheduler.Schedule(ref jobData, JobProducer<T>.Parallel, JobScheduler.KindParallel, arrayLength, innerloopBatchCount, true, dependsOn);

        public static JobHandle ScheduleByRef<T>(ref this T jobData, int arrayLength, int innerloopBatchCount, JobHandle dependsOn = default)
            where T : struct, IJobParallelFor =>
            JobScheduler.Schedule(ref jobData, JobProducer<T>.Parallel, JobScheduler.KindParallel, arrayLength, innerloopBatchCount, true, dependsOn);

        public static void Run<T>(this T jobData, int arrayLength) where T : struct, IJobParallelFor => RunByRef(ref jobData, arrayLength);

        public static void RunByRef<T>(ref this T jobData, int arrayLength) where T : struct, IJobParallelFor
        {
            JobInfo<T>.Init();
            for (int i = 0; i < arrayLength; i++)
                jobData.Execute(i);
        }

        unsafe struct JobProducer<T> where T : struct, IJobParallelFor
        {
            internal static readonly IntPtr Parallel = (IntPtr)(delegate*<byte*, uint, uint, void>)&RunRange;

            static void RunRange(byte* block, uint begin, uint end)
            {
                T job = JobScheduler.BatchCopy<T>(block, begin, end);
                for (int i = (int)begin; i < (int)end; i++)
                    job.Execute(i);
            }
        }
    }
}

namespace Helix.Jobs.LowLevel.Unsafe
{
    /// <summary>The job system's workers.</summary>
    public static class JobsUtility
    {
        public const int MaxJobThreadCount = 128;

        /// <summary>The workers jobs run on (the main thread helps when it waits).</summary>
        public static int JobWorkerCount
        {
            get => JobsNative.WorkerCount();
            set { }
        }

        public static int JobWorkerMaximumCount => JobsNative.MaximumWorkerCount();

        /// <summary>Whether this thread runs a job now.</summary>
        public static bool IsExecutingJob => JobScheduler.t_Executing > 0;

        /// <summary>0 on the main thread, 1 to ThreadIndexCount - 1 on the workers.</summary>
        public static int ThreadIndex => JobScheduler.ThreadIndex;

        public static int ThreadIndexCount => Math.Min(JobsNative.MaximumWorkerCount() + 1, MaxJobThreadCount);
    }
}
