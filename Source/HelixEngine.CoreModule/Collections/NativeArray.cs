using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Helix.Collections.LowLevel.Unsafe;
using Helix.Jobs;

namespace Helix.Collections
{
    /// <summary>
    /// An array in native memory: jobs and C++ code read it where it is (a C++ span of its buffer and length). It
    /// lives until disposed (Temp memory until the end of the frame); development builds check its use (index ranges,
    /// use after Dispose, jobs using it).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    [NativeContainer]
    [NativeContainerSupportsMinMaxWriteRestriction]
    [NativeContainerSupportsDeallocateOnJobCompletion]
    [NativeContainerSupportsDeferredConvertListToArray]
    public unsafe struct NativeArray<T> : IDisposable, IEnumerable<T>, IEquatable<NativeArray<T>> where T : struct
    {
        // The layout C++ reads (Helix.Collections' header): buffer, length, allocator, then the checks' fields.
        [NativeDisableUnsafePtrRestriction] internal void* m_Buffer;
        internal int m_Length;
        internal Allocator m_AllocatorLabel;
        internal int m_MinIndex;
        internal int m_MaxIndex;
        internal AtomicSafetyHandle m_Safety;

        static readonly int s_StaticSafetyId = CollectionChecks.Enabled ? AtomicSafetyHandle.NewStaticSafetyId<NativeArray<T>>() : 0;

        public NativeArray(int length, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory)
        {
            Allocate(length, allocator, out this);
            if ((options & NativeArrayOptions.ClearMemory) == NativeArrayOptions.ClearMemory)
                UnsafeUtility.MemClear(m_Buffer, (long)Length * UnsafeUtility.SizeOf<T>());
        }

