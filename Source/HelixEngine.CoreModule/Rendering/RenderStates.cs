using System;
using Helix.Collections;
using Helix.Collections.LowLevel.Unsafe;
using HelixEngine.Internal;

namespace HelixEngine.Rendering
{
    // The values of the scriptable pipeline API, as the reference has them; the engine takes them as one plain struct
    // (DrawRendererData) when DrawRenderers is called.

    public struct RenderQueueRange : IEquatable<RenderQueueRange>
    {
        int m_LowerBound;
        int m_UpperBound;

        public static readonly int minimumBound = 0;
        public static readonly int maximumBound = 5000;

        public RenderQueueRange(int lowerBound, int upperBound)
        {
            if (lowerBound < minimumBound || lowerBound > maximumBound)
                throw new ArgumentOutOfRangeException(nameof(lowerBound), lowerBound, $"The lower bound must be at least {minimumBound} and at most {maximumBound}.");
            if (upperBound < minimumBound || upperBound > maximumBound)
                throw new ArgumentOutOfRangeException(nameof(upperBound), upperBound, $"The upper bound must be at least {minimumBound} and at most {maximumBound}.");
            m_LowerBound = lowerBound;
            m_UpperBound = upperBound;
        }

        public static RenderQueueRange all => new RenderQueueRange(minimumBound, maximumBound);
        public static RenderQueueRange opaque => new RenderQueueRange(minimumBound, 2500);
        public static RenderQueueRange transparent => new RenderQueueRange(2501, maximumBound);

        public int lowerBound { get => m_LowerBound; set => m_LowerBound = value; }
        public int upperBound { get => m_UpperBound; set => m_UpperBound = value; }

        public bool Equals(RenderQueueRange other) => m_LowerBound == other.m_LowerBound && m_UpperBound == other.m_UpperBound;
        public override bool Equals(object obj) => obj is RenderQueueRange other && Equals(other);
        public override int GetHashCode() => (m_LowerBound * 397) ^ m_UpperBound;
        public static bool operator ==(RenderQueueRange left, RenderQueueRange right) => left.Equals(right);
        public static bool operator !=(RenderQueueRange left, RenderQueueRange right) => !left.Equals(right);
        public override string ToString() => $"({m_LowerBound}, {m_UpperBound})";
    }

    public struct SortingSettings : IEquatable<SortingSettings>
    {
        Matrix4x4 m_WorldToCameraMatrix;
        Vector3 m_CameraPosition;
        Vector3 m_CustomAxis;
        SortingCriteria m_Criteria;
        DistanceMetric m_DistanceMetric;

        /// <summary>The camera's matrix, position and distance metric (orthographic cameras sort along their view direction).</summary>
        public SortingSettings(Camera camera)
        {
            if (camera == null)
                throw new ArgumentNullException(nameof(camera));
            m_WorldToCameraMatrix = camera.worldToCameraMatrix;
            m_CameraPosition = camera.transform.position;
            m_CustomAxis = default;
            m_Criteria = SortingCriteria.None;
            m_DistanceMetric = GraphicsSettings.transparencySortMode switch
            {
                TransparencySortMode.Perspective => DistanceMetric.Perspective,
                TransparencySortMode.Orthographic => DistanceMetric.Orthographic,
                TransparencySortMode.CustomAxis => DistanceMetric.CustomAxis,
                _ => camera.orthographic ? DistanceMetric.Orthographic : DistanceMetric.Perspective,
            };
            if (m_DistanceMetric == DistanceMetric.CustomAxis)
                m_CustomAxis = GraphicsSettings.transparencySortAxis;
        }

        public Matrix4x4 worldToCameraMatrix { get => m_WorldToCameraMatrix; set => m_WorldToCameraMatrix = value; }
        public Vector3 cameraPosition { get => m_CameraPosition; set => m_CameraPosition = value; }
        public Vector3 customAxis { get => m_CustomAxis; set => m_CustomAxis = value; }
        public SortingCriteria criteria { get => m_Criteria; set => m_Criteria = value; }
        public DistanceMetric distanceMetric { get => m_DistanceMetric; set => m_DistanceMetric = value; }

        public bool Equals(SortingSettings other) =>
            m_WorldToCameraMatrix.Equals(other.m_WorldToCameraMatrix) && m_CameraPosition.Equals(other.m_CameraPosition) && m_CustomAxis.Equals(other.m_CustomAxis) &&
            m_Criteria == other.m_Criteria && m_DistanceMetric == other.m_DistanceMetric;
        public override bool Equals(object obj) => obj is SortingSettings other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(m_WorldToCameraMatrix, m_CameraPosition, m_CustomAxis, (int)m_Criteria, (int)m_DistanceMetric);
        public static bool operator ==(SortingSettings left, SortingSettings right) => left.Equals(right);
        public static bool operator !=(SortingSettings left, SortingSettings right) => !left.Equals(right);
    }

