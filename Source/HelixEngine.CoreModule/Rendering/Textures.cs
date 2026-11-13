using System;
using System.Runtime.InteropServices;
using Helix.Collections;
using Helix.Collections.LowLevel.Unsafe;

namespace HelixEngine
{
    public partial class Texture
    {
        /// <summary>A mip count that asks for the whole chain.</summary>
        public static readonly int GenerateAllMips = -1;
    }

    public partial class Texture2D
    {
        /// <summary>Raw pixel bytes from unmanaged memory.</summary>
        public void LoadRawTextureData(IntPtr data, int size)
        {
            if (data == IntPtr.Zero || size <= 0)
                throw new ArgumentException("LoadRawTextureData: No texture data provided");
            var bytes = new byte[size];
            Marshal.Copy(data, bytes, 0, size);
            LoadRawTextureData(bytes);
        }

        public void LoadRawTextureData<T>(NativeArray<T> data) where T : struct
        {
            if (data.Length == 0)
                throw new ArgumentException("LoadRawTextureData: No texture data provided");
            LoadRawTextureData(data.Reinterpret<byte>(UnsafeUtility.SizeOf<T>()));
        }

        /// <summary>The pixels of a mip as T where they are (writes change the texture; Apply uploads them).</summary>
        public unsafe NativeArray<T> GetPixelData<T>(int mipLevel) where T : struct =>
            View<T>(PixelDataAddress(mipLevel), PixelDataSize(mipLevel), $"mip {mipLevel}");

        /// <summary>The bytes of every mip, one after the other, as T where they are.</summary>
        public unsafe NativeArray<T> GetRawTextureData<T>() where T : struct =>
            View<T>(RawTextureDataAddress(), RawTextureDataSize(), "the raw data");

        unsafe NativeArray<T> View<T>(ulong address, int bytes, string what) where T : struct
        {
            if (address == 0)
                throw new ArgumentException($"Texture '{name}' has no pixel data for {what} (it is not readable or the mip does not exist)");
            int size = UnsafeUtility.SizeOf<T>();
            if (bytes % size != 0)
                throw new ArgumentException($"Texture '{name}' {what} size ({bytes}) is not a multiple of type {typeof(T)} size ({size})");
            return NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>((void*)address, bytes / size, Allocator.None);
        }

        public void SetPixelData<T>(T[] data, int mipLevel, int sourceDataStartIndex = 0) where T : struct
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (sourceDataStartIndex < 0 || sourceDataStartIndex > data.Length)
                throw new ArgumentOutOfRangeException(nameof(sourceDataStartIndex), "sourceDataStartIndex must be within the data");
            var bytes = MemoryMarshal.AsBytes(data.AsSpan(sourceDataStartIndex)).ToArray();
            var view = new NativeArray<byte>(bytes, Allocator.Temp);
            SetPixelDataRaw(view, mipLevel);
            view.Dispose();
        }

        public void SetPixelData<T>(NativeArray<T> data, int mipLevel, int sourceDataStartIndex = 0) where T : struct
        {
            if (sourceDataStartIndex < 0 || sourceDataStartIndex > data.Length)
                throw new ArgumentOutOfRangeException(nameof(sourceDataStartIndex), "sourceDataStartIndex must be within the data");
            int size = UnsafeUtility.SizeOf<T>();
            SetPixelDataRaw(data.GetSubArray(sourceDataStartIndex, data.Length - sourceDataStartIndex).Reinterpret<byte>(size), mipLevel);
        }
    }
}
