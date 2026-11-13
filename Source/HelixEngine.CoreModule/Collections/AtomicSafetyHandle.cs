using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Helix.Jobs;
using HelixEngine.Internal;

namespace Helix.Collections.LowLevel.Unsafe
{
    /// <summary>
    /// The safety of a native container in development builds: a version (use after Dispose), the scheduled job that
    /// writes it and the ones that read it (main thread access while they run, jobs scheduled against them without a
    /// dependency), and inside a job the access the job declared ([ReadOnly], [WriteOnly]). Release builds check nothing.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct AtomicSafetyHandle
    {
        internal IntPtr versionNode;
        internal int version;
        internal int flags;

        // flags: the copy of a job, and what the job declared.
        internal const int InJob = 1, JobReadOnly = 2, JobWriteOnly = 4, TempMemory = 8, Secondary = 16, NoChecks = 32;

        public static AtomicSafetyHandle Create() => CollectionChecks.Enabled ? SafetyNodes.Create() : default;
        public static void Release(AtomicSafetyHandle handle) => SafetyNodes.Release(handle);

        public static AtomicSafetyHandle GetTempMemoryHandle()
        {
            AtomicSafetyHandle h = Create();
            h.flags |= TempMemory;
            return h;
        }

        public static AtomicSafetyHandle GetTempUnsafePtrSliceHandle() => GetTempMemoryHandle();
        public static bool IsTempMemoryHandle(AtomicSafetyHandle handle) => (handle.flags & TempMemory) != 0;
        public static bool IsDefaultValue(in AtomicSafetyHandle handle) => handle.versionNode == IntPtr.Zero;
        public static bool IsValidNonDefaultHandle(in AtomicSafetyHandle handle) => handle.versionNode != IntPtr.Zero && SafetyNodes.Alive(handle);
        public static bool IsHandleValid(in AtomicSafetyHandle handle) => handle.versionNode == IntPtr.Zero || SafetyNodes.Alive(handle);

        public static void ValidateNonDefaultHandle(in AtomicSafetyHandle handle)
        {
            if (handle.versionNode == IntPtr.Zero)
                throw new InvalidOperationException("The safety handle is the default one: the container was not created.");
        }

        public static void CheckExistsAndThrow(in AtomicSafetyHandle handle)
        {
            if (handle.versionNode != IntPtr.Zero && !SafetyNodes.Alive(handle))
                throw new ObjectDisposedException(null, SafetyNodes.TypeName(handle) + " has been deallocated, it is not allowed to access it");
        }

        public static void CheckReadAndThrow(in AtomicSafetyHandle handle)
        {
            if (handle.versionNode != IntPtr.Zero)
                SafetyNodes.CheckRead(handle);
        }

        public static void CheckWriteAndThrow(in AtomicSafetyHandle handle)
        {
            if (handle.versionNode != IntPtr.Zero)
                SafetyNodes.CheckWrite(handle);
        }

        public static void CheckDeallocateAndThrow(in AtomicSafetyHandle handle)
        {
            if (handle.versionNode != IntPtr.Zero)
                SafetyNodes.CheckDeallocate(handle);
        }

        public static void CheckGetSecondaryDataPointerAndThrow(in AtomicSafetyHandle handle) => CheckExistsAndThrow(handle);

        public static void CheckWriteAndBumpSecondaryVersion(in AtomicSafetyHandle handle)
        {
            CheckWriteAndThrow(handle);
            if (handle.versionNode != IntPtr.Zero)
                SafetyNodes.BumpSecondary(handle);
        }

        public static void UseSecondaryVersion(ref AtomicSafetyHandle handle)
        {
            if (handle.versionNode == IntPtr.Zero)
                return;
            handle.flags |= Secondary;
            handle.version = SafetyNodes.SecondaryVersion(handle);
        }

        public static void SetAllowSecondaryVersionWriting(AtomicSafetyHandle handle, bool allowWriting) => SafetyNodes.SetFlag(handle, SafetyNodes.DenySecondaryWrite, !allowWriting);
        public static void SetBumpSecondaryVersionOnScheduleWrite(AtomicSafetyHandle handle, bool value) => SafetyNodes.SetFlag(handle, SafetyNodes.BumpOnSchedule, value);

        /// <summary>Whether the main thread may read and write (false while a job owns the container).</summary>
        public static void SetAllowReadOrWriteAccess(AtomicSafetyHandle handle, bool allowReadWriteAccess) => SafetyNodes.SetFlag(handle, SafetyNodes.DenyAccess, !allowReadWriteAccess);
        public static bool GetAllowReadOrWriteAccess(AtomicSafetyHandle handle) => !SafetyNodes.HasFlag(handle, SafetyNodes.DenyAccess);

        public static JobHandle EnforceAllBufferJobsHaveCompleted(AtomicSafetyHandle handle) => SafetyNodes.CompleteAll(handle, false);
        public static JobHandle EnforceAllBufferJobsHaveCompletedAndRelease(AtomicSafetyHandle handle)
        {
            JobHandle h = SafetyNodes.CompleteAll(handle, false);
            Release(handle);
            return h;
        }

        public static JobHandle EnforceAllBufferJobsHaveCompletedAndDisableReadWrite(AtomicSafetyHandle handle)
        {
            JobHandle h = SafetyNodes.CompleteAll(handle, false);
            SetAllowReadOrWriteAccess(handle, false);
            return h;
        }

        public static JobHandle GetWriter(AtomicSafetyHandle handle) => SafetyNodes.Writer(handle);
        public static string GetWriterName(AtomicSafetyHandle handle) => SafetyNodes.WriterName(handle);
        public static int GetReaderArray(AtomicSafetyHandle handle, int maxCount, IntPtr output) => SafetyNodes.Readers(handle, maxCount, output);
        public static string GetReaderName(AtomicSafetyHandle handle, int readerIndex) => SafetyNodes.ReaderName(handle, readerIndex);

        /// <summary>An id for a container type, so messages name it.</summary>
        public static int NewStaticSafetyId<T>() => SafetyNodes.StaticId(typeof(T).ToString());
        public static int NewStaticSafetyId(byte* ownerTypeNameBytes, int byteCount) =>
            SafetyNodes.StaticId(System.Text.Encoding.UTF8.GetString(ownerTypeNameBytes, byteCount));
        public static void SetStaticSafetyId(ref AtomicSafetyHandle handle, int sharedStaticId) => SafetyNodes.SetStaticId(handle, sharedStaticId);
        public static void SetCustomErrorMessage(int sharedStaticId, AtomicSafetyErrorType errorType, byte* messageBytes, int byteCount) =>
            SafetyNodes.SetCustomMessage(sharedStaticId, errorType, System.Text.Encoding.UTF8.GetString(messageBytes, byteCount));

        /// <summary>A container whose Dispose is not for the user (the handle cannot be released).</summary>
        public static void PrepareUndisposable(ref AtomicSafetyHandle handle) => SafetyNodes.SetFlag(handle, SafetyNodes.Undisposable, true);

        public static void SetNestedContainer(AtomicSafetyHandle handle, bool isNested) => SafetyNodes.SetFlag(handle, SafetyNodes.Nested, isNested);
        public static bool GetNestedContainer(AtomicSafetyHandle handle) => SafetyNodes.HasFlag(handle, SafetyNodes.Nested);
    }