    public unsafe struct DrawingSettings : IEquatable<DrawingSettings>
    {
        SortingSettings m_SortingSettings;
        fixed int m_ShaderPassNames[16];
        PerObjectData m_PerObjectData;
        bool m_EnableDynamicBatching;
        bool m_EnableInstancing;
        Material m_OverrideMaterial;
        int m_OverrideMaterialPassIndex;
        Shader m_OverrideShader;
        int m_OverrideShaderPassIndex;
        Material m_FallbackMaterial;
        int m_MainLightIndex;

        public static readonly int maxShaderPasses = 16;

        public DrawingSettings(ShaderTagId shaderPassName, SortingSettings sortingSettings)
        {
            m_SortingSettings = sortingSettings;
            m_PerObjectData = PerObjectData.None;
            m_EnableDynamicBatching = false;
            m_EnableInstancing = true;
            m_OverrideMaterial = null;
            m_OverrideMaterialPassIndex = 0;
            m_OverrideShader = null;
            m_OverrideShaderPassIndex = 0;
            m_FallbackMaterial = null;
            m_MainLightIndex = -1;
            m_ShaderPassNames[0] = shaderPassName.Id;
            for (int i = 1; i < 16; i++)
                m_ShaderPassNames[i] = 0;
        }

        public SortingSettings sortingSettings { get => m_SortingSettings; set => m_SortingSettings = value; }
        public PerObjectData perObjectData { get => m_PerObjectData; set => m_PerObjectData = value; }
        public bool enableDynamicBatching { get => m_EnableDynamicBatching; set => m_EnableDynamicBatching = value; }
        public bool enableInstancing { get => m_EnableInstancing; set => m_EnableInstancing = value; }
        public Material overrideMaterial { get => m_OverrideMaterial; set => m_OverrideMaterial = value; }
        public int overrideMaterialPassIndex { get => m_OverrideMaterialPassIndex; set => m_OverrideMaterialPassIndex = value; }
        public Shader overrideShader { get => m_OverrideShader; set => m_OverrideShader = value; }
        public int overrideShaderPassIndex { get => m_OverrideShaderPassIndex; set => m_OverrideShaderPassIndex = value; }
        public Material fallbackMaterial { get => m_FallbackMaterial; set => m_FallbackMaterial = value; }
        public int mainLightIndex { get => m_MainLightIndex; set => m_MainLightIndex = value; }

        public ShaderTagId GetShaderPassName(int index)
        {
            if (index >= maxShaderPasses || index < 0)
                throw new ArgumentOutOfRangeException(nameof(index), $"Index should range from 0 to DrawSettings.maxShaderPasses ({maxShaderPasses}), was {index}");
            return ShaderTagId.FromId(m_ShaderPassNames[index]);
        }

        public void SetShaderPassName(int index, ShaderTagId shaderPassName)
        {
            if (index >= maxShaderPasses || index < 0)
                throw new ArgumentOutOfRangeException(nameof(index), $"Index should range from 0 to DrawSettings.maxShaderPasses ({maxShaderPasses}), was {index}");
            m_ShaderPassNames[index] = shaderPassName.Id;
        }

        internal ShaderTagId[] PassNames()
        {
            int count = 0;
            for (int i = 0; i < 16; i++)
                if (m_ShaderPassNames[i] != 0)
                    count = i + 1;
            var names = new ShaderTagId[count];
            for (int i = 0; i < count; i++)
                names[i] = ShaderTagId.FromId(m_ShaderPassNames[i]);
            return names;
        }

        public bool Equals(DrawingSettings other)
        {
            for (int i = 0; i < 16; i++)
                if (m_ShaderPassNames[i] != other.m_ShaderPassNames[i])
                    return false;
            return m_SortingSettings.Equals(other.m_SortingSettings) && m_PerObjectData == other.m_PerObjectData &&
                   m_EnableDynamicBatching == other.m_EnableDynamicBatching && m_EnableInstancing == other.m_EnableInstancing &&
                   ReferenceEquals(m_OverrideMaterial, other.m_OverrideMaterial) && m_OverrideMaterialPassIndex == other.m_OverrideMaterialPassIndex &&
                   ReferenceEquals(m_OverrideShader, other.m_OverrideShader) && m_OverrideShaderPassIndex == other.m_OverrideShaderPassIndex &&
                   ReferenceEquals(m_FallbackMaterial, other.m_FallbackMaterial) && m_MainLightIndex == other.m_MainLightIndex;
        }

        public override bool Equals(object obj) => obj is DrawingSettings other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(m_SortingSettings, m_ShaderPassNames[0], (int)m_PerObjectData, m_EnableInstancing, m_OverrideMaterialPassIndex, m_MainLightIndex);
        public static bool operator ==(DrawingSettings left, DrawingSettings right) => left.Equals(right);
        public static bool operator !=(DrawingSettings left, DrawingSettings right) => !left.Equals(right);
    }

