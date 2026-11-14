using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Helix.Collections;
using Helix.Collections.LowLevel.Unsafe;
using HelixEngine.Experimental.Rendering;
using HelixEngine.Internal;
using HelixEngine.Rendering.RendererUtils;

namespace HelixEngine.Rendering.RendererUtils
{
    /// <summary>What a context made from the renderers of a culling result, drawn by CommandBuffer.DrawRendererList; valid in the context's frame.</summary>
    public struct RendererList : IEquatable<RendererList>
    {
        internal ulong m_Id;

        internal RendererList(ulong id) => m_Id = id;

        public static readonly RendererList nullRendererList = default;

        public bool isValid => m_Id != 0 && RenderPipelineNative.RendererListValid(m_Id);

        public bool Equals(RendererList other) => m_Id == other.m_Id;
        public override bool Equals(object obj) => obj is RendererList other && Equals(other);
        public override int GetHashCode() => m_Id.GetHashCode();
    }

    public enum RendererListStatus
    {
        kRendererListInvalid = -2,
        kRendererListProcessing = -1,
        kRendererListEmpty = 0,
        kRendererListPopulated = 1,
    }

    /// <summary>A renderer list from a camera's sorting, a queue range, masks and overrides.</summary>
    public struct RendererListDesc
    {
        public SortingCriteria sortingCriteria;
        public PerObjectData rendererConfiguration;
        public RenderQueueRange renderQueueRange;
        public RenderStateBlock? stateBlock;
        public Shader overrideShader;
        public Material overrideMaterial;
        public bool excludeObjectMotionVectors;
        public int layerMask;
        public uint renderingLayerMask;
        public int overrideMaterialPassIndex;
        public int overrideShaderPassIndex;

        internal CullingResults cullingResult { get; private set; }
        internal Camera camera { get; set; }
        internal ShaderTagId passName { get; private set; }
        internal ShaderTagId[] passNames { get; private set; }

        public RendererListDesc(ShaderTagId passName, CullingResults cullingResult, Camera camera) : this()
        {
            this.passName = passName;
            passNames = null;
            this.cullingResult = cullingResult;
            this.camera = camera;
            layerMask = -1;
            renderingLayerMask = uint.MaxValue;
            overrideMaterialPassIndex = 0;
        }

        public RendererListDesc(ShaderTagId[] passNames, CullingResults cullingResult, Camera camera) : this()
        {
            this.passNames = passNames;
            passName = ShaderTagId.none;
            this.cullingResult = cullingResult;
            this.camera = camera;
            layerMask = -1;
            renderingLayerMask = uint.MaxValue;
            overrideMaterialPassIndex = 0;
        }

        public bool IsValid()
        {
            if (camera == null || (passName == ShaderTagId.none && (passNames == null || passNames.Length == 0)))
                return false;
            return true;
        }

        public static RendererListParams ConvertToParameters(in RendererListDesc desc)
        {
            if (!desc.IsValid())
                return RendererListParams.Invalid;
            var sorting = new SortingSettings(desc.camera) { criteria = desc.sortingCriteria };
            var drawing = new DrawingSettings(ShaderTagId.none, sorting)
            {
                perObjectData = desc.rendererConfiguration,
                overrideMaterial = desc.overrideMaterial,
                overrideMaterialPassIndex = desc.overrideMaterialPassIndex,
                overrideShader = desc.overrideShader,
                overrideShaderPassIndex = desc.overrideShaderPassIndex,
            };
            if (desc.passName != ShaderTagId.none)
                drawing.SetShaderPassName(0, desc.passName);
            else
                for (int i = 0; i < desc.passNames.Length; i++)
                    drawing.SetShaderPassName(i, desc.passNames[i]);
            var filtering = new FilteringSettings(desc.renderQueueRange, desc.layerMask, desc.renderingLayerMask)
            {
                excludeMotionVectorObjects = desc.excludeObjectMotionVectors,
            };
            var param = new RendererListParams(desc.cullingResult, drawing, filtering);
            if (desc.stateBlock.HasValue)
            {
                var tags = new NativeArray<ShaderTagId>(1, Allocator.Temp);
                tags[0] = ShaderTagId.none;
                var states = new NativeArray<RenderStateBlock>(1, Allocator.Temp);
                states[0] = desc.stateBlock.Value;
                param.tagValues = tags;
                param.stateBlocks = states;
            }
            return param;
        }
    }
}

