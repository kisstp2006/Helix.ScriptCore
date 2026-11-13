using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Helix.Collections;
using Helix.Collections.LowLevel.Unsafe;
using HelixEngine.Internal;

namespace Helix.Jobs
{
    // Jobs to the native scheduler: the job struct after a header in a block the native side copies, and one entry
    // point (an UnmanagedCallersOnly function) that calls the job type's producer, a generic instantiation per job
    // type: header [producer, count, kind, transform items], the job at offset 32.
    static unsafe class JobScheduler
    {
        internal const int HeaderSize = 32, ItemsOffset = 16;
        internal const int KindSingle = 0, KindSequential = 1, KindParallel = 2, KindTransform = 3;

        [ThreadStatic] internal static int t_Executing;
        [ThreadStatic] static int t_ThreadIndex;
        static int s_NextThreadIndex;
        // The thread that first schedules a job (the main thread: the C# side schedules from it).
        static int s_MainThread = -1;
        // The main thread is 0; it sets its index when the C# side starts (the first schedule).
        [ThreadStatic] static bool t_HasIndex;

        internal static int ThreadIndex
        {
            get
            {
                if (!t_HasIndex)
                {
                    t_HasIndex = true;
                    t_ThreadIndex = Environment.CurrentManagedThreadId == s_MainThread ? 0 : Interlocked.Increment(ref s_NextThreadIndex);
                }
                return t_ThreadIndex;
            }
        }

        static readonly ulong s_Entry = (ulong)(delegate* unmanaged[Cdecl]<byte*, uint, uint, void>)&Entry;

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void Entry(byte* block, uint begin, uint end)
        {
            t_Executing++;
            try
            {
                var producer = (delegate*<byte*, uint, uint, void>)*(IntPtr*)block;
                producer(block, begin, end);
            }
            catch (Exception e)
            {
                try
                {
                    HelixEngine.Debug.LogException(e);
                }
                catch
                {
                }
            }
            finally
            {
                t_Executing--;
            }
        }

        internal static ref T JobOf<T>(byte* block) where T : struct
        {
            ResolveDeferred(block + HeaderSize, JobInfo<T>.Containers);
            return ref Unsafe.AsRef<T>(block + HeaderSize);
        }

        // NativeList.AsDeferredJobArray: an array whose buffer is the list's header (odd address) until the job runs.
        internal static void ResolveDeferred(byte* job, JobContainer[] containers)
        {
            foreach (JobContainer c in containers)
                if (c.isNativeArray && ((ulong)*(void**)(job + c.offset) & 1) != 0)
                {
                    byte* list = (byte*)*(void**)(job + c.offset) - 1;
                    int length = *(int*)(list + sizeof(void*));
                    *(void**)(job + c.offset) = *(void**)list;
                    *(int*)(job + c.offset + sizeof(void*)) = length;
                    if (c.maxOffset >= 0)
                        *(int*)(job + c.maxOffset) = length - 1;
                }
        }
        internal static int CountOf(byte* block) => *(int*)(block + 8);

        /// <summary>The worker's own copy of a parallel job, its containers restricted to the batch.</summary>
        internal static T BatchCopy<T>(byte* block, uint begin, uint end) where T : struct
        {
            T job = Unsafe.Read<T>(block + HeaderSize);
            ResolveDeferred((byte*)Unsafe.AsPointer(ref job), JobInfo<T>.Containers);
            if (CollectionChecks.Enabled)
            {
                byte* p = (byte*)Unsafe.AsPointer(ref job);
                foreach (JobContainer c in JobInfo<T>.Containers)
                    if (c.minOffset >= 0 && !c.readOnly && !c.noRestriction)
                    {
                        *(int*)(p + c.minOffset) = (int)begin;
                        *(int*)(p + c.maxOffset) = (int)end - 1;
                    }
                foreach (int offset in JobInfo<T>.ThreadIndexOffsets)
                    *(int*)(p + offset) = ThreadIndex;
            }
            else if (JobInfo<T>.ThreadIndexOffsets.Length > 0)
            {
                byte* p = (byte*)Unsafe.AsPointer(ref job);
                foreach (int offset in JobInfo<T>.ThreadIndexOffsets)
                    *(int*)(p + offset) = ThreadIndex;
            }
            return job;
        }