    public struct FilteringSettings : IEquatable<FilteringSettings>
    {
        RenderQueueRange m_RenderQueueRange;
        int m_LayerMask;
        uint m_RenderingLayerMask;
        uint m_BatchLayerMask;
        int m_ExcludeMotionVectorObjects;

        public FilteringSettings(RenderQueueRange? renderQueueRange = null, int layerMask = -1, uint renderingLayerMask = uint.MaxValue,
                                 int excludeMotionVectorObjects = 0)
        {
            m_RenderQueueRange = renderQueueRange ?? RenderQueueRange.all;
            m_LayerMask = layerMask;
            m_RenderingLayerMask = renderingLayerMask;
            m_BatchLayerMask = uint.MaxValue;
            m_ExcludeMotionVectorObjects = excludeMotionVectorObjects;
        }

        public static FilteringSettings defaultValue => new FilteringSettings(RenderQueueRange.all);

        public RenderQueueRange renderQueueRange { get => m_RenderQueueRange; set => m_RenderQueueRange = value; }
        public int layerMask { get => m_LayerMask; set => m_LayerMask = value; }
        public uint renderingLayerMask { get => m_RenderingLayerMask; set => m_RenderingLayerMask = value; }
        public uint batchLayerMask { get => m_BatchLayerMask; set => m_BatchLayerMask = value; }
        public bool excludeMotionVectorObjects { get => m_ExcludeMotionVectorObjects != 0; set => m_ExcludeMotionVectorObjects = value ? 1 : 0; }

        public bool Equals(FilteringSettings other) =>
            m_RenderQueueRange.Equals(other.m_RenderQueueRange) && m_LayerMask == other.m_LayerMask && m_RenderingLayerMask == other.m_RenderingLayerMask &&
            m_BatchLayerMask == other.m_BatchLayerMask && m_ExcludeMotionVectorObjects == other.m_ExcludeMotionVectorObjects;
        public override bool Equals(object obj) => obj is FilteringSettings other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(m_RenderQueueRange, m_LayerMask, m_RenderingLayerMask, m_BatchLayerMask, m_ExcludeMotionVectorObjects);
        public static bool operator ==(FilteringSettings left, FilteringSettings right) => left.Equals(right);
        public static bool operator !=(FilteringSettings left, FilteringSettings right) => !left.Equals(right);
    }

    // ---------------------------------------------------------------------------------------------- render state

    public struct RenderTargetBlendState : IEquatable<RenderTargetBlendState>
    {
        byte m_WriteMask;
        byte m_SourceColorBlendMode;
        byte m_DestinationColorBlendMode;
        byte m_SourceAlphaBlendMode;
        byte m_DestinationAlphaBlendMode;
        byte m_ColorBlendOperation;
        byte m_AlphaBlendOperation;

        public RenderTargetBlendState(ColorWriteMask writeMask = ColorWriteMask.All, BlendMode sourceColorBlendMode = BlendMode.One,
                                      BlendMode destinationColorBlendMode = BlendMode.Zero, BlendMode sourceAlphaBlendMode = BlendMode.One,
                                      BlendMode destinationAlphaBlendMode = BlendMode.Zero, BlendOp colorBlendOperation = BlendOp.Add,
                                      BlendOp alphaBlendOperation = BlendOp.Add)
        {
            m_WriteMask = (byte)writeMask;
            m_SourceColorBlendMode = (byte)sourceColorBlendMode;
            m_DestinationColorBlendMode = (byte)destinationColorBlendMode;
            m_SourceAlphaBlendMode = (byte)sourceAlphaBlendMode;
            m_DestinationAlphaBlendMode = (byte)destinationAlphaBlendMode;
            m_ColorBlendOperation = (byte)colorBlendOperation;
            m_AlphaBlendOperation = (byte)alphaBlendOperation;
        }

        public static RenderTargetBlendState defaultValue => new RenderTargetBlendState(ColorWriteMask.All);

        public ColorWriteMask writeMask { get => (ColorWriteMask)m_WriteMask; set => m_WriteMask = (byte)value; }
        public BlendMode sourceColorBlendMode { get => (BlendMode)m_SourceColorBlendMode; set => m_SourceColorBlendMode = (byte)value; }
        public BlendMode destinationColorBlendMode { get => (BlendMode)m_DestinationColorBlendMode; set => m_DestinationColorBlendMode = (byte)value; }
        public BlendMode sourceAlphaBlendMode { get => (BlendMode)m_SourceAlphaBlendMode; set => m_SourceAlphaBlendMode = (byte)value; }
        public BlendMode destinationAlphaBlendMode { get => (BlendMode)m_DestinationAlphaBlendMode; set => m_DestinationAlphaBlendMode = (byte)value; }
        public BlendOp colorBlendOperation { get => (BlendOp)m_ColorBlendOperation; set => m_ColorBlendOperation = (byte)value; }
        public BlendOp alphaBlendOperation { get => (BlendOp)m_AlphaBlendOperation; set => m_AlphaBlendOperation = (byte)value; }