    /// <summary>What a custom safety message replaces.</summary>
    public enum AtomicSafetyErrorType
    {
        Deallocated = 0,
        DeallocatedFromJob = 1,
        NotAllocatedFromJob = 2,
    }

    /// <summary>Whether the safety checks run: development builds (decided when the C# side starts).</summary>
    static class CollectionChecks
    {
        internal static readonly bool Enabled = HelixEngine.Debug.isDebugBuild || Environment.GetEnvironmentVariable("HELIX_COLLECTIONS_CHECKS") == "1";
    }

    // The nodes the handles point at: native memory (job structs carry handles into native memory), reused after a
    // release with a new version.
    static unsafe class SafetyNodes
    {
        internal const int DenyAccess = 1, DenySecondaryWrite = 2, BumpOnSchedule = 4, Undisposable = 8, Nested = 16;
        const int MaxReaders = 8;

        [StructLayout(LayoutKind.Sequential)]
        struct Node
        {
            public int version;
            public int secondaryVersion;
            public int flags;
            public int staticId;
            public ulong writer;
            public int writerName;
            public int readerCount;
            public fixed ulong readers[MaxReaders];
            public fixed int readerNames[MaxReaders];
            // Readers past MaxReaders, combined.
            public ulong moreReaders;
        }

        static readonly object s_Lock = new();
        static readonly Stack<IntPtr> s_Free = new();
        static readonly List<string> s_Names = new() { "" };
        static readonly Dictionary<string, int> s_NameIds = new();
        static readonly List<string> s_StaticNames = new() { "" };
        static readonly Dictionary<(int, AtomicSafetyErrorType), string> s_CustomMessages = new();

