using System;
using Helix.Interop;
using HelixEngine.Experimental.Rendering;
using HelixEngine.Internal;
using HelixEngine.Rendering;

namespace HelixEngine
{
    /// <summary>What a render texture is made of (the engine takes it as RenderTextureDescriptorData).</summary>
    public struct RenderTextureDescriptor
    {
        int m_Width, m_Height, m_MsaaSamples, m_VolumeDepth, m_MipCount;
        GraphicsFormat m_GraphicsFormat, m_DepthStencilFormat, m_StencilFormat;
        TextureDimension m_Dimension;
        ShadowSamplingMode m_ShadowSamplingMode;
        VRTextureUsage m_VrUsage;
        RenderTextureMemoryless m_Memoryless;
        int m_Flags;

        const int SRgbFlag = 1, MipMapFlag = 2, GenerateMipsFlag = 4, RandomWriteFlag = 8, BindMSFlag = 16, DynamicScaleFlag = 32;

        public RenderTextureDescriptor(int width, int height) : this(width, height, RenderTextureFormat.Default, 0) { }

        public RenderTextureDescriptor(int width, int height, RenderTextureFormat colorFormat) : this(width, height, colorFormat, 0) { }

        public RenderTextureDescriptor(int width, int height, RenderTextureFormat colorFormat, int depthBufferBits) : this(width, height, colorFormat, depthBufferBits, Texture.GenerateAllMips) { }

        public RenderTextureDescriptor(int width, int height, RenderTextureFormat colorFormat, int depthBufferBits, int mipCount) : this()
        {
            Init(width, height, mipCount);
            this.colorFormat = colorFormat;
            this.depthBufferBits = depthBufferBits;
        }

        public RenderTextureDescriptor(int width, int height, GraphicsFormat colorFormat, int depthBufferBits) : this(width, height, colorFormat, depthBufferBits, Texture.GenerateAllMips) { }

        public RenderTextureDescriptor(int width, int height, GraphicsFormat colorFormat, int depthBufferBits, int mipCount) : this()
        {
            Init(width, height, mipCount);
            graphicsFormat = colorFormat;
            this.depthBufferBits = depthBufferBits;
        }

        public RenderTextureDescriptor(int width, int height, GraphicsFormat colorFormat, GraphicsFormat depthStencilFormat) : this()
        {
            Init(width, height, Texture.GenerateAllMips);
            graphicsFormat = colorFormat;
            this.depthStencilFormat = depthStencilFormat;
        }

        void Init(int width, int height, int mipCount)
        {
            m_Width = width;
            m_Height = height;
            m_MsaaSamples = 1;
            m_VolumeDepth = 1;
            m_MipCount = mipCount;
            m_Dimension = TextureDimension.Tex2D;
            m_ShadowSamplingMode = ShadowSamplingMode.None;
            m_Flags = GenerateMipsFlag;
        }

        bool Flag(int f) => (m_Flags & f) != 0;
        void SetFlag(int f, bool on) => m_Flags = on ? m_Flags | f : m_Flags & ~f;