        internal static JobHandle Schedule<T>(ref T job, IntPtr producer, int kind, int count, int batch, bool parallel, JobHandle dependsOn, ulong transforms = 0)
            where T : struct
        {
            JobInfo<T>.Init();
            if (s_MainThread < 0)
                s_MainThread = Environment.CurrentManagedThreadId;
            if (count < 0)
                throw new ArgumentOutOfRangeException("arrayLength", "arrayLength must be >= 0");
            int size = HeaderSize + Unsafe.SizeOf<T>();
            byte* block = stackalloc byte[0];
            void* heap = null;
            if (size <= 4096)
            {
                byte* local = stackalloc byte[size];
                block = local;
            }
            else
                block = (byte*)(heap = NativeMemory.AlignedAlloc((nuint)size, 16));
            try
            {
                *(IntPtr*)block = producer;
                *(int*)(block + 8) = count;
                *(int*)(block + 12) = kind;
                *(ulong*)(block + 16) = 0;
                Unsafe.Write(block + HeaderSize, job);
                bool checks = CollectionChecks.Enabled;
                if (checks)
                    JobInfo<T>.PrepareSafety(block + HeaderSize, dependsOn);
                ulong h = kind == KindTransform
                    ? JobsNative.ScheduleTransforms(s_Entry, (ulong)block, size, ItemsOffset, transforms, dependsOn.m_Handle)
                    : JobsNative.Schedule(s_Entry, (ulong)block, size, count, batch, parallel, dependsOn.m_Handle);
                if (checks)
                {
                    JobDependencies.Add(h, dependsOn.m_Handle);
                    JobInfo<T>.CommitSafety(block + HeaderSize, h);
                    ProcessPending();
                }
                return new JobHandle(h);
            }
            finally
            {
                if (heap != null)
                    NativeMemory.AlignedFree(heap);
            }
        }

        // Containers marked [DeallocateOnJobCompletion]: freed (and their handles released) once their job is done.
        struct Pending
        {
            public ulong job;
            public AtomicSafetyHandle safety;
        }

        static readonly List<Pending> s_Pending = new();

        internal static void Deallocate(ulong job, void* buffer, Allocator allocator, AtomicSafetyHandle safety)
        {
            LeakTracker.Untrack(buffer);
            if (buffer != null && allocator > Allocator.None)
                new FreeJob { buffer = buffer, allocator = allocator }.Schedule(new JobHandle(job));
            lock (s_Pending)
                s_Pending.Add(new Pending { job = job, safety = safety });
        }

        internal static void ProcessPending()
        {
            lock (s_Pending)
                for (int i = s_Pending.Count - 1; i >= 0; i--)
                    if (JobsNative.IsCompleted(s_Pending[i].job))
                    {
                        AtomicSafetyHandle.Release(s_Pending[i].safety);
                        s_Pending.RemoveAt(i);
                    }
        }