        static Node* N(in AtomicSafetyHandle h) => (Node*)h.versionNode;

        internal static AtomicSafetyHandle Create()
        {
            IntPtr p;
            lock (s_Lock)
                p = s_Free.Count > 0 ? s_Free.Pop() : (IntPtr)NativeMemory.AllocZeroed((nuint)sizeof(Node));
            var n = (Node*)p;
            int version = n->version;
            *n = default;
            n->version = version;
            n->secondaryVersion = version + 1;
            return new AtomicSafetyHandle { versionNode = p, version = version };
        }

        internal static void Release(in AtomicSafetyHandle h)
        {
            if (h.versionNode == IntPtr.Zero)
                return;
            Node* n = N(h);
            if (n->version != h.version)
                return;
            n->version += 2;
            n->secondaryVersion = n->version + 1;
            lock (s_Lock)
                s_Free.Push(h.versionNode);
        }

        internal static bool Alive(in AtomicSafetyHandle h)
        {
            Node* n = N(h);
            return (h.flags & AtomicSafetyHandle.Secondary) != 0 ? n->secondaryVersion == h.version : n->version == h.version;
        }

        internal static string TypeName(in AtomicSafetyHandle h)
        {
            int id = h.versionNode == IntPtr.Zero ? 0 : N(h)->staticId;
            return id > 0 && id < s_StaticNames.Count ? s_StaticNames[id] : "The native container";
        }

        static string Custom(in AtomicSafetyHandle h, AtomicSafetyErrorType type, string fallback)
        {
            int id = N(h)->staticId;
            lock (s_Lock)
                return id > 0 && s_CustomMessages.TryGetValue((id, type), out string m) ? m : fallback;
        }

        static void CheckExists(in AtomicSafetyHandle h)
        {
            if (!Alive(h))
                throw new ObjectDisposedException(null, Custom(h, AtomicSafetyErrorType.Deallocated, TypeName(h) + " has been deallocated, it is not allowed to access it"));
        }

        // A job is the container's until the main thread completed it (finished on a worker is not enough).
        static bool Pending(ulong handle) => !JobDependencies.IsSynced(handle);

        internal static void CheckRead(in AtomicSafetyHandle h)
        {
            CheckExists(h);
            if ((h.flags & AtomicSafetyHandle.InJob) != 0)
            {
                if ((h.flags & AtomicSafetyHandle.JobWriteOnly) != 0)
                    throw new InvalidOperationException($"The {TypeName(h)} has been declared as [WriteOnly] in the job, but you are reading from it.");
                return;
            }
            Node* n = N(h);
            if ((n->flags & DenyAccess) != 0)
                throw new InvalidOperationException($"The {TypeName(h)} can not be accessed: a job owns it.");
            if (Pending(n->writer))
            {
                string job = JobName(n->writerName);
                throw new InvalidOperationException(
                    $"The previously scheduled job {job} writes to the {TypeName(h)} {FieldName(n->writerName)}. You must call JobHandle.Complete() on the job {job}, before you can read from the {TypeName(h)} safely.");
            }
        }