namespace HelixEngine.Rendering
{
    /// <summary>
    /// What a renderer list draws: DrawRenderers' settings, and states by tag value (the tagName tag of a pass, or of its
    /// shader when isPassTagName is false; ShaderTagId.none: every pass).
    /// </summary>
    public struct RendererListParams : IEquatable<RendererListParams>
    {
        public static readonly RendererListParams Invalid = default;

        public CullingResults cullingResults;
        public DrawingSettings drawSettings;
        public FilteringSettings filteringSettings;
        public ShaderTagId tagName;
        public bool isPassTagName;
        public NativeArray<ShaderTagId>? tagValues;
        public NativeArray<RenderStateBlock>? stateBlocks;

        public RendererListParams(CullingResults cullingResults, DrawingSettings drawSettings, FilteringSettings filteringSettings)
        {
            this.cullingResults = cullingResults;
            this.drawSettings = drawSettings;
            this.filteringSettings = filteringSettings;
            tagName = ShaderTagId.none;
            isPassTagName = false;
            tagValues = null;
            stateBlocks = null;
        }

        public void Validate()
        {
            if (tagValues.HasValue != stateBlocks.HasValue)
                throw new ArgumentException("Both or none of tagValues and stateBlocks arrays must be set.");
            if (tagValues.HasValue && tagValues.Value.Length != stateBlocks.Value.Length)
                throw new ArgumentException($"Arrays {nameof(tagValues)} and {nameof(stateBlocks)} should have same length, but {nameof(tagValues)} had length {tagValues.Value.Length} and {nameof(stateBlocks)} had length {stateBlocks.Value.Length}.");
        }

        internal void Dispose()
        {
            if (tagValues.HasValue && tagValues.Value.IsCreated)
                tagValues.Value.Dispose();
            if (stateBlocks.HasValue && stateBlocks.Value.IsCreated)
                stateBlocks.Value.Dispose();
        }

        public bool Equals(RendererListParams other) =>
            cullingResults.Equals(other.cullingResults) && drawSettings.Equals(other.drawSettings) && filteringSettings.Equals(other.filteringSettings) &&
            tagName == other.tagName && isPassTagName == other.isPassTagName && Nullable.Equals(tagValues, other.tagValues) &&
            Nullable.Equals(stateBlocks, other.stateBlocks);
        public override bool Equals(object obj) => obj is RendererListParams other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(cullingResults, drawSettings, filteringSettings, tagName, isPassTagName);
        public static bool operator ==(RendererListParams left, RendererListParams right) => left.Equals(right);
        public static bool operator !=(RendererListParams left, RendererListParams right) => !left.Equals(right);
    }

    /// <summary>
    /// An attachment of a native render pass: its format, what happens to it at the pass's start and end, and the target
    /// it loads from and stores to (none: a temporary of the pass).
    /// </summary>
    public struct AttachmentDescriptor : IEquatable<AttachmentDescriptor>
    {
        RenderBufferLoadAction m_LoadAction;
        RenderBufferStoreAction m_StoreAction;
        GraphicsFormat m_Format;
        RenderTargetIdentifier m_LoadStoreTarget;
        RenderTargetIdentifier m_ResolveTarget;
        Color m_ClearColor;
        float m_ClearDepth;
        uint m_ClearStencil;

        public AttachmentDescriptor(GraphicsFormat format)
        {
            m_LoadAction = RenderBufferLoadAction.DontCare;
            m_StoreAction = RenderBufferStoreAction.DontCare;
            m_Format = format;
            m_LoadStoreTarget = new RenderTargetIdentifier(BuiltinRenderTextureType.None);
            m_ResolveTarget = new RenderTargetIdentifier(BuiltinRenderTextureType.None);
            m_ClearColor = new Color(0, 0, 0, 0);
            m_ClearDepth = 1.0f;
            m_ClearStencil = 0;
        }

        public AttachmentDescriptor(RenderTextureFormat format) : this(GraphicsFormatUtility.GetGraphicsFormat(format, false)) { }

        public AttachmentDescriptor(RenderTextureFormat format, RenderTargetIdentifier target, bool loadExistingContents = false, bool storeResults = false,
                                    bool resolve = false) : this(GraphicsFormatUtility.GetGraphicsFormat(format, false), target, loadExistingContents, storeResults, resolve)
        {
        }