        public bool Equals(RenderTargetBlendState other) =>
            m_WriteMask == other.m_WriteMask && m_SourceColorBlendMode == other.m_SourceColorBlendMode &&
            m_DestinationColorBlendMode == other.m_DestinationColorBlendMode && m_SourceAlphaBlendMode == other.m_SourceAlphaBlendMode &&
            m_DestinationAlphaBlendMode == other.m_DestinationAlphaBlendMode && m_ColorBlendOperation == other.m_ColorBlendOperation &&
            m_AlphaBlendOperation == other.m_AlphaBlendOperation;
        public override bool Equals(object obj) => obj is RenderTargetBlendState other && Equals(other);
        public override int GetHashCode() =>
            HashCode.Combine(m_WriteMask, m_SourceColorBlendMode, m_DestinationColorBlendMode, m_SourceAlphaBlendMode, m_DestinationAlphaBlendMode,
                             m_ColorBlendOperation, m_AlphaBlendOperation);
        public static bool operator ==(RenderTargetBlendState left, RenderTargetBlendState right) => left.Equals(right);
        public static bool operator !=(RenderTargetBlendState left, RenderTargetBlendState right) => !left.Equals(right);
    }

    /// <summary>The first target's state draws; the others and alphaToMask are kept for the targets and devices of later steps.</summary>
    public struct BlendState : IEquatable<BlendState>
    {
        RenderTargetBlendState m_BlendState0, m_BlendState1, m_BlendState2, m_BlendState3, m_BlendState4, m_BlendState5, m_BlendState6, m_BlendState7;
        byte m_SeparateMRTBlendStates;
        byte m_AlphaToMask;

        public BlendState(bool separateMRTBlend = false, bool alphaToMask = false)
        {
            m_BlendState0 = m_BlendState1 = m_BlendState2 = m_BlendState3 = RenderTargetBlendState.defaultValue;
            m_BlendState4 = m_BlendState5 = m_BlendState6 = m_BlendState7 = RenderTargetBlendState.defaultValue;
            m_SeparateMRTBlendStates = (byte)(separateMRTBlend ? 1 : 0);
            m_AlphaToMask = (byte)(alphaToMask ? 1 : 0);
        }

        public static BlendState defaultValue => new BlendState(false, false);

        public bool separateMRTBlendStates { get => m_SeparateMRTBlendStates != 0; set => m_SeparateMRTBlendStates = (byte)(value ? 1 : 0); }
        public bool alphaToMask { get => m_AlphaToMask != 0; set => m_AlphaToMask = (byte)(value ? 1 : 0); }
        public RenderTargetBlendState blendState0 { get => m_BlendState0; set => m_BlendState0 = value; }
        public RenderTargetBlendState blendState1 { get => m_BlendState1; set => m_BlendState1 = value; }
        public RenderTargetBlendState blendState2 { get => m_BlendState2; set => m_BlendState2 = value; }
        public RenderTargetBlendState blendState3 { get => m_BlendState3; set => m_BlendState3 = value; }
        public RenderTargetBlendState blendState4 { get => m_BlendState4; set => m_BlendState4 = value; }
        public RenderTargetBlendState blendState5 { get => m_BlendState5; set => m_BlendState5 = value; }
        public RenderTargetBlendState blendState6 { get => m_BlendState6; set => m_BlendState6 = value; }
        public RenderTargetBlendState blendState7 { get => m_BlendState7; set => m_BlendState7 = value; }

        public bool Equals(BlendState other) =>
            m_BlendState0.Equals(other.m_BlendState0) && m_BlendState1.Equals(other.m_BlendState1) && m_BlendState2.Equals(other.m_BlendState2) &&
            m_BlendState3.Equals(other.m_BlendState3) && m_BlendState4.Equals(other.m_BlendState4) && m_BlendState5.Equals(other.m_BlendState5) &&
            m_BlendState6.Equals(other.m_BlendState6) && m_BlendState7.Equals(other.m_BlendState7) && m_SeparateMRTBlendStates == other.m_SeparateMRTBlendStates &&
            m_AlphaToMask == other.m_AlphaToMask;
        public override bool Equals(object obj) => obj is BlendState other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(m_BlendState0, m_BlendState1, m_SeparateMRTBlendStates, m_AlphaToMask);
        public static bool operator ==(BlendState left, BlendState right) => left.Equals(right);
        public static bool operator !=(BlendState left, BlendState right) => !left.Equals(right);
    }

    public struct DepthState : IEquatable<DepthState>
    {
        byte m_WriteEnabled;
        sbyte m_CompareFunction;