        public int width { get => m_Width; set => m_Width = value; }
        public int height { get => m_Height; set => m_Height = value; }
        public int msaaSamples { get => m_MsaaSamples; set => m_MsaaSamples = value; }
        public int volumeDepth { get => m_VolumeDepth; set => m_VolumeDepth = value; }
        public int mipCount { get => m_MipCount; set => m_MipCount = value; }
        public GraphicsFormat graphicsFormat
        {
            get => m_GraphicsFormat;
            set
            {
                m_GraphicsFormat = value;
                SetFlag(SRgbFlag, GraphicsFormatUtility.IsSRGBFormat(value));
            }
        }
        public GraphicsFormat depthStencilFormat { get => m_DepthStencilFormat; set => m_DepthStencilFormat = value; }
        public GraphicsFormat stencilFormat { get => m_StencilFormat; set => m_StencilFormat = value; }
        public int depthBufferBits
        {
            get => m_DepthStencilFormat switch
            {
                GraphicsFormat.D16_UNorm => 16,
                GraphicsFormat.D24_UNorm or GraphicsFormat.D24_UNorm_S8_UInt => 24,
                GraphicsFormat.D32_SFloat or GraphicsFormat.D32_SFloat_S8_UInt => 32,
                _ => 0,
            };
            set => m_DepthStencilFormat = GraphicsFormatUtility.GetDepthStencilFormat(value);
        }
        public RenderTextureFormat colorFormat
        {
            get => GraphicsFormatUtility.GetRenderTextureFormat(m_GraphicsFormat);
            set
            {
                if (value == RenderTextureFormat.Depth || value == RenderTextureFormat.Shadowmap)
                {
                    m_GraphicsFormat = GraphicsFormat.None;
                    if (m_DepthStencilFormat == GraphicsFormat.None)
                        m_DepthStencilFormat = GraphicsFormat.D24_UNorm_S8_UInt;
                    m_ShadowSamplingMode = value == RenderTextureFormat.Shadowmap ? ShadowSamplingMode.CompareDepths : ShadowSamplingMode.None;
                    return;
                }
                m_GraphicsFormat = GraphicsFormatUtility.GetGraphicsFormat(value, sRGB);
            }
        }
        public bool sRGB { get => Flag(SRgbFlag); set => SetFlag(SRgbFlag, value); }
        public TextureDimension dimension { get => m_Dimension; set => m_Dimension = value; }
        public ShadowSamplingMode shadowSamplingMode { get => m_ShadowSamplingMode; set => m_ShadowSamplingMode = value; }
        public VRTextureUsage vrUsage { get => m_VrUsage; set => m_VrUsage = value; }
        public RenderTextureMemoryless memoryless { get => m_Memoryless; set => m_Memoryless = value; }
        public bool useMipMap { get => Flag(MipMapFlag); set => SetFlag(MipMapFlag, value); }
        public bool autoGenerateMips { get => Flag(GenerateMipsFlag); set => SetFlag(GenerateMipsFlag, value); }
        public bool enableRandomWrite { get => Flag(RandomWriteFlag); set => SetFlag(RandomWriteFlag, value); }
        public bool bindMS { get => Flag(BindMSFlag); set => SetFlag(BindMSFlag, value); }
        public bool useDynamicScale { get => Flag(DynamicScaleFlag); set => SetFlag(DynamicScaleFlag, value); }

        internal RenderTextureDescriptorData Data => new RenderTextureDescriptorData
        {
            width = m_Width, height = m_Height, msaaSamples = m_MsaaSamples, volumeDepth = m_VolumeDepth, mipCount = m_MipCount,
            graphicsFormat = (int)m_GraphicsFormat, depthStencilFormat = (int)m_DepthStencilFormat, stencilFormat = (int)m_StencilFormat,
            dimension = (int)m_Dimension, shadowSamplingMode = (int)m_ShadowSamplingMode, vrUsage = (int)m_VrUsage, memoryless = (int)m_Memoryless,
            flags = m_Flags,
        };

        internal static RenderTextureDescriptor From(RenderTextureDescriptorData d)
        {
            var r = new RenderTextureDescriptor();
            r.m_Width = d.width;
            r.m_Height = d.height;
            r.m_MsaaSamples = d.msaaSamples;
            r.m_VolumeDepth = d.volumeDepth;
            r.m_MipCount = d.mipCount;
            r.m_GraphicsFormat = (GraphicsFormat)d.graphicsFormat;
            r.m_DepthStencilFormat = (GraphicsFormat)d.depthStencilFormat;
            r.m_StencilFormat = (GraphicsFormat)d.stencilFormat;
            r.m_Dimension = (TextureDimension)d.dimension;
            r.m_ShadowSamplingMode = (ShadowSamplingMode)d.shadowSamplingMode;
            r.m_VrUsage = (VRTextureUsage)d.vrUsage;
            r.m_Memoryless = (RenderTextureMemoryless)d.memoryless;
            r.m_Flags = d.flags;
            return r;
        }
    }

    /// <summary>A render texture's color or depth buffer.</summary>
    public struct RenderBuffer
    {
        internal RenderTexture m_Texture;
        internal bool m_Depth;

        internal RenderBuffer(RenderTexture texture, bool depth)
        {
            m_Texture = texture;
            m_Depth = depth;
        }

        public IntPtr GetNativeRenderBufferPtr() => IntPtr.Zero;
    }