        public AttachmentDescriptor(GraphicsFormat format, RenderTargetIdentifier target, bool loadExistingContents = false, bool storeResults = false,
                                    bool resolve = false) : this(format)
        {
            ConfigureTarget(target, loadExistingContents, storeResults);
            if (resolve)
                ConfigureResolveTarget(target);
        }

        public RenderBufferLoadAction loadAction { get => m_LoadAction; set => m_LoadAction = value; }
        public RenderBufferStoreAction storeAction { get => m_StoreAction; set => m_StoreAction = value; }
        public GraphicsFormat graphicsFormat { get => m_Format; set => m_Format = value; }
        public RenderTextureFormat format
        {
            get => GraphicsFormatUtility.GetRenderTextureFormat(m_Format);
            set => m_Format = GraphicsFormatUtility.GetGraphicsFormat(value, false);
        }
        public RenderTargetIdentifier loadStoreTarget { get => m_LoadStoreTarget; set => m_LoadStoreTarget = value; }
        public RenderTargetIdentifier resolveTarget { get => m_ResolveTarget; set => m_ResolveTarget = value; }
        public Color clearColor { get => m_ClearColor; set => m_ClearColor = value; }
        public float clearDepth { get => m_ClearDepth; set => m_ClearDepth = value; }
        public uint clearStencil { get => m_ClearStencil; set => m_ClearStencil = value; }

        public void ConfigureTarget(RenderTargetIdentifier target, bool loadExistingContents, bool storeResults)
        {
            m_LoadStoreTarget = target;
            if (loadExistingContents && m_LoadAction != RenderBufferLoadAction.Clear)
                m_LoadAction = RenderBufferLoadAction.Load;
            if (storeResults)
                m_StoreAction = m_StoreAction == RenderBufferStoreAction.Resolve ? RenderBufferStoreAction.StoreAndResolve : RenderBufferStoreAction.Store;
        }

        public void ConfigureResolveTarget(RenderTargetIdentifier target)
        {
            m_ResolveTarget = target;
            m_StoreAction = m_StoreAction == RenderBufferStoreAction.Store ? RenderBufferStoreAction.StoreAndResolve : RenderBufferStoreAction.Resolve;
        }

        public void ConfigureClear(Color clearColor, float clearDepth = 1.0f, uint clearStencil = 0)
        {
            m_ClearColor = clearColor;
            m_ClearDepth = clearDepth;
            m_ClearStencil = clearStencil;
            m_LoadAction = RenderBufferLoadAction.Clear;
        }

        internal AttachmentData Data => new AttachmentData
        {
            graphicsFormat = (int)m_Format,
            loadAction = (int)m_LoadAction,
            storeAction = (int)m_StoreAction,
            loadStoreTarget = m_LoadStoreTarget.Data,
            resolveTarget = m_ResolveTarget.Data,
            clearColor = m_ClearColor,
            clearDepth = m_ClearDepth,
            clearStencil = m_ClearStencil,
        };

        public bool Equals(AttachmentDescriptor other) =>
            m_LoadAction == other.m_LoadAction && m_StoreAction == other.m_StoreAction && m_Format == other.m_Format &&
            m_LoadStoreTarget.Equals(other.m_LoadStoreTarget) && m_ResolveTarget.Equals(other.m_ResolveTarget) && m_ClearColor.Equals(other.m_ClearColor) &&
            m_ClearStencil == other.m_ClearStencil && m_ClearDepth.Equals(other.m_ClearDepth);
        public override bool Equals(object obj) => obj is AttachmentDescriptor other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(m_LoadAction, m_StoreAction, m_Format, m_LoadStoreTarget, m_ResolveTarget, m_ClearColor, m_ClearDepth, m_ClearStencil);
        public static bool operator ==(AttachmentDescriptor left, AttachmentDescriptor right) => left.Equals(right);
        public static bool operator !=(AttachmentDescriptor left, AttachmentDescriptor right) => !left.Equals(right);
    }

    /// <summary>
    /// A render texture's texels read back without waiting for the GPU: read when the frame's commands are submitted, the
    /// callback runs at the start of the next frame; the data stays readable until the frame after that.
    /// </summary>
    public struct AsyncGPUReadbackRequest
    {
        internal ulong m_Id;