        internal static void CheckWrite(in AtomicSafetyHandle h)
        {
            CheckExists(h);
            if ((h.flags & AtomicSafetyHandle.InJob) != 0)
            {
                if ((h.flags & AtomicSafetyHandle.JobReadOnly) != 0)
                    throw new InvalidOperationException($"The {TypeName(h)} has been declared as [ReadOnly] in the job, but you are writing to it.");
                return;
            }
            Node* n = N(h);
            if ((n->flags & DenyAccess) != 0)
                throw new InvalidOperationException($"The {TypeName(h)} can not be accessed: a job owns it.");
            CheckJobsDone(h, "write to");
        }

        static void CheckJobsDone(in AtomicSafetyHandle h, string what)
        {
            Node* n = N(h);
            if (Pending(n->writer))
            {
                string job = JobName(n->writerName);
                throw new InvalidOperationException(
                    $"The previously scheduled job {job} writes to the {TypeName(h)} {FieldName(n->writerName)}. You must call JobHandle.Complete() on the job {job}, before you can {what} the {TypeName(h)} safely.");
            }
            for (int i = 0; i < n->readerCount; i++)
                if (Pending(n->readers[i]))
                {
                    string job = JobName(n->readerNames[i]);
                    throw new InvalidOperationException(
                        $"The previously scheduled job {job} reads from the {TypeName(h)} {FieldName(n->readerNames[i])}. You must call JobHandle.Complete() on the job {job}, before you can {what} the {TypeName(h)} safely.");
                }
            if (Pending(n->moreReaders))
                throw new InvalidOperationException($"Previously scheduled jobs read from the {TypeName(h)}. You must call JobHandle.Complete() on them, before you can {what} the {TypeName(h)} safely.");
        }

        internal static void CheckDeallocate(in AtomicSafetyHandle h)
        {
            if (!Alive(h))
                throw new ObjectDisposedException(null, Custom(h, AtomicSafetyErrorType.Deallocated, TypeName(h) + " has been deallocated, it is not allowed to access it"));
            if ((h.flags & AtomicSafetyHandle.InJob) != 0)
                throw new InvalidOperationException(Custom(h, AtomicSafetyErrorType.DeallocatedFromJob, $"The {TypeName(h)} can not be Disposed in a job: it was not allocated in the job."));
            if ((N(h)->flags & Undisposable) != 0)
                throw new InvalidOperationException($"The {TypeName(h)} can not be Disposed because it was not allocated with a valid allocator.");
            CheckJobsDone(h, "deallocate");
        }

        internal static void BumpSecondary(in AtomicSafetyHandle h) => N(h)->secondaryVersion += 2;
        internal static int SecondaryVersion(in AtomicSafetyHandle h) => N(h)->secondaryVersion;

        internal static void SetFlag(in AtomicSafetyHandle h, int flag, bool on)
        {
            if (h.versionNode == IntPtr.Zero)
                return;
            if (on)
                N(h)->flags |= flag;
            else
                N(h)->flags &= ~flag;
        }

        internal static bool HasFlag(in AtomicSafetyHandle h, int flag) => h.versionNode != IntPtr.Zero && (N(h)->flags & flag) != 0;

        internal static JobHandle CompleteAll(in AtomicSafetyHandle h, bool release)
        {
            if (h.versionNode == IntPtr.Zero)
                return default;
            Node* n = N(h);
            new JobHandle(n->writer).Complete();
            for (int i = 0; i < n->readerCount; i++)
                new JobHandle(n->readers[i]).Complete();
            new JobHandle(n->moreReaders).Complete();
            n->writer = 0;
            n->readerCount = 0;
            n->moreReaders = 0;
            return default;
        }

        internal static JobHandle Writer(in AtomicSafetyHandle h) => h.versionNode == IntPtr.Zero ? default : new JobHandle(N(h)->writer);
        internal static string WriterName(in AtomicSafetyHandle h) => h.versionNode == IntPtr.Zero ? null : FieldName(N(h)->writerName);

        internal static int Readers(in AtomicSafetyHandle h, int maxCount, IntPtr output)
        {
            if (h.versionNode == IntPtr.Zero)
                return 0;
            Node* n = N(h);
            int count = Math.Min(maxCount, n->readerCount);
            for (int i = 0; i < count; i++)
                ((JobHandle*)output)[i] = new JobHandle(n->readers[i]);
            return n->readerCount;
        }

