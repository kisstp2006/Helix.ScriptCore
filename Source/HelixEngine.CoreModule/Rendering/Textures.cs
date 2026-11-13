using System;
using System.Runtime.InteropServices;

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
    }
}