        internal AsyncGPUReadbackRequest(ulong id)
        {
            m_Id = id;
            forcePlayerLoopUpdate = false;
        }

        public bool done => ReadbackNative.Done(m_Id);
        public bool hasError => ReadbackNative.HasError(m_Id);
        public int layerCount => 1;
        public int layerDataSize => ReadbackNative.DataSize(m_Id);
        public int width => ReadbackNative.Width(m_Id);
        public int height => ReadbackNative.Height(m_Id);
        public int depth => 1;
        /// <summary>Requests are read at submits and frame ends whether or not it is set.</summary>
        public bool forcePlayerLoopUpdate { get; set; }

        public void Update() { }

        /// <summary>Reads now when it still waits; a native array it reads into is filled.</summary>
        public void WaitForCompletion()
        {
            ReadbackNative.WaitForCompletion(m_Id);
            AsyncGPUReadback.FillTarget(m_Id);
        }

        public unsafe NativeArray<T> GetData<T>(int layer = 0) where T : struct
        {
            if (!done || hasError)
                throw new InvalidOperationException("Cannot access the data as it is not available");
            if (layer < 0 || layer >= layerCount)
                throw new ArgumentException($"Layer index is out of range {layer} / {layerCount}");
            int size = UnsafeUtility.SizeOf<T>();
            int bytes = layerDataSize;
            return NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>((void*)ReadbackNative.Data(m_Id), bytes / size, Allocator.None);
        }
    }

    public static unsafe class AsyncGPUReadback
    {
        // A request's C# side: its callback and the array it reads into.
        sealed class Pending
        {
            public Action<AsyncGPUReadbackRequest> callback;
            public void* target;
            public long targetBytes;
            public bool filled;
        }

        static readonly Dictionary<ulong, Pending> s_Targets = new Dictionary<ulong, Pending>();

        public static AsyncGPUReadbackRequest Request(Texture src, int mipIndex = 0, Action<AsyncGPUReadbackRequest> callback = null) =>
            Make(src, mipIndex, callback, 0, default);

        public static AsyncGPUReadbackRequest Request(Texture src, int mipIndex, TextureFormat dstFormat, Action<AsyncGPUReadbackRequest> callback = null) =>
            Make(src, mipIndex, callback, 0, default);

        public static AsyncGPUReadbackRequest Request(Texture src, int mipIndex, GraphicsFormat dstFormat, Action<AsyncGPUReadbackRequest> callback = null) =>
            Make(src, mipIndex, callback, 0, default);

        /// <summary>The texels are copied into output when the callback runs or WaitForCompletion returns.</summary>
        public static AsyncGPUReadbackRequest RequestIntoNativeArray<T>(ref NativeArray<T> output, Texture src, int mipIndex = 0,
                                                                       Action<AsyncGPUReadbackRequest> callback = null) where T : struct
        {
            if (!output.IsCreated)
                throw new ArgumentException("The output array is not created", nameof(output));
            return Make(src, mipIndex, callback, 0, ((IntPtr)output.m_Buffer, (long)output.Length * UnsafeUtility.SizeOf<T>()));
        }

        public static void WaitAllRequests()
        {
            ReadbackNative.WaitAllRequests();
            foreach (ulong id in new List<ulong>(s_Targets.Keys))
                FillTarget(id);
        }

        internal static AsyncGPUReadbackRequest Make(Texture src, int mipIndex, Action<AsyncGPUReadbackRequest> callback, ulong buffer, (IntPtr data, long bytes) target)
        {
            if (src == null)
                throw new ArgumentNullException(nameof(src));
            var pending = new Pending { callback = callback, target = (void*)target.data, targetBytes = target.bytes };
            bool managed = callback != null || target.data != IntPtr.Zero;
            GCHandle handle = managed ? GCHandle.Alloc(pending) : default;
            ulong function = managed ? (ulong)(delegate* unmanaged[Cdecl]<ulong, ulong, void>)&OnDone : 0;
            ulong user = managed ? (ulong)GCHandle.ToIntPtr(handle) : 0;
            ulong id = buffer != 0 ? ReadbackNative.RequestInBuffer(buffer, src, mipIndex, function, user) : ReadbackNative.Request(src, mipIndex, function, user);
            if (id == 0 && managed)
                handle.Free();
            if (id != 0 && target.data != IntPtr.Zero)
                s_Targets[id] = pending;
            return new AsyncGPUReadbackRequest(id);
        }