        public DepthState(bool writeEnabled = true, CompareFunction compareFunction = CompareFunction.Less)
        {
            m_WriteEnabled = (byte)(writeEnabled ? 1 : 0);
            m_CompareFunction = (sbyte)compareFunction;
        }

        public static DepthState defaultValue => new DepthState(true, CompareFunction.Less);

        public bool writeEnabled { get => m_WriteEnabled != 0; set => m_WriteEnabled = (byte)(value ? 1 : 0); }
        public CompareFunction compareFunction { get => (CompareFunction)m_CompareFunction; set => m_CompareFunction = (sbyte)value; }

        public bool Equals(DepthState other) => m_WriteEnabled == other.m_WriteEnabled && m_CompareFunction == other.m_CompareFunction;
        public override bool Equals(object obj) => obj is DepthState other && Equals(other);
        public override int GetHashCode() => (m_WriteEnabled * 397) ^ m_CompareFunction;
        public static bool operator ==(DepthState left, DepthState right) => left.Equals(right);
        public static bool operator !=(DepthState left, DepthState right) => !left.Equals(right);
    }

    /// <summary>The culling mode draws; offsets, depth clip and conservative rasterization are kept until the devices have them.</summary>
    public struct RasterState : IEquatable<RasterState>
    {
        CullMode m_CullingMode;
        int m_OffsetUnits;
        float m_OffsetFactor;
        byte m_DepthClip;
        byte m_Conservative;

        public RasterState(CullMode cullingMode = CullMode.Back, int offsetUnits = 0, float offsetFactor = 0.0f, bool depthClip = true)
        {
            m_CullingMode = cullingMode;
            m_OffsetUnits = offsetUnits;
            m_OffsetFactor = offsetFactor;
            m_DepthClip = (byte)(depthClip ? 1 : 0);
            m_Conservative = 0;
        }

        public static RasterState defaultValue => new RasterState(CullMode.Back);

        public CullMode cullingMode { get => m_CullingMode; set => m_CullingMode = value; }
        public bool depthClip { get => m_DepthClip != 0; set => m_DepthClip = (byte)(value ? 1 : 0); }
        public bool conservative { get => m_Conservative != 0; set => m_Conservative = (byte)(value ? 1 : 0); }
        public int offsetUnits { get => m_OffsetUnits; set => m_OffsetUnits = value; }
        public float offsetFactor { get => m_OffsetFactor; set => m_OffsetFactor = value; }

