using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SysUnsafe = System.Runtime.CompilerServices.Unsafe;
using HelixEngine.Internal;

namespace Helix.Collections.LowLevel.Unsafe
{
    /// <summary>Native memory and raw access for containers.</summary>
    public static unsafe class UnsafeUtility
    {
        public static void* Malloc(long size, int alignment, Allocator allocator)
        {
            if (!IsValidAllocator(allocator) || allocator == Allocator.None)
                throw new ArgumentException("Allocator " + allocator + " is not a valid allocator to allocate memory with.", nameof(allocator));
            return (void*)JobsNative.Allocate(size, alignment, (int)allocator);
        }

        public static void Free(void* memory, Allocator allocator)
        {
            if (allocator == Allocator.None || allocator == Allocator.Invalid)
                return;
            JobsNative.Free((ulong)memory, (int)allocator);
        }

        public static bool IsValidAllocator(Allocator allocator) => allocator > Allocator.None;

        public static void MemCpy(void* destination, void* source, long size) => Buffer.MemoryCopy(source, destination, size, size);
        public static void MemMove(void* destination, void* source, long size) => Buffer.MemoryCopy(source, destination, size, size);

        public static void MemCpyStride(void* destination, int destinationStride, void* source, int sourceStride, int elementSize, int count)
        {
            for (int i = 0; i < count; i++)
                Buffer.MemoryCopy((byte*)source + (long)i * sourceStride, (byte*)destination + (long)i * destinationStride, elementSize, elementSize);
        }

        public static void MemCpyReplicate(void* destination, void* source, int size, int count)
        {
            for (int i = 0; i < count; i++)
                Buffer.MemoryCopy(source, (byte*)destination + (long)i * size, size, size);
        }

        public static void MemClear(void* destination, long size) => NativeMemory.Clear(destination, (nuint)size);
        public static void MemSet(void* destination, byte value, long size) => NativeMemory.Fill(destination, (nuint)size, value);

        public static int MemCmp(void* ptr1, void* ptr2, long size)
        {
            var a = new ReadOnlySpan<byte>(ptr1, checked((int)size));
            var b = new ReadOnlySpan<byte>(ptr2, checked((int)size));
            return a.SequenceCompareTo(b);
        }

        public static int SizeOf<T>() where T : struct => SysUnsafe.SizeOf<T>();
        public static int SizeOf(Type type) => Marshal.SizeOf(type);

        // The alignment of T: the offset of a T after a byte.
        public static int AlignOf<T>() where T : struct => SysUnsafe.SizeOf<AlignHelper<T>>() - SysUnsafe.SizeOf<T>();

        [StructLayout(LayoutKind.Sequential)]
        struct AlignHelper<T> where T : struct
        {
            byte dummy;
            T data;
        }