        public NativeArray(T[] array, Allocator allocator)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));
            Allocate(array.Length, allocator, out this);
            Copy(array, this);
        }

        public NativeArray(NativeArray<T> array, Allocator allocator)
        {
            AtomicSafetyHandle.CheckReadAndThrow(array.m_Safety);
            Allocate(array.Length, allocator, out this);
            Copy(array, 0, this, 0, array.Length);
        }

        static void Allocate(int length, Allocator allocator, out NativeArray<T> array)
        {
            long totalSize = (long)UnsafeUtility.SizeOf<T>() * length;
            if (allocator <= Allocator.None)
                throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof(allocator));
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "Length must be >= 0");
            CheckElementType();
            if (totalSize > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(length), $"Length * sizeof(T) cannot exceed {int.MaxValue} bytes");
            array = default;
            array.m_Buffer = UnsafeUtility.Malloc(totalSize, UnsafeUtility.AlignOf<T>(), allocator);
            array.m_Length = length;
            array.m_AllocatorLabel = allocator;
            array.m_MinIndex = 0;
            array.m_MaxIndex = length - 1;
            if (CollectionChecks.Enabled)
            {
                array.m_Safety = AtomicSafetyHandle.Create();
                AtomicSafetyHandle.SetStaticSafetyId(ref array.m_Safety, s_StaticSafetyId);
                LeakTracker.Track(array.m_Buffer, allocator);
            }
        }

        internal static void CheckElementType()
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                throw new InvalidOperationException($"{typeof(T)} used in NativeArray<{typeof(T)}> must be unmanaged (contain no managed types) and cannot itself be a native container type.");
        }

        public readonly int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_Length;
        }

        public readonly bool IsCreated => m_Buffer != null;

        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (CollectionChecks.Enabled)
                    CheckElementRead(index);
                return UnsafeUtility.ReadArrayElement<T>(m_Buffer, index);
            }
            [WriteAccessRequired]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (CollectionChecks.Enabled)
                    CheckElementWrite(index);
                UnsafeUtility.WriteArrayElement(m_Buffer, index, value);
            }
        }

        readonly void CheckElementRead(int index)
        {
            CheckIndex(index);
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
        }

        readonly void CheckElementWrite(int index)
        {
            CheckIndex(index);
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
        }

        readonly void CheckIndex(int index)
        {
            if ((uint)index >= (uint)m_Length)
                throw new IndexOutOfRangeException($"Index {index} is out of range of '{m_Length}' Length.");
            if (index < m_MinIndex || index > m_MaxIndex)
                throw new IndexOutOfRangeException(
                    $"Index {index} is out of restricted IJobParallelFor range [{m_MinIndex}...{m_MaxIndex}] in ReadWriteBuffer.\nReadWriteBuffers are restricted to only read & write the element at the job index. You can use double buffering strategies to avoid race conditions due to reading & writing in parallel to the same elements from a job.");
        }

        [WriteAccessRequired]
        public void Dispose()
        {
            if (m_Buffer == null)
                throw new ObjectDisposedException(null, "The NativeArray is already disposed.");
            if (m_AllocatorLabel == Allocator.Invalid)
                throw new InvalidOperationException("The NativeArray can not be Disposed because it was not allocated with a valid allocator.");
            if (CollectionChecks.Enabled)
            {
                AtomicSafetyHandle.CheckDeallocateAndThrow(m_Safety);
                AtomicSafetyHandle.Release(m_Safety);
                LeakTracker.Untrack(m_Buffer);
            }
            if (m_AllocatorLabel > Allocator.None)
                UnsafeUtility.Free(m_Buffer, m_AllocatorLabel);
            m_Buffer = null;
            m_Length = 0;
        }

        /// <summary>Disposed by a job after inputDeps.</summary>
        public JobHandle Dispose(JobHandle inputDeps)
        {
            if (m_AllocatorLabel == Allocator.Invalid)
                throw new InvalidOperationException("The NativeArray can not be Disposed because it was not allocated with a valid allocator.");
            if (m_Buffer == null)
                throw new InvalidOperationException("The NativeArray is already disposed.");
            if (CollectionChecks.Enabled)
            {
                AtomicSafetyHandle.CheckDeallocateAndThrow(m_Safety);
                AtomicSafetyHandle.Release(m_Safety);
                LeakTracker.Untrack(m_Buffer);
            }
            JobHandle h = m_AllocatorLabel > Allocator.None ? new DisposeJob { buffer = m_Buffer, allocator = m_AllocatorLabel }.Schedule(inputDeps) : inputDeps;
            m_Buffer = null;
            m_Length = 0;
            return h;
        }

        public void CopyFrom(T[] array) => Copy(array, this);
        public void CopyFrom(NativeArray<T> array) => Copy(array, this);
        public readonly void CopyTo(T[] array) => Copy(this, array);
        public readonly void CopyTo(NativeArray<T> array) => Copy(this, array);

        public readonly T[] ToArray()
        {
            var array = new T[Length];
            Copy(this, array, Length);
            return array;
        }

        public readonly NativeArray<T>.ReadOnly AsReadOnly() => new ReadOnly(m_Buffer, m_Length, m_Safety);

        public readonly Span<T> AsSpan()
        {
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
            return new Span<T>(m_Buffer, m_Length);
        }

        public readonly ReadOnlySpan<T> AsReadOnlySpan()
        {
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
            return new ReadOnlySpan<T>(m_Buffer, m_Length);
        }

        /// <summary>A view of `length` elements from `start` (not disposed itself).</summary>
        public NativeArray<T> GetSubArray(int start, int length)
        {
            if (start < 0)
                throw new ArgumentOutOfRangeException(nameof(start), $"start {start} must be >= 0");
            if (length < 0 || start + length > Length)
                throw new ArgumentOutOfRangeException(nameof(length), $"sub array range {start}-{start + length - 1} is outside the range of the native array 0-{Length - 1}");
            var result = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>((byte*)m_Buffer + (long)UnsafeUtility.SizeOf<T>() * start, length, Allocator.None);
            result.m_Safety = m_Safety;
            return result;
        }

        public NativeArray<U> Reinterpret<U>() where U : struct
        {
            int tSize = UnsafeUtility.SizeOf<T>(), uSize = UnsafeUtility.SizeOf<U>();
            if (tSize != uSize)
                throw new InvalidOperationException($"Types {typeof(T)} and {typeof(U)} are different sizes - direct reinterpretation is not possible. If this is what you intended, use Reinterpret(<type size>)");
            return InterpretAs<U>(m_Length);
        }

        public NativeArray<U> Reinterpret<U>(int expectedTypeSize) where U : struct
        {
            int tSize = UnsafeUtility.SizeOf<T>(), uSize = UnsafeUtility.SizeOf<U>();
            if (tSize != expectedTypeSize)
                throw new InvalidOperationException($"Type {typeof(T)} was expected to be {expectedTypeSize} but is {tSize} bytes");
            long bytes = (long)m_Length * tSize;
            if (bytes % uSize != 0)
                throw new InvalidOperationException($"Types {typeof(T)} (array length {m_Length}) and {typeof(U)} cannot be aliased due to size constraints. The size of the types and lengths involved must line up.");
            return InterpretAs<U>((int)(bytes / uSize));
        }

        NativeArray<U> InterpretAs<U>(int length) where U : struct
        {
            var result = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<U>(m_Buffer, length, Allocator.None);
            result.m_Safety = m_Safety;
            return result;
        }

        /// <summary>The bytes of element sourceIndex (of T) as a U.</summary>
        public readonly U ReinterpretLoad<U>(int sourceIndex) where U : struct
        {
            CheckReinterpretRange<U>(sourceIndex);
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
            return Unsafe.Read<U>((byte*)m_Buffer + (long)UnsafeUtility.SizeOf<T>() * sourceIndex);
        }

        public void ReinterpretStore<U>(int destIndex, U data) where U : struct
        {
            CheckReinterpretRange<U>(destIndex);
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
            Unsafe.Write((byte*)m_Buffer + (long)UnsafeUtility.SizeOf<T>() * destIndex, data);
        }

        readonly void CheckReinterpretRange<U>(int index) where U : struct
        {
            long start = (long)UnsafeUtility.SizeOf<T>() * index;
            if (index < 0 || start + UnsafeUtility.SizeOf<U>() > (long)UnsafeUtility.SizeOf<T>() * m_Length)
                throw new ArgumentOutOfRangeException(nameof(index), "loaded byte range must fall inside container bounds");
        }

        public Enumerator GetEnumerator() => new Enumerator(ref this);
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => new Enumerator(ref this);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool Equals(NativeArray<T> other) => m_Buffer == other.m_Buffer && m_Length == other.m_Length;
        public override bool Equals(object obj) => obj is NativeArray<T> other && Equals(other);
        public override int GetHashCode() => ((int)(long)m_Buffer * 397) ^ m_Length;
        public static bool operator ==(NativeArray<T> left, NativeArray<T> right) => left.Equals(right);
        public static bool operator !=(NativeArray<T> left, NativeArray<T> right) => !left.Equals(right);

        // ------------------------------------------------------------------------------------------- copies

        public static void Copy(NativeArray<T> src, NativeArray<T> dst)
        {
            CheckCopyLengths(src.Length, dst.Length);
            CopySafely(src.m_Buffer, src.m_Safety, 0, dst, 0, src.Length);
        }

        public static void Copy(ReadOnly src, NativeArray<T> dst)
        {
            CheckCopyLengths(src.Length, dst.Length);
            CopySafely(src.m_Buffer, src.m_Safety, 0, dst, 0, src.Length);
        }

        public static void Copy(T[] src, NativeArray<T> dst)
        {
            CheckCopyLengths(src.Length, dst.Length);
            Copy(src, 0, dst, 0, src.Length);
        }

        public static void Copy(NativeArray<T> src, T[] dst)
        {
            CheckCopyLengths(src.Length, dst.Length);
            Copy(src, 0, dst, 0, src.Length);
        }

        public static void Copy(ReadOnly src, T[] dst)
        {
            CheckCopyLengths(src.Length, dst.Length);
            Copy(src, 0, dst, 0, src.Length);
        }

        public static void Copy(NativeArray<T> src, NativeArray<T> dst, int length) => Copy(src, 0, dst, 0, length);
        public static void Copy(ReadOnly src, NativeArray<T> dst, int length) => Copy(src, 0, dst, 0, length);
        public static void Copy(T[] src, NativeArray<T> dst, int length) => Copy(src, 0, dst, 0, length);
        public static void Copy(NativeArray<T> src, T[] dst, int length) => Copy(src, 0, dst, 0, length);
        public static void Copy(ReadOnly src, T[] dst, int length) => Copy(src, 0, dst, 0, length);

        public static void Copy(NativeArray<T> src, int srcIndex, NativeArray<T> dst, int dstIndex, int length)
        {
            CheckCopyArguments(src.Length, srcIndex, dst.Length, dstIndex, length);
            CopySafely(src.m_Buffer, src.m_Safety, srcIndex, dst, dstIndex, length);
        }

        public static void Copy(ReadOnly src, int srcIndex, NativeArray<T> dst, int dstIndex, int length)
        {
            CheckCopyArguments(src.Length, srcIndex, dst.Length, dstIndex, length);
            CopySafely(src.m_Buffer, src.m_Safety, srcIndex, dst, dstIndex, length);
        }

        public static void Copy(T[] src, int srcIndex, NativeArray<T> dst, int dstIndex, int length)
        {
            if (src == null)
                throw new ArgumentNullException(nameof(src));
            CheckCopyArguments(src.Length, srcIndex, dst.Length, dstIndex, length);
            AtomicSafetyHandle.CheckWriteAndThrow(dst.m_Safety);
            new ReadOnlySpan<T>(src, srcIndex, length).CopyTo(new Span<T>((byte*)dst.m_Buffer + (long)dstIndex * UnsafeUtility.SizeOf<T>(), length));
        }

        public static void Copy(NativeArray<T> src, int srcIndex, T[] dst, int dstIndex, int length)
        {
            if (dst == null)
                throw new ArgumentNullException(nameof(dst));
            CheckCopyArguments(src.Length, srcIndex, dst.Length, dstIndex, length);
            AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);
            new ReadOnlySpan<T>((byte*)src.m_Buffer + (long)srcIndex * UnsafeUtility.SizeOf<T>(), length).CopyTo(new Span<T>(dst, dstIndex, length));
        }

        public static void Copy(ReadOnly src, int srcIndex, T[] dst, int dstIndex, int length)
        {
            if (dst == null)
                throw new ArgumentNullException(nameof(dst));
            CheckCopyArguments(src.Length, srcIndex, dst.Length, dstIndex, length);
            AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);
            new ReadOnlySpan<T>((byte*)src.m_Buffer + (long)srcIndex * UnsafeUtility.SizeOf<T>(), length).CopyTo(new Span<T>(dst, dstIndex, length));
        }

        static void CopySafely(void* src, AtomicSafetyHandle srcSafety, int srcIndex, NativeArray<T> dst, int dstIndex, int length)
        {
            AtomicSafetyHandle.CheckReadAndThrow(srcSafety);
            AtomicSafetyHandle.CheckWriteAndThrow(dst.m_Safety);
            int size = UnsafeUtility.SizeOf<T>();
            UnsafeUtility.MemMove((byte*)dst.m_Buffer + (long)dstIndex * size, (byte*)src + (long)srcIndex * size, (long)length * size);
        }

        static void CheckCopyLengths(int srcLength, int dstLength)
        {
            if (srcLength != dstLength)
                throw new ArgumentException("source and destination length must be the same");
        }

        static void CheckCopyArguments(int srcLength, int srcIndex, int dstLength, int dstIndex, int length)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "length must be equal or greater than zero.");
            if (srcIndex < 0 || srcIndex > srcLength || (srcIndex == srcLength && srcLength > 0))
                throw new ArgumentOutOfRangeException(nameof(srcIndex), "srcIndex is outside the range of valid indexes for the source NativeArray.");
            if (dstIndex < 0 || dstIndex > dstLength || (dstIndex == dstLength && dstLength > 0))
                throw new ArgumentOutOfRangeException(nameof(dstIndex), "dstIndex is outside the range of valid indexes for the destination NativeArray.");
            if (srcIndex + length > srcLength)
                throw new ArgumentException("length is greater than the number of elements from srcIndex to the end of the source NativeArray.", nameof(length));
            if (dstIndex + length > dstLength)
                throw new ArgumentException("length is greater than the number of elements from dstIndex to the end of the destination NativeArray.", nameof(length));
        }

        // ------------------------------------------------------------------------------------------- nested types

        /// <summary>Iterates the elements.</summary>
        public struct Enumerator : IEnumerator<T>
        {
            NativeArray<T> m_Array;
            int m_Index;
            T value;

            public Enumerator(ref NativeArray<T> array)
            {
                m_Array = array;
                m_Index = -1;
                value = default;
            }

            public void Dispose() { }

            public bool MoveNext()
            {
                m_Index++;
                if (m_Index < m_Array.m_Length)
                {
                    AtomicSafetyHandle.CheckReadAndThrow(m_Array.m_Safety);
                    value = UnsafeUtility.ReadArrayElement<T>(m_Array.m_Buffer, m_Index);
                    return true;
                }
                value = default;
                return false;
            }

            public void Reset() => m_Index = -1;
            public T Current => value;
            object IEnumerator.Current => Current;
        }

        /// <summary>A read only view of a NativeArray (a job may read it at the same time as others).</summary>
        [NativeContainer]
        [NativeContainerIsReadOnly]
        [StructLayout(LayoutKind.Sequential)]
        public struct ReadOnly : IEnumerable<T>
        {
            [NativeDisableUnsafePtrRestriction] internal void* m_Buffer;
            internal int m_Length;
            internal AtomicSafetyHandle m_Safety;

            internal ReadOnly(void* buffer, int length, AtomicSafetyHandle safety)
            {
                m_Buffer = buffer;
                m_Length = length;
                m_Safety = safety;
            }

            public readonly int Length => m_Length;
            public readonly bool IsCreated => m_Buffer != null;

            public readonly T this[int index]
            {
                get
                {
                    if (CollectionChecks.Enabled)
                    {
                        if ((uint)index >= (uint)m_Length)
                            throw new IndexOutOfRangeException($"Index {index} is out of range of '{m_Length}' Length.");
                        AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
                    }
                    return UnsafeUtility.ReadArrayElement<T>(m_Buffer, index);
                }
            }

            public readonly void CopyTo(T[] array) => Copy(this, array);
            public readonly void CopyTo(NativeArray<T> array) => Copy(this, array);

            public readonly T[] ToArray()
            {
                var array = new T[m_Length];
                Copy(this, array, m_Length);
                return array;
            }

            public readonly ReadOnlySpan<T> AsReadOnlySpan()
            {
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
                return new ReadOnlySpan<T>(m_Buffer, m_Length);
            }

            public readonly NativeArray<U>.ReadOnly Reinterpret<U>() where U : struct
            {
                if (UnsafeUtility.SizeOf<T>() != UnsafeUtility.SizeOf<U>())
                    throw new InvalidOperationException($"Types {typeof(T)} and {typeof(U)} are different sizes - direct reinterpretation is not possible. If this is what you intended, use Reinterpret(<type size>)");
                return new NativeArray<U>.ReadOnly(m_Buffer, m_Length, m_Safety);
            }

            public readonly U ReinterpretLoad<U>(int sourceIndex) where U : struct
            {
                if (sourceIndex < 0 || (long)UnsafeUtility.SizeOf<T>() * sourceIndex + UnsafeUtility.SizeOf<U>() > (long)UnsafeUtility.SizeOf<T>() * m_Length)
                    throw new ArgumentOutOfRangeException(nameof(sourceIndex), "loaded byte range must fall inside container bounds");
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
                return Unsafe.Read<U>((byte*)m_Buffer + (long)UnsafeUtility.SizeOf<T>() * sourceIndex);
            }

            public Enumerator GetEnumerator()
            {
                var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(m_Buffer, m_Length, Allocator.None);
                array.m_Safety = m_Safety;
                return new Enumerator(ref array);
            }

            IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        // The job of Dispose(JobHandle).
        struct DisposeJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] public void* buffer;
            public Allocator allocator;
            public void Execute() => UnsafeUtility.Free(buffer, allocator);
        }
    }
}