        internal static string ReaderName(in AtomicSafetyHandle h, int index) =>
            h.versionNode == IntPtr.Zero || index < 0 || index >= N(h)->readerCount ? null : FieldName(N(h)->readerNames[index]);

        internal static int StaticId(string name)
        {
            lock (s_Lock)
            {
                s_StaticNames.Add(name);
                return s_StaticNames.Count - 1;
            }
        }

        internal static void SetStaticId(in AtomicSafetyHandle h, int id)
        {
            if (h.versionNode != IntPtr.Zero)
                N(h)->staticId = id;
        }

        internal static void SetCustomMessage(int id, AtomicSafetyErrorType type, string message)
        {
            lock (s_Lock)
                s_CustomMessages[(id, type)] = message;
        }

        // ------------------------------------------------------------------------------------------- jobs

        // "Job.field" names, kept by index (the nodes are native memory).
        internal static int NameId(string job, string field)
        {
            string key = job + "\n" + field;
            lock (s_Lock)
            {
                if (!s_NameIds.TryGetValue(key, out int id))
                {
                    s_Names.Add(key);
                    id = s_Names.Count - 1;
                    s_NameIds[key] = id;
                }
                return id;
            }
        }

        static string JobName(int id)
        {
            string s = id > 0 && id < s_Names.Count ? s_Names[id] : "";
            int at = s.IndexOf('\n');
            return at < 0 ? s : s.Substring(0, at);
        }

        static string FieldName(int id)
        {
            string s = id > 0 && id < s_Names.Count ? s_Names[id] : "";
            return s.Replace('\n', '.');
        }

        static string Field(int id)
        {
            string s = id > 0 && id < s_Names.Count ? s_Names[id] : "";
            int at = s.IndexOf('\n');
            return at < 0 ? s : s.Substring(at + 1);
        }

        /// <summary>Checks a container a new job uses against the jobs scheduled with it (no dependency on them is an error).</summary>
        internal static void CheckSchedule(in AtomicSafetyHandle h, bool writes, int name, JobHandle dependsOn)
        {
            CheckExists(h);
            Node* n = N(h);
            string job = JobName(name);
            if (Pending(n->writer) && !JobDependencies.IsDependency(n->writer, dependsOn.m_Handle))
            {
                string other = JobName(n->writerName);
                throw new InvalidOperationException(
                    $"The previously scheduled job {other} writes to the {TypeName(h)} {FieldName(n->writerName)}. You are trying to schedule a new job {job}, which {(writes ? "writes to" : "reads from")} the same {TypeName(h)} (via {FieldName(name)}). To guarantee safety, you must include {other} as a dependency of the newly scheduled job.");
            }
            if (!writes)
                return;
            for (int i = 0; i < n->readerCount; i++)
                if (Pending(n->readers[i]) && !JobDependencies.IsDependency(n->readers[i], dependsOn.m_Handle))
                {
                    string other = JobName(n->readerNames[i]);
                    throw new InvalidOperationException(
                        $"The previously scheduled job {other} reads from the {TypeName(h)} {FieldName(n->readerNames[i])}. You are trying to schedule a new job {job}, which writes to the same {TypeName(h)} (via {FieldName(name)}). To guarantee safety, you must include {other} as a dependency of the newly scheduled job.");
                }
        }

        internal static void Scheduled(in AtomicSafetyHandle h, bool writes, int name, ulong job)
        {
            Node* n = N(h);
            if (writes)
            {
                n->writer = job;
                n->writerName = name;
                n->readerCount = 0;
                n->moreReaders = 0;
                if ((n->flags & BumpOnSchedule) != 0)
                    n->secondaryVersion += 2;
                return;
            }
            // Finished readers make room.
            int w = 0;
            for (int i = 0; i < n->readerCount; i++)
                if (Pending(n->readers[i]))
                {
                    n->readers[w] = n->readers[i];
                    n->readerNames[w] = n->readerNames[i];
                    w++;
                }
            n->readerCount = w;
            if (w < MaxReaders)
            {
                n->readers[w] = job;
                n->readerNames[w] = name;
                n->readerCount = w + 1;
            }
            else
                n->moreReaders = n->moreReaders == 0 ? job : JobHandle.CombineDependencies(new JobHandle(n->moreReaders), new JobHandle(job)).m_Handle;
        }
    }

