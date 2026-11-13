using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Helix.Collections.LowLevel.Unsafe;

namespace Helix.Collections
{
    /// <summary>A strided view of native memory (part of a NativeArray, or one field of its elements).</summary>
    [StructLayout(LayoutKind.Sequential)]
    [NativeContainer]
    [NativeContainerSupportsMinMaxWriteRestriction]
    public unsafe struct NativeSlice<T> : IEnumerable<T>, IEquatable<NativeSlice<T>> where T : struct
    {
        [NativeDisableUnsafePtrRestriction] internal byte* m_Buffer;
        internal int m_Stride;
        internal int m_Length;
        internal int m_MinIndex;
        internal int m_MaxIndex;
        internal AtomicSafetyHandle m_Safety;

        public NativeSlice(NativeSlice<T> slice, int start) : this(slice, start, slice.Length - start) { }

        public NativeSlice(NativeSlice<T> slice, int start, int length)
        {
            if (start < 0)
                throw new ArgumentOutOfRangeException(nameof(start), $"Slice start {start} < 0.");
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), $"Slice length {length} < 0.");
            if (start + length > slice.Length)
                throw new ArgumentException($"Slice start + length ({start + length}) range must be <= slice.Length ({slice.Length})");
            m_Stride = slice.m_Stride;
            m_Buffer = slice.m_Buffer + (long)m_Stride * start;
            m_Length = length;
            m_MinIndex = 0;
            m_MaxIndex = length - 1;
            m_Safety = slice.m_Safety;
        }

        public NativeSlice(NativeArray<T> array) : this(array, 0, array.Length) { }
        public NativeSlice(NativeArray<T> array, int start) : this(array, start, array.Length - start) { }

        public NativeSlice(NativeArray<T> array, int start, int length)
        {
            if (start < 0)
                throw new ArgumentOutOfRangeException(nameof(start), $"Slice start {start} < 0.");
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), $"Slice length {length} < 0.");
            if (start + length > array.Length)
                throw new ArgumentException($"Slice start + length ({start + length}) range must be <= array.Length ({array.Length})");
            m_Stride = UnsafeUtility.SizeOf<T>();
            m_Buffer = (byte*)array.m_Buffer + (long)m_Stride * start;
            m_Length = length;
            m_MinIndex = 0;
            m_MaxIndex = length - 1;
            m_Safety = array.m_Safety;
        }

        public static implicit operator NativeSlice<T>(NativeArray<T> array) => new NativeSlice<T>(array);

        public readonly int Length => m_Length;
        public readonly int Stride => m_Stride;

        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (CollectionChecks.Enabled)
                {
                    CheckIndex(index);
                    AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
                }
                return UnsafeUtility.ReadArrayElementWithStride<T>(m_Buffer, index, m_Stride);
            }
            [WriteAccessRequired]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (CollectionChecks.Enabled)
                {
                    CheckIndex(index);
                    AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
                }
                UnsafeUtility.WriteArrayElementWithStride(m_Buffer, index, m_Stride, value);
            }
        }

        readonly void CheckIndex(int index)
        {
            if ((uint)index >= (uint)m_Length)
                throw new IndexOutOfRangeException($"Index {index} is out of range of '{m_Length}' Length.");
            if (index < m_MinIndex || index > m_MaxIndex)
                throw new IndexOutOfRangeException($"Index {index} is out of restricted IJobParallelFor range [{m_MinIndex}...{m_MaxIndex}] in ReadWriteBuffer.");
        }

        /// <summary>The same memory as U elements (the stride must be the size of T).</summary>
        public NativeSlice<U> SliceConvert<U>() where U : struct
        {
            int sizeofU = UnsafeUtility.SizeOf<U>();
            if (m_Stride != UnsafeUtility.SizeOf<T>())
                throw new InvalidOperationException("SliceConvert requires that stride matches the size of the source type");
            if ((long)m_Stride * m_Length % sizeofU != 0)
                throw new InvalidOperationException("SliceConvert requires that Length * sizeof(T) is a multiple of sizeof(U).");
            int length = (int)((long)m_Stride * m_Length / sizeofU);
            return new NativeSlice<U> { m_Buffer = m_Buffer, m_Stride = sizeofU, m_Length = length, m_MinIndex = 0, m_MaxIndex = length - 1, m_Safety = m_Safety };
        }

        /// <summary>The U at byte `offset` of each element (a field of the elements).</summary>
        public NativeSlice<U> SliceWithStride<U>(int offset) where U : struct
        {
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset), "SliceWithStride offset must be >= 0");
            if (offset + UnsafeUtility.SizeOf<U>() > m_Stride)
                throw new ArgumentException("SliceWithStride sizeof(U) + offset must be <= than stride", nameof(offset));
            return new NativeSlice<U> { m_Buffer = m_Buffer + offset, m_Stride = m_Stride, m_Length = m_Length, m_MinIndex = 0, m_MaxIndex = m_Length - 1, m_Safety = m_Safety };
        }

        public NativeSlice<U> SliceWithStride<U>() where U : struct => SliceWithStride<U>(0);

        public void CopyFrom(NativeSlice<T> slice)
        {
            if (Length != slice.Length)
                throw new ArgumentException($"slice.Length ({slice.Length}) does not match the Length of this instance ({Length}).", nameof(slice));
            AtomicSafetyHandle.CheckReadAndThrow(slice.m_Safety);
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
            UnsafeUtility.MemCpyStride(m_Buffer, m_Stride, slice.m_Buffer, slice.m_Stride, UnsafeUtility.SizeOf<T>(), m_Length);
        }

        public void CopyFrom(T[] array)
        {
            if (Length != array.Length)
                throw new ArgumentException($"array.Length ({array.Length}) does not match the Length of this instance ({Length}).", nameof(array));
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
            for (int i = 0; i < array.Length; i++)
                UnsafeUtility.WriteArrayElementWithStride(m_Buffer, i, m_Stride, array[i]);
        }

        public readonly void CopyTo(NativeArray<T> array)
        {
            if (Length != array.Length)
                throw new ArgumentException($"array.Length ({array.Length}) does not match the Length of this instance ({Length}).", nameof(array));
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
            AtomicSafetyHandle.CheckWriteAndThrow(array.m_Safety);
            int size = UnsafeUtility.SizeOf<T>();
            UnsafeUtility.MemCpyStride(array.m_Buffer, size, m_Buffer, m_Stride, size, m_Length);
        }

        public readonly void CopyTo(T[] array)
        {
            if (Length != array.Length)
                throw new ArgumentException($"array.Length ({array.Length}) does not match the Length of this instance ({Length}).", nameof(array));
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
            for (int i = 0; i < m_Length; i++)
                array[i] = UnsafeUtility.ReadArrayElementWithStride<T>(m_Buffer, i, m_Stride);
        }

        public readonly T[] ToArray()
        {
            var array = new T[m_Length];
            CopyTo(array);
            return array;
        }

        public Enumerator GetEnumerator() => new Enumerator(ref this);
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool Equals(NativeSlice<T> other) => m_Buffer == other.m_Buffer && m_Stride == other.m_Stride && m_Length == other.m_Length;
        public override bool Equals(object obj) => obj is NativeSlice<T> other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((long)m_Buffer, m_Stride, m_Length);
        public static bool operator ==(NativeSlice<T> left, NativeSlice<T> right) => left.Equals(right);
        public static bool operator !=(NativeSlice<T> left, NativeSlice<T> right) => !left.Equals(right);

        public struct Enumerator : IEnumerator<T>
        {
            NativeSlice<T> m_Slice;
            int m_Index;

            public Enumerator(ref NativeSlice<T> slice)
            {
                m_Slice = slice;
                m_Index = -1;
            }

            public void Dispose() { }
            public bool MoveNext() => ++m_Index < m_Slice.Length;
            public void Reset() => m_Index = -1;
            public T Current => m_Slice[m_Index];
            object IEnumerator.Current => Current;
        }
    }

    public static class NativeSliceExtensions
    {
        public static NativeSlice<T> Slice<T>(this NativeArray<T> thisArray) where T : struct => new NativeSlice<T>(thisArray);
        public static NativeSlice<T> Slice<T>(this NativeArray<T> thisArray, int start) where T : struct => new NativeSlice<T>(thisArray, start);
        public static NativeSlice<T> Slice<T>(this NativeArray<T> thisArray, int start, int length) where T : struct => new NativeSlice<T>(thisArray, start, length);
        public static NativeSlice<T> Slice<T>(this NativeSlice<T> thisSlice) where T : struct => thisSlice;
        public static NativeSlice<T> Slice<T>(this NativeSlice<T> thisSlice, int start) where T : struct => new NativeSlice<T>(thisSlice, start);
        public static NativeSlice<T> Slice<T>(this NativeSlice<T> thisSlice, int start, int length) where T : struct => new NativeSlice<T>(thisSlice, start, length);
    }
}