    // The members of RenderTexture in the reference's shape (its width, height and dimension can be set).
    public partial class RenderTexture
    {
        public RenderTexture(RenderTextureDescriptor desc)
        {
            RenderTexture made = RenderTextureNative.Create(desc.Data);
            if (made == null)
                throw new ArgumentException("RenderTexture: the descriptor makes no render texture (its width and height must be larger than 0).");
            m_HxId = Objects.IdOf(made);
            Objects.Attach(this, m_HxId, typeof(RenderTexture));
        }

        public new int width { get => base.width; set => RenderTextureNative.SetWidth(this, value); }
        public new int height { get => base.height; set => RenderTextureNative.SetHeight(this, value); }
        public new TextureDimension dimension { get => base.dimension; set => RenderTextureNative.SetDimension(this, value); }

        public RenderTextureDescriptor descriptor
        {
            get => RenderTextureDescriptor.From(RenderTextureNative.Descriptor(this));
            set => RenderTextureNative.SetDescriptor(this, value.Data);
        }

        public RenderBuffer colorBuffer => new RenderBuffer(this, false);
        public RenderBuffer depthBuffer => new RenderBuffer(this, true);

        public static RenderTexture GetTemporary(RenderTextureDescriptor desc) => RenderTextureNative.GetTemporary(desc.Data);
    }

    public static partial class Graphics
    {
        public static void Blit(Texture source, RenderTexture dest) => RenderPipelineNative.GraphicsBlit(source, dest, null, -1, new Vector4(1, 1, 0, 0));
        public static void Blit(Texture source, RenderTexture dest, Material mat, int pass = -1) =>
            RenderPipelineNative.GraphicsBlit(source, dest, mat, pass, new Vector4(1, 1, 0, 0));
        public static void Blit(Texture source, RenderTexture dest, Vector2 scale, Vector2 offset) =>
            RenderPipelineNative.GraphicsBlit(source, dest, null, -1, new Vector4(scale.x, scale.y, offset.x, offset.y));
        public static void Blit(Texture source, Material mat, int pass = -1) => RenderPipelineNative.GraphicsBlit(source, RenderTexture.active, mat, pass, new Vector4(1, 1, 0, 0));

        public static bool ConvertTexture(Texture src, Texture dst) => RenderPipelineNative.GraphicsConvertTexture(src, 0, dst, 0);
        public static bool ConvertTexture(Texture src, int srcElement, Texture dst, int dstElement) =>
            RenderPipelineNative.GraphicsConvertTexture(src, srcElement, dst, dstElement);
        public static void CopyTexture(Texture src, Texture dst) => RenderPipelineNative.GraphicsCopyTexture(src, -1, -1, dst, -1, -1);
        public static void CopyTexture(Texture src, int srcElement, Texture dst, int dstElement) =>
            RenderPipelineNative.GraphicsCopyTexture(src, srcElement, 0, dst, dstElement, 0);
        public static void CopyTexture(Texture src, int srcElement, int srcMip, Texture dst, int dstElement, int dstMip) =>
            RenderPipelineNative.GraphicsCopyTexture(src, srcElement, srcMip, dst, dstElement, dstMip);

        public static void SetRenderTarget(RenderTexture rt) => RenderTexture.active = rt;
        public static void SetRenderTarget(RenderBuffer colorBuffer, RenderBuffer depthBuffer) => RenderTexture.active = colorBuffer.m_Texture ?? depthBuffer.m_Texture;
        public static RenderBuffer activeColorBuffer => new RenderBuffer(RenderTexture.active, false);
        public static RenderBuffer activeDepthBuffer => new RenderBuffer(RenderTexture.active, true);
    }
}

namespace HelixEngine.Rendering
{
    /// <summary>A target of command buffers: a built in target, a temporary render texture by its id, or a texture; a mip, a cube face and a slice.</summary>
    public struct RenderTargetIdentifier : IEquatable<RenderTargetIdentifier>
    {
        BuiltinRenderTextureType m_Type;
        int m_NameID;
        ulong m_Texture;
        int m_MipLevel;
        CubemapFace m_CubeFace;
        int m_DepthSlice;

        public const int AllDepthSlices = -1;

        public RenderTargetIdentifier(BuiltinRenderTextureType type) : this(type, 0, CubemapFace.Unknown, 0) { }