    // Which scheduled handles each handle waits for, and which ones the main thread has not completed yet
    // (development builds), for "is it a dependency" and "does a job still own the container".
    static class JobDependencies
    {
        static readonly object s_Lock = new();
        static readonly Dictionary<ulong, ulong[]> s_Parents = new();

        internal static void Add(ulong handle, params ulong[] parents)
        {
            if (!CollectionChecks.Enabled || handle == 0)
                return;
            lock (s_Lock)
                s_Parents[handle] = parents;
        }

        internal static bool IsSynced(ulong handle)
        {
            if (handle == 0)
                return true;
            lock (s_Lock)
                return !s_Parents.ContainsKey(handle);
        }

        /// <summary>The main thread completed the handle: it and what it waited for are synced.</summary>
        internal static void Sync(ulong handle)
        {
            if (handle == 0)
                return;
            lock (s_Lock)
            {
                var stack = new Stack<ulong>();
                stack.Push(handle);
                while (stack.Count > 0)
                {
                    ulong h = stack.Pop();
                    if (h != 0 && s_Parents.Remove(h, out ulong[] parents))
                        foreach (ulong p in parents)
                            stack.Push(p);
                }
            }
        }

        /// <summary>The job is synced, or `of` waits for it.</summary>
        internal static bool IsDependency(ulong job, ulong of)
        {
            if (IsSynced(job))
                return true;
            lock (s_Lock)
            {
                var stack = new Stack<ulong>();
                var seen = new HashSet<ulong>();
                stack.Push(of);
                while (stack.Count > 0)
                {
                    ulong h = stack.Pop();
                    if (h == 0 || !seen.Add(h))
                        continue;
                    if (h == job)
                        return true;
                    if (s_Parents.TryGetValue(h, out ulong[] parents))
                        foreach (ulong p in parents)
                            stack.Push(p);
                }
            }
            return false;
        }
    }

    // Allocations not disposed (development builds): reported when the application quits, or when asked.
    static unsafe class LeakTracker
    {
        static readonly object s_Lock = new();
        static readonly Dictionary<IntPtr, string> s_Live = new();
        internal static NativeLeakDetectionMode Mode = NativeLeakDetectionMode.Enabled;
        static bool s_Hooked;

        internal static void Track(void* pointer, Allocator allocator)
        {
            if (!CollectionChecks.Enabled || Mode == NativeLeakDetectionMode.Disabled || (allocator != Allocator.Persistent && allocator != Allocator.TempJob))
                return;
            string trace = Mode == NativeLeakDetectionMode.EnabledWithStackTrace ? Environment.StackTrace : null;
            lock (s_Lock)
            {
                s_Live[(IntPtr)pointer] = trace;
                if (!s_Hooked)
                {
                    s_Hooked = true;
                    HelixEngine.Application.quitting += () => Report();
                }
            }
        }

        internal static void Untrack(void* pointer)
        {
            lock (s_Lock)
                s_Live.Remove((IntPtr)pointer);
        }

        /// <summary>Logs each allocation not disposed; their count.</summary>
        internal static int Report()
        {
            List<string> traces;
            lock (s_Lock)
            {
                traces = new List<string>(s_Live.Values);
                s_Live.Clear();
            }
            foreach (string trace in traces)
                HelixEngine.Debug.LogError(trace != null
                    ? "A Native Collection has not been disposed, resulting in a memory leak. Allocated from:\n" + trace
                    : "A Native Collection has not been disposed, resulting in a memory leak. Enable Full StackTraces to get more details. Leak tracking may be enabled via `Helix.Collections.NativeLeakDetection.Mode`.");
            return traces.Count;
        }

        internal static int Count
        {
            get
            {
                lock (s_Lock)
                    return s_Live.Count;
            }
        }
    }
}