        struct FreeJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] public void* buffer;
            public Allocator allocator;
            public void Execute() => UnsafeUtility.Free(buffer, allocator);
        }
    }

    // A native container field of a job struct (offsets from the job's start).
    struct JobContainer
    {
        public int offset;
        public int safetyOffset;
        public int minOffset, maxOffset;
        public bool readOnly, writeOnly, noRestriction, noSafety, deallocate;
        public bool isNativeArray;
        public int name;
    }

    // What the scheduler knows of a job type: no references (checked once), its containers and thread index fields
    // (development builds), found by reflection once; their offsets by writing markers into a boxed job.
    static unsafe class JobInfo<T> where T : struct
    {
        internal static JobContainer[] Containers = Array.Empty<JobContainer>();
        internal static int[] ThreadIndexOffsets = Array.Empty<int>();
        static bool s_Ready;
        static readonly object s_Lock = new();

        internal static void Init()
        {
            if (s_Ready)
                return;
            lock (s_Lock)
            {
                if (s_Ready)
                    return;
                if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                    throw new InvalidOperationException($"{ReferenceField(typeof(T)) ?? typeof(T).ToString()} is not a value type. Job structs may not contain any reference types.");
                var containers = new List<JobContainer>();
                var threadIndices = new List<int>();
                Walk(typeof(T), new List<FieldInfo>(), containers, threadIndices);
                Containers = containers.ToArray();
                ThreadIndexOffsets = threadIndices.ToArray();
                s_Ready = true;
            }
        }

        static string ReferenceField(Type t)
        {
            foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!f.FieldType.IsValueType)
                    return t + "." + f.Name;
                if (!f.FieldType.IsPrimitive && !f.FieldType.IsEnum && !f.FieldType.IsPointer)
                    if (ReferenceField(f.FieldType) is string inner)
                        return inner;
            }
            return null;
        }

        static void Walk(Type t, List<FieldInfo> path, List<JobContainer> containers, List<int> threadIndices)
        {
            foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                Type ft = f.FieldType;
                if (ft.IsPrimitive || ft.IsEnum || ft.IsPointer || !ft.IsValueType)
                {
                    if (ft == typeof(int) && f.IsDefined(typeof(NativeSetThreadIndexAttribute)))
                        threadIndices.Add(Offset(Append(path, f), null, 1));
                    continue;
                }
                List<FieldInfo> here = Append(path, f);
                if (!ft.IsDefined(typeof(NativeContainerAttribute)))
                {
                    Walk(ft, here, containers, threadIndices);
                    continue;
                }
                FieldInfo safety = ft.GetField("m_Safety", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (safety == null)
                    continue;
                var c = new JobContainer
                {
                    offset = -1,
                    safetyOffset = Offset(Append(here, safety), safety.FieldType.GetField("versionNode", BindingFlags.Instance | BindingFlags.NonPublic), 3),
                    minOffset = -1,
                    maxOffset = -1,
                    readOnly = f.IsDefined(typeof(ReadOnlyAttribute)) || ft.IsDefined(typeof(NativeContainerIsReadOnlyAttribute)),
                    writeOnly = f.IsDefined(typeof(WriteOnlyAttribute)),
                    noRestriction = f.IsDefined(typeof(NativeDisableParallelForRestrictionAttribute)) || ft.IsDefined(typeof(NativeContainerIsAtomicWriteOnlyAttribute)),
                    noSafety = f.IsDefined(typeof(NativeDisableContainerSafetyRestrictionAttribute)),
                    deallocate = f.IsDefined(typeof(DeallocateOnJobCompletionAttribute)),
                    isNativeArray = ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(NativeArray<>),
                    name = SafetyNodes.NameId(typeof(T).Name, string.Join(".", here.ConvertAll(x => x.Name))),
                };
                // A NativeArray's buffer comes before its length.
                if (c.isNativeArray)
                    c.offset = Offset(here, ft.GetField("m_Length", BindingFlags.Instance | BindingFlags.NonPublic), 2) - IntPtr.Size;
                if (ft.IsDefined(typeof(NativeContainerSupportsMinMaxWriteRestrictionAttribute)))
                {
                    FieldInfo min = ft.GetField("m_MinIndex", BindingFlags.Instance | BindingFlags.NonPublic);
                    FieldInfo max = ft.GetField("m_MaxIndex", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (min != null && max != null)
                    {
                        c.minOffset = Offset(here, min, 4);
                        c.maxOffset = Offset(here, max, 5);
                    }
                }
                containers.Add(c);
            }
        }

        static List<FieldInfo> Append(List<FieldInfo> path, FieldInfo f)
        {
            var list = new List<FieldInfo>(path) { f };
            return list;
        }

        // The byte offset of a field in T: a marker written into a boxed T through the field path, then found.
        static int Offset(List<FieldInfo> path, FieldInfo leaf, int salt)
        {
            object box = default(T);
            var fields = new List<FieldInfo>(path);
            if (leaf == null)
            {
                leaf = fields[fields.Count - 1];
                fields.RemoveAt(fields.Count - 1);
            }
            int marker = 0x5AFE0000 | salt << 8 | 0x5A;
            object value = leaf.FieldType == typeof(IntPtr) ? (object)(IntPtr)marker : marker;
            if (fields.Count == 0)
                leaf.SetValue(box, value);
            else
                leaf.SetValueDirect(TypedReference.MakeTypedReference(box, fields.ToArray()), value);
            ref byte start = ref Unsafe.As<T, byte>(ref Unsafe.Unbox<T>(box));
            int size = Unsafe.SizeOf<T>();
            for (int i = 0; i + 4 <= size; i += 4)
                if (Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref start, i)) == marker)
                    return i;
            throw new InvalidOperationException($"The field {leaf.Name} of {typeof(T)} could not be located.");
        }

        /// <summary>Checks each container against the jobs scheduled with it and marks the job's copies.</summary>
        internal static void PrepareSafety(byte* job, JobHandle dependsOn)
        {
            foreach (JobContainer c in Containers)
            {
                if (c.noSafety)
                    continue;
                ref AtomicSafetyHandle h = ref Unsafe.AsRef<AtomicSafetyHandle>(job + c.safetyOffset);
                if (h.versionNode == IntPtr.Zero)
                    continue;
                SafetyNodes.CheckSchedule(h, !c.readOnly, c.name, dependsOn);
                h.flags |= AtomicSafetyHandle.InJob | (c.readOnly ? AtomicSafetyHandle.JobReadOnly : 0) | (c.writeOnly ? AtomicSafetyHandle.JobWriteOnly : 0);
            }
        }

        /// <summary>The containers' nodes learn the job that uses them.</summary>
        internal static void CommitSafety(byte* job, ulong handle)
        {
            foreach (JobContainer c in Containers)
            {
                if (c.noSafety)
                    continue;
                AtomicSafetyHandle h = Unsafe.AsRef<AtomicSafetyHandle>(job + c.safetyOffset);
                if (h.versionNode == IntPtr.Zero)
                    continue;
                SafetyNodes.Scheduled(h, !c.readOnly, c.name, handle);
                if (c.deallocate && c.isNativeArray)
                {
                    void* buffer = *(void**)(job + c.offset);
                    var allocator = (Allocator)(*(int*)(job + c.offset + sizeof(void*) + sizeof(int)));
                    JobScheduler.Deallocate(handle, buffer, allocator, h);
                }
            }
        }
    }
}