        public RenderTargetIdentifier(BuiltinRenderTextureType type, int mipLevel = 0, CubemapFace cubeFace = CubemapFace.Unknown, int depthSlice = 0)
        {
            m_Type = type;
            m_NameID = -1;
            m_Texture = 0;
            m_MipLevel = mipLevel;
            m_CubeFace = cubeFace;
            m_DepthSlice = depthSlice;
        }

        public RenderTargetIdentifier(string name) : this(Shader.PropertyToID(name)) { }

        public RenderTargetIdentifier(string name, int mipLevel = 0, CubemapFace cubeFace = CubemapFace.Unknown, int depthSlice = 0)
            : this(Shader.PropertyToID(name), mipLevel, cubeFace, depthSlice) { }

        public RenderTargetIdentifier(int nameID) : this(nameID, 0, CubemapFace.Unknown, 0) { }

        public RenderTargetIdentifier(int nameID, int mipLevel = 0, CubemapFace cubeFace = CubemapFace.Unknown, int depthSlice = 0)
            : this(BuiltinRenderTextureType.PropertyName, mipLevel, cubeFace, depthSlice)
        {
            m_NameID = nameID;
        }

        public RenderTargetIdentifier(RenderTargetIdentifier renderTargetIdentifier, int mipLevel, CubemapFace cubemapFace = CubemapFace.Unknown, int depthSlice = 0)
        {
            this = renderTargetIdentifier;
            m_MipLevel = mipLevel;
            m_CubeFace = cubemapFace;
            m_DepthSlice = depthSlice;
        }

        public RenderTargetIdentifier(Texture tex) : this(tex, 0, CubemapFace.Unknown, 0) { }

        public RenderTargetIdentifier(Texture tex, int mipLevel = 0, CubemapFace cubeFace = CubemapFace.Unknown, int depthSlice = 0)
            : this(BuiltinRenderTextureType.None, mipLevel, cubeFace, depthSlice)
        {
            if (tex == null)
                return;
            m_Type = tex is RenderTexture ? BuiltinRenderTextureType.RenderTexture : BuiltinRenderTextureType.BindableTexture;
            m_Texture = Objects.IdOf(tex);
        }

        public RenderTargetIdentifier(RenderBuffer buf, int mipLevel = 0, CubemapFace cubeFace = CubemapFace.Unknown, int depthSlice = 0)
            : this(buf.m_Texture, mipLevel, cubeFace, depthSlice) { }

        public static implicit operator RenderTargetIdentifier(BuiltinRenderTextureType type) => new RenderTargetIdentifier(type);
        public static implicit operator RenderTargetIdentifier(string name) => new RenderTargetIdentifier(name);
        public static implicit operator RenderTargetIdentifier(int nameID) => new RenderTargetIdentifier(nameID);
        public static implicit operator RenderTargetIdentifier(Texture tex) => new RenderTargetIdentifier(tex);
        public static implicit operator RenderTargetIdentifier(RenderBuffer buf) => new RenderTargetIdentifier(buf);

        internal RenderTargetData Data => new RenderTargetData
        {
            type = (int)m_Type, nameID = m_NameID, texture = m_Texture, mipLevel = m_MipLevel, face = (int)m_CubeFace, depthSlice = m_DepthSlice,
        };

        public override string ToString() => $"Type {m_Type} NameID {m_NameID} InstanceID {m_Texture}";
        public bool Equals(RenderTargetIdentifier rhs) =>
            m_Type == rhs.m_Type && m_NameID == rhs.m_NameID && m_Texture == rhs.m_Texture && m_MipLevel == rhs.m_MipLevel && m_CubeFace == rhs.m_CubeFace &&
            m_DepthSlice == rhs.m_DepthSlice;
        public override bool Equals(object obj) => obj is RenderTargetIdentifier r && Equals(r);
        public override int GetHashCode() => HashCode.Combine((int)m_Type, m_NameID, m_Texture, m_MipLevel, (int)m_CubeFace, m_DepthSlice);
        public static bool operator ==(RenderTargetIdentifier lhs, RenderTargetIdentifier rhs) => lhs.Equals(rhs);
        public static bool operator !=(RenderTargetIdentifier lhs, RenderTargetIdentifier rhs) => !lhs.Equals(rhs);
    }
}