        public static bool IsBlittable<T>() where T : struct => !RuntimeHelpers.IsReferenceOrContainsReferences<T>();
        public static bool IsBlittable(Type type) => type.IsValueType && !(bool)typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.IsReferenceOrContainsReferences)).MakeGenericMethod(type).Invoke(null, null);
        public static bool IsUnmanaged<T>() => !RuntimeHelpers.IsReferenceOrContainsReferences<T>();
        public static bool IsUnmanaged(Type type) => type.IsValueType && IsBlittable(type);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ReadArrayElement<T>(void* source, int index) => SysUnsafe.Read<T>((byte*)source + (long)index * SysUnsafe.SizeOf<T>());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ReadArrayElementWithStride<T>(void* source, int index, int stride) => SysUnsafe.Read<T>((byte*)source + (long)index * stride);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteArrayElement<T>(void* destination, int index, T value) => SysUnsafe.Write((byte*)destination + (long)index * SysUnsafe.SizeOf<T>(), value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteArrayElementWithStride<T>(void* destination, int index, int stride, T value) => SysUnsafe.Write((byte*)destination + (long)index * stride, value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T ArrayElementAsRef<T>(void* ptr, int index) where T : struct => ref SysUnsafe.AsRef<T>((byte*)ptr + (long)index * SysUnsafe.SizeOf<T>());

        public static void* AddressOf<T>(ref T output) where T : struct => SysUnsafe.AsPointer(ref output);
        public static ref T AsRef<T>(void* ptr) where T : struct => ref SysUnsafe.AsRef<T>(ptr);
        public static ref U As<T, U>(ref T from) => ref SysUnsafe.As<T, U>(ref from);
        public static void CopyPtrToStructure<T>(void* ptr, out T output) where T : struct => output = SysUnsafe.Read<T>(ptr);
        public static void CopyStructureToPtr<T>(ref T input, void* ptr) where T : struct => SysUnsafe.Write(ptr, input);
        public static int EnumToInt<T>(T enumValue) where T : struct, IConvertible => Convert.ToInt32(enumValue);

        public static void* PinGCObjectAndGetAddress(object target, out ulong gcHandle)
        {
            GCHandle h = GCHandle.Alloc(target, GCHandleType.Pinned);
            gcHandle = (ulong)GCHandle.ToIntPtr(h);
            return (void*)h.AddrOfPinnedObject();
        }

        public static void* PinGCArrayAndGetDataAddress(Array target, out ulong gcHandle) => PinGCObjectAndGetAddress(target, out gcHandle);
        public static void ReleaseGCObject(ulong gcHandle) => GCHandle.FromIntPtr((IntPtr)gcHandle).Free();
    }

    /// <summary>Native arrays over memory and their raw parts.</summary>
    public static unsafe class NativeArrayUnsafeUtility
    {
        /// <summary>A NativeArray over memory it does not own (Allocator.None: not freed by Dispose).</summary>
        public static NativeArray<T> ConvertExistingDataToNativeArray<T>(void* dataPointer, int length, Allocator allocator) where T : struct
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "Length must be >= 0");
            NativeArray<T>.CheckElementType();
            return new NativeArray<T> { m_Buffer = dataPointer, m_Length = length, m_AllocatorLabel = allocator, m_MinIndex = 0, m_MaxIndex = length - 1 };
        }

        public static void* GetUnsafePtr<T>(this NativeArray<T> nativeArray) where T : struct
        {
            AtomicSafetyHandle.CheckWriteAndThrow(nativeArray.m_Safety);
            return nativeArray.m_Buffer;
        }

        public static void* GetUnsafeReadOnlyPtr<T>(this NativeArray<T> nativeArray) where T : struct
        {
            AtomicSafetyHandle.CheckReadAndThrow(nativeArray.m_Safety);
            return nativeArray.m_Buffer;
        }

        public static void* GetUnsafeReadOnlyPtr<T>(this NativeArray<T>.ReadOnly nativeArray) where T : struct
        {
            AtomicSafetyHandle.CheckReadAndThrow(nativeArray.m_Safety);
            return nativeArray.m_Buffer;
        }

        public static void* GetUnsafeBufferPointerWithoutChecks<T>(NativeArray<T> nativeArray) where T : struct => nativeArray.m_Buffer;
        public static AtomicSafetyHandle GetAtomicSafetyHandle<T>(NativeArray<T> array) where T : struct => array.m_Safety;
        public static void SetAtomicSafetyHandle<T>(ref NativeArray<T> array, AtomicSafetyHandle safety) where T : struct => array.m_Safety = safety;
    }

    public static unsafe class NativeSliceUnsafeUtility
    {
        public static NativeSlice<T> ConvertExistingDataToNativeSlice<T>(void* dataPointer, int stride, int length) where T : struct
        {
            if (length < 0)
                throw new ArgumentException("Invalid length of " + length + ". It must be greater than 0.", nameof(length));
            if (stride < 0)
                throw new ArgumentException("Invalid stride of " + stride + ". It must be greater than 0.", nameof(stride));
            return new NativeSlice<T> { m_Buffer = (byte*)dataPointer, m_Stride = stride, m_Length = length, m_MinIndex = 0, m_MaxIndex = length - 1 };
        }

        public static void* GetUnsafePtr<T>(this NativeSlice<T> nativeSlice) where T : struct
        {
            AtomicSafetyHandle.CheckWriteAndThrow(nativeSlice.m_Safety);
            return nativeSlice.m_Buffer;
        }

        public static void* GetUnsafeReadOnlyPtr<T>(this NativeSlice<T> nativeSlice) where T : struct
        {
            AtomicSafetyHandle.CheckReadAndThrow(nativeSlice.m_Safety);
            return nativeSlice.m_Buffer;
        }

        public static AtomicSafetyHandle GetAtomicSafetyHandle<T>(NativeSlice<T> slice) where T : struct => slice.m_Safety;
        public static void SetAtomicSafetyHandle<T>(ref NativeSlice<T> slice, AtomicSafetyHandle safety) where T : struct => slice.m_Safety = safety;
    }
}
