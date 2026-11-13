using System;
using Helix.Collections.LowLevel.Unsafe;

namespace Helix.Collections
{
    /// <summary>Where the memory of a native container comes from.</summary>
    public enum Allocator
    {
        Invalid = 0,
        /// <summary>No allocation (a view of memory someone else owns).</summary>
        None = 1,
        /// <summary>For this frame only: freed at its end (fast, never disposed by hand necessarily).</summary>
        Temp = 2,
        /// <summary>For jobs: at most four frames.</summary>
        TempJob = 3,
        /// <summary>Until disposed.</summary>
        Persistent = 4,
        AudioKernel = 5,
        Domain = 6,
        FirstUserIndex = 64,
    }

    /// <summary>Whether new native memory is cleared.</summary>
    public enum NativeArrayOptions
    {
        UninitializedMemory = 0,
        ClearMemory = 1,
    }

    public enum NativeLeakDetectionMode
    {
        Disabled = 1,
        Enabled = 2,
        EnabledWithStackTrace = 3,
    }

    /// <summary>Reports native containers that were not disposed (development builds).</summary>
    public static class NativeLeakDetection
    {
        public static NativeLeakDetectionMode Mode
        {
            get => LeakTracker.Mode;
            set => LeakTracker.Mode = value;
        }
    }

    /// <summary>The job only reads the container (other jobs may read it at the same time).</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter)]
    public sealed class ReadOnlyAttribute : Attribute { }

    /// <summary>The job only writes the container.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class WriteOnlyAttribute : Attribute { }

    /// <summary>The container is disposed when the job is done.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class DeallocateOnJobCompletionAttribute : Attribute { }

    /// <summary>A parallel job may write any element, not only those of its index.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class NativeDisableParallelForRestrictionAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class NativeFixedLengthAttribute : Attribute
    {
        public int FixedLength;
        public NativeFixedLengthAttribute(int fixedLength) { FixedLength = fixedLength; }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class NativeMatchesParallelForLengthAttribute : Attribute { }
}

namespace Helix.Collections.LowLevel.Unsafe
{
    /// <summary>A struct is a native container: the safety system tracks it in jobs.</summary>
    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class NativeContainerAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class NativeContainerIsReadOnlyAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class NativeContainerIsAtomicWriteOnlyAttribute : Attribute { }

    /// <summary>The container has m_MinIndex and m_MaxIndex: a parallel job may only touch the elements of its range.</summary>
    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class NativeContainerSupportsMinMaxWriteRestrictionAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class NativeContainerSupportsDeallocateOnJobCompletionAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class NativeContainerSupportsDeferredConvertListToArray : Attribute { }

    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class NativeContainerSupportsDynamicIndexingAttribute : Attribute { }

    /// <summary>No safety checks for this container in the job.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class NativeDisableContainerSafetyRestrictionAttribute : Attribute { }

    /// <summary>A pointer field allowed in a job struct.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class NativeDisableUnsafePtrRestrictionAttribute : Attribute { }

    /// <summary>An int field the job system sets to the index of the thread running the job.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class NativeSetThreadIndexAttribute : Attribute { }

    /// <summary>A method that writes to its container.</summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    public sealed class WriteAccessRequiredAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class NativeSetClassTypeToNullOnScheduleAttribute : Attribute { }
}