        internal static void FillTarget(ulong id)
        {
            if (!s_Targets.TryGetValue(id, out Pending p) || !ReadbackNative.Done(id))
                return;
            Fill(id, p);
        }

        static void Fill(ulong id, Pending p)
        {
            s_Targets.Remove(id);
            if (p.filled || p.target == null)
                return;
            p.filled = true;
            ulong data = ReadbackNative.Data(id);
            if (data == 0)
                return;
            long bytes = Math.Min(p.targetBytes, ReadbackNative.DataSize(id));
            Buffer.MemoryCopy((void*)data, p.target, p.targetBytes, bytes);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void OnDone(ulong user, ulong id)
        {
            try
            {
                GCHandle handle = GCHandle.FromIntPtr((IntPtr)user);
                var p = (Pending)handle.Target;
                handle.Free();
                Fill(id, p);
                p.callback?.Invoke(new AsyncGPUReadbackRequest(id));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    /// <summary>A pipeline's settings of one kind, kept by its global settings asset.</summary>
    public interface IRenderPipelineGraphicsSettings
    {
        int version { get; }
        bool isAvailableInPlayerBuild => false;
    }

    /// <summary>The settings asset of a pipeline type: GraphicsSettings finds the current pipeline's settings in it.</summary>
    public abstract class RenderPipelineGlobalSettings : ScriptableObject
    {
        protected virtual List<IRenderPipelineGraphicsSettings> settingsList { get; }

        internal bool TryGet(Type type, out IRenderPipelineGraphicsSettings settings)
        {
            settings = null;
            List<IRenderPipelineGraphicsSettings> list = settingsList;
            if (list == null)
                return false;
            foreach (IRenderPipelineGraphicsSettings s in list)
                if (s != null && type.IsInstanceOfType(s))
                {
                    settings = s;
                    return true;
                }
            return false;
        }

        internal bool Contains(Type type) => TryGet(type, out _);
    }

    public static partial class GraphicsSettings
    {
        static readonly Dictionary<Type, RenderPipelineGlobalSettings> s_PipelineSettings = new Dictionary<Type, RenderPipelineGlobalSettings>();

        public static void RegisterRenderPipelineSettings<T>(RenderPipelineGlobalSettings settings) where T : RenderPipeline
        {
            if (settings == null)
                s_PipelineSettings.Remove(typeof(T));
            else
                s_PipelineSettings[typeof(T)] = settings;
        }

        public static void UnregisterRenderPipelineSettings<T>() where T : RenderPipeline => s_PipelineSettings.Remove(typeof(T));

        public static RenderPipelineGlobalSettings GetSettingsForRenderPipeline<T>() where T : RenderPipeline => SettingsOf(typeof(T));

        /// <summary>The settings of the current pipeline's type (the running pipeline's, else the current asset's pipelineType).</summary>
        public static bool TryGetCurrentRenderPipelineGlobalSettings(out RenderPipelineGlobalSettings asset)
        {
            Type type = RenderPipelineManager.currentPipeline?.GetType() ?? currentRenderPipeline?.pipelineType;
            asset = type != null ? SettingsOf(type) : null;
            return asset != null;
        }

        public static T GetRenderPipelineSettings<T>() where T : class, IRenderPipelineGraphicsSettings
        {
            TryGetRenderPipelineSettings(out T settings);
            return settings;
        }

        public static bool TryGetRenderPipelineSettings<T>(out T settings) where T : class, IRenderPipelineGraphicsSettings
        {
            settings = null;
            if (!TryGetCurrentRenderPipelineGlobalSettings(out RenderPipelineGlobalSettings asset) || !asset.TryGet(typeof(T), out IRenderPipelineGraphicsSettings s))
                return false;
            settings = s as T;
            return settings != null;
        }

        // The settings of a type or of its nearest base pipeline type that has some.
        static RenderPipelineGlobalSettings SettingsOf(Type type)
        {
            for (Type t = type; t != null && t != typeof(object); t = t.BaseType)
                if (s_PipelineSettings.TryGetValue(t, out RenderPipelineGlobalSettings s) && s != null)
                    return s;
            return null;
        }
    }
}