        public bool Equals(RasterState other) =>
            m_CullingMode == other.m_CullingMode && m_OffsetUnits == other.m_OffsetUnits && m_OffsetFactor.Equals(other.m_OffsetFactor) &&
            m_DepthClip == other.m_DepthClip && m_Conservative == other.m_Conservative;
        public override bool Equals(object obj) => obj is RasterState other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)m_CullingMode, m_OffsetUnits, m_OffsetFactor, m_DepthClip, m_Conservative);
        public static bool operator ==(RasterState left, RasterState right) => left.Equals(right);
        public static bool operator !=(RasterState left, RasterState right) => !left.Equals(right);
    }

    /// <summary>Kept until the devices have stencil state.</summary>
    public struct StencilState : IEquatable<StencilState>
    {
        byte m_Enabled;
        byte m_ReadMask;
        byte m_WriteMask;
        byte m_CompareFunctionFront;
        byte m_PassOperationFront;
        byte m_FailOperationFront;
        byte m_ZFailOperationFront;
        byte m_CompareFunctionBack;
        byte m_PassOperationBack;
        byte m_FailOperationBack;
        byte m_ZFailOperationBack;

        public StencilState(bool enabled = true, byte readMask = 255, byte writeMask = 255, CompareFunction compareFunction = CompareFunction.Always,
                            StencilOp passOperation = StencilOp.Keep, StencilOp failOperation = StencilOp.Keep, StencilOp zFailOperation = StencilOp.Keep)
            : this(enabled, readMask, writeMask, compareFunction, passOperation, failOperation, zFailOperation, compareFunction, passOperation,
                   failOperation, zFailOperation)
        {
        }

        public StencilState(bool enabled, byte readMask, byte writeMask, CompareFunction compareFunctionFront, StencilOp passOperationFront,
                            StencilOp failOperationFront, StencilOp zFailOperationFront, CompareFunction compareFunctionBack, StencilOp passOperationBack,
                            StencilOp failOperationBack, StencilOp zFailOperationBack)
        {
            m_Enabled = (byte)(enabled ? 1 : 0);
            m_ReadMask = readMask;
            m_WriteMask = writeMask;
            m_CompareFunctionFront = (byte)compareFunctionFront;
            m_PassOperationFront = (byte)passOperationFront;
            m_FailOperationFront = (byte)failOperationFront;
            m_ZFailOperationFront = (byte)zFailOperationFront;
            m_CompareFunctionBack = (byte)compareFunctionBack;
            m_PassOperationBack = (byte)passOperationBack;
            m_FailOperationBack = (byte)failOperationBack;
            m_ZFailOperationBack = (byte)zFailOperationBack;
        }

        public static StencilState defaultValue => new StencilState(true, 255, 255, CompareFunction.Always, StencilOp.Keep, StencilOp.Keep, StencilOp.Keep);

        public bool enabled { get => m_Enabled != 0; set => m_Enabled = (byte)(value ? 1 : 0); }
        public byte readMask { get => m_ReadMask; set => m_ReadMask = value; }
        public byte writeMask { get => m_WriteMask; set => m_WriteMask = value; }
        public CompareFunction compareFunctionFront { get => (CompareFunction)m_CompareFunctionFront; set => m_CompareFunctionFront = (byte)value; }
        public StencilOp passOperationFront { get => (StencilOp)m_PassOperationFront; set => m_PassOperationFront = (byte)value; }
        public StencilOp failOperationFront { get => (StencilOp)m_FailOperationFront; set => m_FailOperationFront = (byte)value; }
        public StencilOp zFailOperationFront { get => (StencilOp)m_ZFailOperationFront; set => m_ZFailOperationFront = (byte)value; }
        public CompareFunction compareFunctionBack { get => (CompareFunction)m_CompareFunctionBack; set => m_CompareFunctionBack = (byte)value; }
        public StencilOp passOperationBack { get => (StencilOp)m_PassOperationBack; set => m_PassOperationBack = (byte)value; }
        public StencilOp failOperationBack { get => (StencilOp)m_FailOperationBack; set => m_FailOperationBack = (byte)value; }
        public StencilOp zFailOperationBack { get => (StencilOp)m_ZFailOperationBack; set => m_ZFailOperationBack = (byte)value; }

        public void SetCompareFunction(CompareFunction value)
        {
            compareFunctionFront = value;
            compareFunctionBack = value;
        }

        public void SetPassOperation(StencilOp value)
        {
            passOperationFront = value;
            passOperationBack = value;
        }

        public void SetFailOperation(StencilOp value)
        {
            failOperationFront = value;
            failOperationBack = value;
        }

        public void SetZFailOperation(StencilOp value)
        {
            zFailOperationFront = value;
            zFailOperationBack = value;
        }

        public bool Equals(StencilState other) =>
            m_Enabled == other.m_Enabled && m_ReadMask == other.m_ReadMask && m_WriteMask == other.m_WriteMask &&
            m_CompareFunctionFront == other.m_CompareFunctionFront && m_PassOperationFront == other.m_PassOperationFront &&
            m_FailOperationFront == other.m_FailOperationFront && m_ZFailOperationFront == other.m_ZFailOperationFront &&
            m_CompareFunctionBack == other.m_CompareFunctionBack && m_PassOperationBack == other.m_PassOperationBack &&
            m_FailOperationBack == other.m_FailOperationBack && m_ZFailOperationBack == other.m_ZFailOperationBack;
        public override bool Equals(object obj) => obj is StencilState other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(m_Enabled, m_ReadMask, m_WriteMask, m_CompareFunctionFront, m_PassOperationFront, m_CompareFunctionBack);
        public static bool operator ==(StencilState left, StencilState right) => left.Equals(right);
        public static bool operator !=(StencilState left, StencilState right) => !left.Equals(right);
    }

    public struct RenderStateBlock : IEquatable<RenderStateBlock>
    {
        BlendState m_BlendState;
        RasterState m_RasterState;
        DepthState m_DepthState;
        StencilState m_StencilState;
        int m_StencilReference;
        RenderStateMask m_Mask;

        public RenderStateBlock(RenderStateMask mask)
        {
            m_BlendState = BlendState.defaultValue;
            m_RasterState = RasterState.defaultValue;
            m_DepthState = DepthState.defaultValue;
            m_StencilState = StencilState.defaultValue;
            m_StencilReference = 0;
            m_Mask = mask;
        }

        public BlendState blendState { get => m_BlendState; set => m_BlendState = value; }
        public RasterState rasterState { get => m_RasterState; set => m_RasterState = value; }
        public DepthState depthState { get => m_DepthState; set => m_DepthState = value; }
        public StencilState stencilState { get => m_StencilState; set => m_StencilState = value; }
        public int stencilReference { get => m_StencilReference; set => m_StencilReference = value; }
        public RenderStateMask mask { get => m_Mask; set => m_Mask = value; }

        public bool Equals(RenderStateBlock other) =>
            m_BlendState.Equals(other.m_BlendState) && m_RasterState.Equals(other.m_RasterState) && m_DepthState.Equals(other.m_DepthState) &&
            m_StencilState.Equals(other.m_StencilState) && m_StencilReference == other.m_StencilReference && m_Mask == other.m_Mask;
        public override bool Equals(object obj) => obj is RenderStateBlock other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(m_BlendState, m_RasterState, m_DepthState, m_StencilState, m_StencilReference, (int)m_Mask);
        public static bool operator ==(RenderStateBlock left, RenderStateBlock right) => left.Equals(right);
        public static bool operator !=(RenderStateBlock left, RenderStateBlock right) => !left.Equals(right);
    }

    // ---------------------------------------------------------------------------------------------- culling

    public unsafe struct ScriptableCullingParameters : IEquatable<ScriptableCullingParameters>
    {
        int m_CullingMask;
        Matrix4x4 m_CullingMatrix;
        fixed float m_CullingPlanes[40];
        int m_CullingPlaneCount;
        CullingOptions m_CullingOptions;
        Vector3 m_Origin;
        byte m_IsOrthographic;
        internal Camera m_Camera;
        LODParameters m_LODParameters;
        int m_MaximumVisibleLights;
        fixed float m_LayerCullDistances[32];
        byte m_LayerCullSpherical;

        public static readonly int maximumCullingPlaneCount = 10;

        public uint cullingMask { get => (uint)m_CullingMask; set => m_CullingMask = (int)value; }
        public Matrix4x4 cullingMatrix { get => m_CullingMatrix; set => m_CullingMatrix = value; }
        public CullingOptions cullingOptions { get => m_CullingOptions; set => m_CullingOptions = value; }
        public Vector3 origin { get => m_Origin; set => m_Origin = value; }
        public bool isOrthographic { get => m_IsOrthographic != 0; set => m_IsOrthographic = (byte)(value ? 1 : 0); }
        public LODParameters lodParameters { get => m_LODParameters; set => m_LODParameters = value; }
        /// <summary>At most this many visible lights (directional ones first); -1 for all.</summary>
        public int maximumVisibleLights { get => m_MaximumVisibleLights; set => m_MaximumVisibleLights = value; }
        /// <summary>Layer cull distances measured from the origin instead of along the view.</summary>
        public bool layerCullSpherical { get => m_LayerCullSpherical != 0; set => m_LayerCullSpherical = (byte)(value ? 1 : 0); }

        public float GetLayerCullDistance(int layerIndex)
        {
            if (layerIndex < 0 || layerIndex >= 32)
                throw new ArgumentOutOfRangeException(nameof(layerIndex), $"Layer index should range from 0 to 31, was {layerIndex}");
            return m_LayerCullDistances[layerIndex];
        }

        public void SetLayerCullDistance(int layerIndex, float distance)
        {
            if (layerIndex < 0 || layerIndex >= 32)
                throw new ArgumentOutOfRangeException(nameof(layerIndex), $"Layer index should range from 0 to 31, was {layerIndex}");
            m_LayerCullDistances[layerIndex] = distance;
        }

        internal float[] LayerCullDistances()
        {
            var distances = new float[32];
            for (int i = 0; i < 32; i++)
                distances[i] = m_LayerCullDistances[i];
            return distances;
        }

        public int cullingPlaneCount
        {
            get => m_CullingPlaneCount;
            set
            {
                if (value < 0 || value > maximumCullingPlaneCount)
                    throw new ArgumentException($"Value should range from {0} to ScriptableCullingParameters.maximumCullingPlaneCount ({maximumCullingPlaneCount}), was {value}");
                m_CullingPlaneCount = value;
            }
        }

        public Plane GetCullingPlane(int index)
        {
            if (index < 0 || index >= cullingPlaneCount)
                throw new ArgumentOutOfRangeException(nameof(index), $"Index should range from 0 to {nameof(cullingPlaneCount)} ({cullingPlaneCount}), was {index}");
            return new Plane(new Vector3(m_CullingPlanes[index * 4], m_CullingPlanes[index * 4 + 1], m_CullingPlanes[index * 4 + 2]), m_CullingPlanes[index * 4 + 3]);
        }

        public void SetCullingPlane(int index, Plane plane)
        {
            if (index < 0 || index >= cullingPlaneCount)
                throw new ArgumentOutOfRangeException(nameof(index), $"Index should range from 0 to {nameof(cullingPlaneCount)} ({cullingPlaneCount}), was {index}");
            m_CullingPlanes[index * 4] = plane.normal.x;
            m_CullingPlanes[index * 4 + 1] = plane.normal.y;
            m_CullingPlanes[index * 4 + 2] = plane.normal.z;
            m_CullingPlanes[index * 4 + 3] = plane.distance;
        }

        internal Plane[] Planes()
        {
            var planes = new Plane[m_CullingPlaneCount];
            for (int i = 0; i < planes.Length; i++)
                planes[i] = GetCullingPlane(i);
            return planes;
        }

        public bool Equals(ScriptableCullingParameters other)
        {
            for (int i = 0; i < 40; i++)
                if (!m_CullingPlanes[i].Equals(other.m_CullingPlanes[i]))
                    return false;
            for (int i = 0; i < 32; i++)
                if (!m_LayerCullDistances[i].Equals(other.m_LayerCullDistances[i]))
                    return false;
            if (!m_LODParameters.Equals(other.m_LODParameters) || m_MaximumVisibleLights != other.m_MaximumVisibleLights ||
                m_LayerCullSpherical != other.m_LayerCullSpherical)
                return false;
            return m_CullingMask == other.m_CullingMask && m_CullingMatrix.Equals(other.m_CullingMatrix) && m_CullingPlaneCount == other.m_CullingPlaneCount &&
                   m_CullingOptions == other.m_CullingOptions && m_Origin.Equals(other.m_Origin) && m_IsOrthographic == other.m_IsOrthographic &&
                   ReferenceEquals(m_Camera, other.m_Camera);
        }

        public override bool Equals(object obj) => obj is ScriptableCullingParameters other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(m_CullingMask, m_CullingMatrix, m_CullingPlaneCount, (int)m_CullingOptions, m_Origin, m_IsOrthographic);
        public static bool operator ==(ScriptableCullingParameters left, ScriptableCullingParameters right) => left.Equals(right);
        public static bool operator !=(ScriptableCullingParameters left, ScriptableCullingParameters right) => !left.Equals(right);
    }

    /// <summary>A light that passed culling: the engine's memory as it is (the layout of the engine's VisibleLight).</summary>
    public struct VisibleLight : IEquatable<VisibleLight>
    {
        // Written by the engine (the memory is its own).
#pragma warning disable CS0649
        ulong m_LightId;
#pragma warning restore CS0649
        LightType m_LightType;
        Color m_FinalColor;
        Rect m_ScreenRect;
        Matrix4x4 m_LocalToWorldMatrix;
        float m_Range;
        float m_SpotAngle;
        uint m_Flags;

        public Light light => RenderPipelineNative.LightOf(m_LightId);
        public LightType lightType { get => m_LightType; set => m_LightType = value; }
        public Color finalColor { get => m_FinalColor; set => m_FinalColor = value; }
        public Rect screenRect { get => m_ScreenRect; set => m_ScreenRect = value; }
        public Matrix4x4 localToWorldMatrix { get => m_LocalToWorldMatrix; set => m_LocalToWorldMatrix = value; }
        public float range { get => m_Range; set => m_Range = value; }
        public float spotAngle { get => m_SpotAngle; set => m_SpotAngle = value; }
        public bool intersectsNearPlane { get => (m_Flags & 1u) != 0; set => m_Flags = value ? m_Flags | 1u : m_Flags & ~1u; }
        public bool intersectsFarPlane { get => (m_Flags & 2u) != 0; set => m_Flags = value ? m_Flags | 2u : m_Flags & ~2u; }

        public bool Equals(VisibleLight other) =>
            m_LightId == other.m_LightId && m_LightType == other.m_LightType && m_FinalColor.Equals(other.m_FinalColor) && m_ScreenRect.Equals(other.m_ScreenRect) &&
            m_LocalToWorldMatrix.Equals(other.m_LocalToWorldMatrix) && m_Range.Equals(other.m_Range) && m_SpotAngle.Equals(other.m_SpotAngle) && m_Flags == other.m_Flags;
        public override bool Equals(object obj) => obj is VisibleLight other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(m_LightId, (int)m_LightType, m_FinalColor, m_Range, m_SpotAngle, m_Flags);
        public static bool operator ==(VisibleLight left, VisibleLight right) => left.Equals(right);
        public static bool operator !=(VisibleLight left, VisibleLight right) => !left.Equals(right);
    }

    /// <summary>What Cull found, valid while the context that made it renders.</summary>
    public struct CullingResults : IEquatable<CullingResults>
    {
        internal ulong ptr;

        internal CullingResults(ulong p) => ptr = p;

        public unsafe NativeArray<VisibleLight> visibleLights
        {
            get
            {
                Validate();
                ulong data = RenderPipelineNative.VisibleLights(ptr, out int count);
                return NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<VisibleLight>((void*)data, count, Allocator.None);
            }
        }

        internal void Validate()
        {
            if (ptr == 0 || !RenderPipelineNative.IsCullingLive(ptr))
                throw new InvalidOperationException("The CullingResults instance is no longer valid. This can happen if you re-use it across multiple frames.");
        }

        public bool Equals(CullingResults other) => ptr == other.ptr;
        public override bool Equals(object obj) => obj is CullingResults other && Equals(other);
        public override int GetHashCode() => ptr.GetHashCode();
        public static bool operator ==(CullingResults left, CullingResults right) => left.Equals(right);
        public static bool operator !=(CullingResults left, CullingResults right) => !left.Equals(right);
    }
}
