using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HelixEngine.Experimental.Rendering;
using HelixEngine.Internal;
using HelixEngine.Rendering;

namespace HelixEngine.Rendering
{
    /// <summary>
    /// What a pipeline renders through, for one call of its Render: Cull runs at once, the other calls record commands
    /// Submit runs on the device in order.
    /// </summary>
    public struct ScriptableRenderContext : IEquatable<ScriptableRenderContext>
    {
        ulong m_Ptr;

        internal ScriptableRenderContext(ulong ptr) => m_Ptr = ptr;

        internal ulong id => m_Ptr;

        void Validate()
        {
            if (m_Ptr == 0 || !RenderPipelineNative.IsContextLive(m_Ptr))
                throw new InvalidOperationException("The ScriptableRenderContext instance is no longer valid. This can happen if you re-use it across multiple frames.");
        }

        public CullingResults Cull(ref ScriptableCullingParameters parameters)
        {
            Validate();
            LODParameters lod = parameters.lodParameters;
            ulong results = RenderPipelineNative.Cull(m_Ptr, parameters.m_Camera, (int)parameters.cullingMask, parameters.cullingMatrix, parameters.Planes(),
                                                      (int)parameters.cullingOptions, parameters.origin, parameters.isOrthographic,
                                                      new Vector4(lod.fieldOfView, lod.orthoSize, lod.cameraPixelHeight, lod.isOrthographic ? 1 : 0),
                                                      lod.cameraPosition, parameters.LayerCullDistances(), parameters.layerCullSpherical,
                                                      parameters.maximumVisibleLights);
            return new CullingResults(results);
        }

        public void DrawRenderers(CullingResults cullingResults, ref DrawingSettings drawingSettings, ref FilteringSettings filteringSettings)
        {
            var state = new RenderStateBlock(RenderStateMask.Nothing);
            DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings, ref state);
        }

        public void DrawRenderers(CullingResults cullingResults, ref DrawingSettings drawingSettings, ref FilteringSettings filteringSettings,
                                  ref RenderStateBlock stateBlock)
        {
            Validate();
            cullingResults.Validate();
            SortingSettings sorting = drawingSettings.sortingSettings;
            var data = new DrawRendererData
            {
                worldToCameraMatrix = sorting.worldToCameraMatrix,
                cameraPosition = sorting.cameraPosition,
                customAxis = sorting.customAxis,
                criteria = (int)sorting.criteria,
                distanceMetric = (int)sorting.distanceMetric,
                perObjectData = (int)drawingSettings.perObjectData,
                flags = (drawingSettings.enableDynamicBatching ? 1 : 0) | (drawingSettings.enableInstancing ? 2 : 0),
                overrideMaterialPassIndex = drawingSettings.overrideMaterialPassIndex,
                overrideShaderPassIndex = drawingSettings.overrideShaderPassIndex,
                mainLightIndex = drawingSettings.mainLightIndex,
                queueLower = filteringSettings.renderQueueRange.lowerBound,
                queueUpper = filteringSettings.renderQueueRange.upperBound,
                layerMask = filteringSettings.layerMask,
                renderingLayerMask = filteringSettings.renderingLayerMask,
                batchLayerMask = filteringSettings.batchLayerMask,
                excludeMotionVectorObjects = filteringSettings.excludeMotionVectorObjects ? 1 : 0,
            };
            BlendState blend = stateBlock.blendState;
            RenderTargetBlendState target = blend.blendState0;
            DepthState depth = stateBlock.depthState;
            RasterState raster = stateBlock.rasterState;
            StencilState stencil = stateBlock.stencilState;
            data.stateMask = (int)stateBlock.mask;
            data.blendFlags = (blend.separateMRTBlendStates ? 1 : 0) | (blend.alphaToMask ? 2 : 0);
            data.writeMask = (int)target.writeMask;
            data.sourceColor = (int)target.sourceColorBlendMode;
            data.destinationColor = (int)target.destinationColorBlendMode;
            data.sourceAlpha = (int)target.sourceAlphaBlendMode;
            data.destinationAlpha = (int)target.destinationAlphaBlendMode;
            data.colorOperation = (int)target.colorBlendOperation;
            data.alphaOperation = (int)target.alphaBlendOperation;
            data.depthWrite = depth.writeEnabled ? 1 : 0;
            data.depthCompare = (int)depth.compareFunction;
            data.cullingMode = (int)raster.cullingMode;
            data.offsetUnits = raster.offsetUnits;
            data.offsetFactor = raster.offsetFactor;
            data.rasterFlags = (raster.depthClip ? 1 : 0) | (raster.conservative ? 2 : 0);
            data.stencilEnabled = stencil.enabled ? 1 : 0;
            data.stencilReadMask = stencil.readMask;
            data.stencilWriteMask = stencil.writeMask;
            data.stencilCompareFront = (int)stencil.compareFunctionFront;
            data.stencilPassFront = (int)stencil.passOperationFront;
            data.stencilFailFront = (int)stencil.failOperationFront;
            data.stencilZFailFront = (int)stencil.zFailOperationFront;
            data.stencilCompareBack = (int)stencil.compareFunctionBack;
            data.stencilPassBack = (int)stencil.passOperationBack;
            data.stencilFailBack = (int)stencil.failOperationBack;
            data.stencilZFailBack = (int)stencil.zFailOperationBack;
            data.stencilReference = stateBlock.stencilReference;
            // The other targets' blend states, seven values each.
            var blends = new int[7 * 7];
            RenderTargetBlendState[] others = { blend.blendState1, blend.blendState2, blend.blendState3, blend.blendState4, blend.blendState5, blend.blendState6, blend.blendState7 };
            for (int i = 0; i < 7; i++)
            {
                RenderTargetBlendState b = others[i];
                blends[i * 7] = (int)b.writeMask;
                blends[i * 7 + 1] = (int)b.sourceColorBlendMode;
                blends[i * 7 + 2] = (int)b.destinationColorBlendMode;
                blends[i * 7 + 3] = (int)b.sourceAlphaBlendMode;
                blends[i * 7 + 4] = (int)b.destinationAlphaBlendMode;
                blends[i * 7 + 5] = (int)b.colorBlendOperation;
                blends[i * 7 + 6] = (int)b.alphaBlendOperation;
            }
            RenderPipelineNative.DrawRenderers(m_Ptr, cullingResults.ptr, data, drawingSettings.PassNames(), drawingSettings.overrideMaterial,
                                               drawingSettings.overrideShader, drawingSettings.fallbackMaterial, blends);
        }

        public void SetupCameraProperties(Camera camera, bool stereoSetup = false)
        {
            Validate();
            if (camera == null)
                throw new ArgumentNullException(nameof(camera));
            RenderPipelineNative.SetupCameraProperties(m_Ptr, camera, stereoSetup);
        }

        public void ExecuteCommandBuffer(CommandBuffer commandBuffer)
        {
            Validate();
            if (commandBuffer == null)
                throw new ArgumentNullException(nameof(commandBuffer));
            RenderPipelineNative.ExecuteCommandBuffer(m_Ptr, commandBuffer.Id);
        }

        /// <summary>The devices have one queue: the commands run in order with the others.</summary>
        public void ExecuteCommandBufferAsync(CommandBuffer commandBuffer, ComputeQueueType queueType) => ExecuteCommandBuffer(commandBuffer);

        public void InvokeOnRenderObjectCallback()
        {
            Validate();
            RenderPipelineNative.InvokeOnRenderObjectCallback(m_Ptr);
        }

        public void Submit()
        {
            Validate();
            RenderPipelineNative.Submit(m_Ptr);
        }

        public bool Equals(ScriptableRenderContext other) => m_Ptr == other.m_Ptr;
        public override bool Equals(object obj) => obj is ScriptableRenderContext other && Equals(other);
        public override int GetHashCode() => m_Ptr.GetHashCode();
        public static bool operator ==(ScriptableRenderContext left, ScriptableRenderContext right) => left.Equals(right);
        public static bool operator !=(ScriptableRenderContext left, ScriptableRenderContext right) => !left.Equals(right);
    }

    /// <summary>A pipeline: the engine calls its Render each frame with the cameras by depth.</summary>
    public abstract class RenderPipeline
    {
        protected abstract void Render(ScriptableRenderContext context, Camera[] cameras);

        protected virtual void Render(ScriptableRenderContext context, List<Camera> cameras) => Render(context, cameras.ToArray());

        protected static void BeginContextRendering(ScriptableRenderContext context, List<Camera> cameras) =>
            RenderPipelineNative.BeginContextRendering(context.id, cameras.ToArray());

        protected static void EndContextRendering(ScriptableRenderContext context, List<Camera> cameras) =>
            RenderPipelineNative.EndContextRendering(context.id, cameras.ToArray());

        protected static void BeginCameraRendering(ScriptableRenderContext context, Camera camera) => RenderPipelineNative.BeginCameraRendering(context.id, camera);

        protected static void EndCameraRendering(ScriptableRenderContext context, Camera camera) => RenderPipelineNative.EndCameraRendering(context.id, camera);

        public bool disposed { get; private set; }

        protected virtual void Dispose(bool disposing)
        {
        }

        internal void InternalRender(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (disposed)
                throw new ObjectDisposedException($"{this} has been disposed. Do not call Render on disposed a RenderPipeline.");
            Render(context, cameras);
        }

        internal void InternalDispose()
        {
            Dispose(true);
            disposed = true;
        }

        /// <summary>A request to render a camera into a render texture (its mip, face and slice).</summary>
        public class StandardRequest
        {
            public RenderTexture destination = null;
            public int mipLevel = 0;
            public CubemapFace face = CubemapFace.Unknown;
            public int slice = 0;
        }

        /// <summary>Whether the active pipeline takes a request of this camera.</summary>
        public static bool SupportsRenderRequest<RequestData>(Camera camera, RequestData data) => Request(camera, data, false);

        /// <summary>The request rendered by the active pipeline now (an error when it does not take it).</summary>
        public static void SubmitRenderRequest<RequestData>(Camera camera, RequestData data)
        {
            if (!Request(camera, data, true))
                Debug.LogError("RenderPipeline.SubmitRenderRequest: the active render pipeline does not support the request.");
        }

        static bool Request<RequestData>(Camera camera, RequestData data, bool process)
        {
            if (camera == null)
                throw new ArgumentNullException(nameof(camera));
            if (data is StandardRequest standard)
                return RenderPipelineNative.RenderRequest(camera, 0, standard.destination, standard.mipLevel, (int)standard.face, standard.slice, process);
            GCHandle handle = GCHandle.Alloc(data);
            try
            {
                return RenderPipelineNative.RenderRequest(camera, (ulong)GCHandle.ToIntPtr(handle), null, 0, -1, 0, process);
            }
            finally
            {
                handle.Free();
            }
        }

        protected virtual bool IsRenderRequestSupported<RequestData>(Camera camera, RequestData data) => false;

        protected virtual void ProcessRenderRequests<RequestData>(ScriptableRenderContext context, Camera camera, RequestData renderRequest)
        {
        }

        // The generic members for a request's type (made once per type).
        static readonly Dictionary<Type, (System.Reflection.MethodInfo Supported, System.Reflection.MethodInfo Process)> s_RequestMethods = new();

        static (System.Reflection.MethodInfo Supported, System.Reflection.MethodInfo Process) RequestMethods(Type type)
        {
            if (!s_RequestMethods.TryGetValue(type, out var m))
            {
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                m = (typeof(RenderPipeline).GetMethod(nameof(IsRenderRequestSupported), flags).MakeGenericMethod(type),
                     typeof(RenderPipeline).GetMethod(nameof(ProcessRenderRequests), flags).MakeGenericMethod(type));
                s_RequestMethods[type] = m;
            }
            return m;
        }

        internal bool InternalSupports(Camera camera, object data) => (bool)RequestMethods(data.GetType()).Supported.Invoke(this, new[] { camera, data });

        internal void InternalProcess(ScriptableRenderContext context, Camera camera, object data) =>
            RequestMethods(data.GetType()).Process.Invoke(this, new[] { context, camera, data });
    }

    /// <summary>A ScriptableObject that makes a pipeline; set it in GraphicsSettings or QualitySettings.</summary>
    public abstract class RenderPipelineAsset : ScriptableObject
    {
        protected abstract RenderPipeline CreatePipeline();

        internal RenderPipeline InternalCreatePipeline() => CreatePipeline();

        /// <summary>Its values changed: the pipeline is made again.</summary>
        protected virtual void OnValidate() => RenderPipelineNative.ResetPipeline(this);

        /// <summary>It is disabled or destroyed: the pipeline is made again.</summary>
        protected virtual void OnDisable() => RenderPipelineNative.ResetPipeline(this);
    }

    public static class RenderPipelineManager
    {
        internal static RenderPipeline s_CurrentPipeline;

        public static event Action<ScriptableRenderContext, List<Camera>> beginContextRendering;
        public static event Action<ScriptableRenderContext, List<Camera>> endContextRendering;
        public static event Action<ScriptableRenderContext, Camera> beginCameraRendering;
        public static event Action<ScriptableRenderContext, Camera> endCameraRendering;
        public static event Action<RenderPipelineAsset, RenderPipelineAsset> activeRenderPipelineAssetChanged;
        public static event Action activeRenderPipelineTypeChanged;

        /// <summary>The C# pipeline rendering now (null for the engine's fallback pipeline and for C++ pipelines).</summary>
        public static RenderPipeline currentPipeline => s_CurrentPipeline != null && !s_CurrentPipeline.disposed ? s_CurrentPipeline : null;

        internal static void Raise(uint kind, ulong context)
        {
            var c = new ScriptableRenderContext(context);
            switch (kind)
            {
            case 0:
                beginContextRendering?.Invoke(c, new List<Camera>(RenderPipelineNative.EventCameras()));
                break;
            case 1:
                endContextRendering?.Invoke(c, new List<Camera>(RenderPipelineNative.EventCameras()));
                break;
            case 2:
                beginCameraRendering?.Invoke(c, RenderPipelineNative.EventCamera());
                break;
            case 3:
                endCameraRendering?.Invoke(c, RenderPipelineNative.EventCamera());
                break;
            case 4:
                activeRenderPipelineAssetChanged?.Invoke(RenderPipelineNative.EventAsset(false) as RenderPipelineAsset,
                                                         RenderPipelineNative.EventAsset(true) as RenderPipelineAsset);
                break;
            case 5:
                activeRenderPipelineTypeChanged?.Invoke();
                break;
            }
        }
    }

    public static class GraphicsSettings
    {
        /// <summary>The asset when the quality level sets none (null: the engine's fallback pipeline).</summary>
        public static RenderPipelineAsset defaultRenderPipeline
        {
            get => RenderPipelineNative.DefaultRenderPipeline() as RenderPipelineAsset;
            set => RenderPipelineNative.SetDefaultRenderPipeline(value);
        }

        [Obsolete("renderPipelineAsset has been deprecated. Use defaultRenderPipeline instead.")]
        public static RenderPipelineAsset renderPipelineAsset
        {
            get => defaultRenderPipeline;
            set => defaultRenderPipeline = value;
        }

        /// <summary>The asset that renders: the quality level's, else the default.</summary>
        public static RenderPipelineAsset currentRenderPipeline => RenderPipelineNative.CurrentRenderPipeline() as RenderPipelineAsset;

        public static uint defaultRenderingLayerMask
        {
            get => RenderPipelineNative.DefaultRenderingLayerMask();
            set => RenderPipelineNative.SetDefaultRenderingLayerMask(value);
        }

        /// <summary>Materials and objects keep their blocks in persistent GPU memory, bound by offset (where the device can).</summary>
        public static bool useScriptableRenderPipelineBatching
        {
            get => CullingNative.Batching();
            set => CullingNative.SetBatching(value);
        }

        /// <summary>The distance metric of SortingSettings made from a camera (Default: by the camera's projection).</summary>
        public static TransparencySortMode transparencySortMode
        {
            get => (TransparencySortMode)CullingNative.SortMode();
            set => CullingNative.SetSortMode((int)value);
        }

        public static Vector3 transparencySortAxis
        {
            get => CullingNative.SortAxis();
            set => CullingNative.SetSortAxis(value);
        }
    }

    /// <summary>A list of commands recorded now and run where it is executed.</summary>
    public class CommandBuffer : IDisposable
    {
        ulong m_Ptr;

        public CommandBuffer() => m_Ptr = RenderPipelineNative.CreateBuffer();

        ~CommandBuffer() => Dispose(false);

        internal ulong Id
        {
            get
            {
                if (m_Ptr == 0)
                    throw new ObjectDisposedException("CommandBuffer");
                return m_Ptr;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing)
        {
            if (m_Ptr != 0)
                RenderPipelineNative.DestroyBuffer(m_Ptr);
            m_Ptr = 0;
        }

        public void Release() => Dispose();

        public string name
        {
            get => RenderPipelineNative.BufferName(Id);
            set => RenderPipelineNative.SetBufferName(Id, value ?? "");
        }

        public int sizeInBytes => RenderPipelineNative.BufferSize(Id);

        public void Clear() => RenderPipelineNative.ClearBuffer(Id);

        public void ClearRenderTarget(bool clearDepth, bool clearColor, Color backgroundColor, float depth = 1.0f) =>
            RenderPipelineNative.ClearRenderTarget(Id, clearDepth, clearColor, backgroundColor, depth);

        public void SetViewport(Rect pixelRect) => RenderPipelineNative.SetViewport(Id, pixelRect);
        public void EnableScissorRect(Rect scissor) => RenderPipelineNative.EnableScissorRect(Id, scissor);
        public void DisableScissorRect() => RenderPipelineNative.DisableScissorRect(Id);
        public void SetViewProjectionMatrices(Matrix4x4 view, Matrix4x4 proj) => RenderPipelineNative.SetViewProjectionMatrices(Id, view, proj);

        public void SetGlobalFloat(string name, float value) => SetGlobalFloat(Shader.PropertyToID(name), value);
        public void SetGlobalFloat(int nameID, float value) => RenderPipelineNative.SetGlobalFloat(Id, nameID, value);
        public void SetGlobalInt(string name, int value) => SetGlobalInt(Shader.PropertyToID(name), value);
        public void SetGlobalInt(int nameID, int value) => RenderPipelineNative.SetGlobalFloat(Id, nameID, value);
        public void SetGlobalInteger(string name, int value) => SetGlobalInteger(Shader.PropertyToID(name), value);
        public void SetGlobalInteger(int nameID, int value) => RenderPipelineNative.SetGlobalInteger(Id, nameID, value);
        public void SetGlobalVector(string name, Vector4 value) => SetGlobalVector(Shader.PropertyToID(name), value);
        public void SetGlobalVector(int nameID, Vector4 value) => RenderPipelineNative.SetGlobalVector(Id, nameID, value);
        public void SetGlobalColor(string name, Color value) => SetGlobalColor(Shader.PropertyToID(name), value);
        public void SetGlobalColor(int nameID, Color value) => RenderPipelineNative.SetGlobalVector(Id, nameID, new Vector4(value.r, value.g, value.b, value.a));
        public void SetGlobalFloatArray(string propertyName, float[] values) => SetGlobalFloatArray(Shader.PropertyToID(propertyName), values);
        public void SetGlobalFloatArray(int nameID, float[] values) => RenderPipelineNative.SetGlobalFloatArray(Id, nameID, values ?? throw new ArgumentNullException(nameof(values)));
        public void SetGlobalFloatArray(string propertyName, System.Collections.Generic.List<float> values) => SetGlobalFloatArray(propertyName, values?.ToArray());
        public void SetGlobalFloatArray(int nameID, System.Collections.Generic.List<float> values) => SetGlobalFloatArray(nameID, values?.ToArray());
        public void SetGlobalVectorArray(string propertyName, Vector4[] values) => SetGlobalVectorArray(Shader.PropertyToID(propertyName), values);
        public void SetGlobalVectorArray(int nameID, Vector4[] values) => RenderPipelineNative.SetGlobalVectorArray(Id, nameID, values ?? throw new ArgumentNullException(nameof(values)));
        public void SetGlobalVectorArray(string propertyName, System.Collections.Generic.List<Vector4> values) => SetGlobalVectorArray(propertyName, values?.ToArray());
        public void SetGlobalVectorArray(int nameID, System.Collections.Generic.List<Vector4> values) => SetGlobalVectorArray(nameID, values?.ToArray());
        public void SetGlobalMatrixArray(string propertyName, Matrix4x4[] values) => SetGlobalMatrixArray(Shader.PropertyToID(propertyName), values);
        public void SetGlobalMatrixArray(int nameID, Matrix4x4[] values) => RenderPipelineNative.SetGlobalMatrixArray(Id, nameID, values ?? throw new ArgumentNullException(nameof(values)));
        public void SetGlobalMatrixArray(string propertyName, System.Collections.Generic.List<Matrix4x4> values) => SetGlobalMatrixArray(propertyName, values?.ToArray());
        public void SetGlobalMatrixArray(int nameID, System.Collections.Generic.List<Matrix4x4> values) => SetGlobalMatrixArray(nameID, values?.ToArray());
        /// <summary>Front faces turn around for the draws after it (mirrored views).</summary>
        public void SetInvertCulling(bool invertCulling) => RenderPipelineNative.SetInvertCulling(Id, invertCulling);
        public void SetGlobalMatrix(string name, Matrix4x4 value) => SetGlobalMatrix(Shader.PropertyToID(name), value);
        public void SetGlobalMatrix(int nameID, Matrix4x4 value) => RenderPipelineNative.SetGlobalMatrix(Id, nameID, value);
        public void SetGlobalTexture(string name, RenderTargetIdentifier value) => SetGlobalTexture(Shader.PropertyToID(name), value);
        public void SetGlobalTexture(int nameID, RenderTargetIdentifier value) => RenderPipelineNative.SetGlobalTextureTarget(Id, nameID, value.Data);

        public void EnableShaderKeyword(string keyword) => RenderPipelineNative.SetKeyword(Id, keyword, true);
        public void DisableShaderKeyword(string keyword) => RenderPipelineNative.SetKeyword(Id, keyword, false);
        public void EnableKeyword(in GlobalKeyword keyword) => RenderPipelineNative.SetKeyword(Id, keyword.name, true);
        public void DisableKeyword(in GlobalKeyword keyword) => RenderPipelineNative.SetKeyword(Id, keyword.name, false);
        public void SetKeyword(in GlobalKeyword keyword, bool value) => RenderPipelineNative.SetKeyword(Id, keyword.name, value);

        public void DrawMeshInstanced(Mesh mesh, int submeshIndex, Material material, int shaderPass, Matrix4x4[] matrices, int count,
                                      MaterialPropertyBlock properties)
        {
            if (matrices == null)
                throw new ArgumentNullException(nameof(matrices));
            if (count < 0 || count > matrices.Length)
                throw new ArgumentOutOfRangeException(nameof(count));
            RenderPipelineNative.DrawMeshInstanced(Id, mesh, submeshIndex, material, shaderPass, new ReadOnlySpan<Matrix4x4>(matrices, 0, count).ToArray(),
                                                   properties?.m_Block ?? default);
        }

        public void DrawMeshInstanced(Mesh mesh, int submeshIndex, Material material, int shaderPass, Matrix4x4[] matrices, int count) =>
            DrawMeshInstanced(mesh, submeshIndex, material, shaderPass, matrices, count, null);

        public void DrawMeshInstanced(Mesh mesh, int submeshIndex, Material material, int shaderPass, Matrix4x4[] matrices) =>
            DrawMeshInstanced(mesh, submeshIndex, material, shaderPass, matrices, matrices?.Length ?? 0, null);

        public void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int submeshIndex = 0, int shaderPass = -1, MaterialPropertyBlock properties = null)
        {
            if (mesh == null)
                throw new ArgumentNullException(nameof(mesh));
            if (submeshIndex < 0 || submeshIndex >= Math.Max(1, mesh.subMeshCount))
                throw new ArgumentOutOfRangeException(nameof(submeshIndex), $"Invalid submesh index {submeshIndex}.");
            if (material == null)
                throw new ArgumentNullException(nameof(material));
            RenderPipelineNative.DrawMesh(Id, mesh, matrix, material, submeshIndex, shaderPass, properties?.m_Block ?? default);
        }

        public void DrawRenderer(Renderer renderer, Material material, int submeshIndex = 0, int shaderPass = -1)
        {
            if (renderer == null)
                throw new ArgumentNullException(nameof(renderer));
            if (submeshIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(submeshIndex), $"Invalid submesh index {submeshIndex}.");
            if (material == null)
                throw new ArgumentNullException(nameof(material));
            RenderPipelineNative.DrawRenderer(Id, renderer, material, submeshIndex, shaderPass);
        }

        public void BeginSample(string name) => RenderPipelineNative.BeginSample(Id, name ?? "");
        public void EndSample(string name) => RenderPipelineNative.EndSample(Id, name ?? "");

        public void ClearRenderTarget(RTClearFlags clearFlags, Color backgroundColor, float depth = 1.0f, uint stencil = 0) =>
            ClearRenderTarget((clearFlags & RTClearFlags.Depth) != 0, (clearFlags & RTClearFlags.Color) != 0, backgroundColor, depth);

        // ---------------------------------------------------------------------------------------------- targets

        static readonly RenderTargetData[] s_NoColors = new RenderTargetData[0];

        void SetTarget(RenderTargetIdentifier[] colors, RenderTargetIdentifier depth, bool separateDepth, RenderBufferLoadAction load = RenderBufferLoadAction.Load,
                       RenderBufferStoreAction store = RenderBufferStoreAction.Store)
        {
            if (colors == null)
                throw new ArgumentNullException(nameof(colors));
            var data = colors.Length == 0 ? s_NoColors : new RenderTargetData[colors.Length];
            for (int i = 0; i < colors.Length; i++)
                data[i] = colors[i].Data;
            RenderPipelineNative.SetRenderTarget(Id, data, depth.Data, separateDepth, (int)load, (int)store);
        }

        public void SetRenderTarget(RenderTargetIdentifier rt) => SetTarget(new[] { rt }, rt, false);
        public void SetRenderTarget(RenderTargetIdentifier rt, RenderBufferLoadAction loadAction, RenderBufferStoreAction storeAction) =>
            SetTarget(new[] { rt }, rt, false, loadAction, storeAction);
        public void SetRenderTarget(RenderTargetIdentifier rt, RenderBufferLoadAction colorLoadAction, RenderBufferStoreAction colorStoreAction,
                                    RenderBufferLoadAction depthLoadAction, RenderBufferStoreAction depthStoreAction) =>
            SetTarget(new[] { rt }, rt, false, colorLoadAction, colorStoreAction);
        public void SetRenderTarget(RenderTargetIdentifier rt, int mipLevel) => SetRenderTarget(new RenderTargetIdentifier(rt, mipLevel));
        public void SetRenderTarget(RenderTargetIdentifier rt, int mipLevel, CubemapFace cubemapFace) => SetRenderTarget(new RenderTargetIdentifier(rt, mipLevel, cubemapFace));
        public void SetRenderTarget(RenderTargetIdentifier rt, int mipLevel, CubemapFace cubemapFace, int depthSlice) =>
            SetRenderTarget(new RenderTargetIdentifier(rt, mipLevel, cubemapFace, depthSlice));
        public void SetRenderTarget(RenderTargetIdentifier color, RenderTargetIdentifier depth) => SetTarget(new[] { color }, depth, true);
        public void SetRenderTarget(RenderTargetIdentifier color, RenderTargetIdentifier depth, int mipLevel) =>
            SetRenderTarget(color, depth, mipLevel, CubemapFace.Unknown, 0);
        public void SetRenderTarget(RenderTargetIdentifier color, RenderTargetIdentifier depth, int mipLevel, CubemapFace cubemapFace) =>
            SetRenderTarget(color, depth, mipLevel, cubemapFace, 0);
        public void SetRenderTarget(RenderTargetIdentifier color, RenderTargetIdentifier depth, int mipLevel, CubemapFace cubemapFace, int depthSlice) =>
            SetTarget(new[] { new RenderTargetIdentifier(color, mipLevel, cubemapFace, depthSlice) }, new RenderTargetIdentifier(depth, mipLevel, cubemapFace, depthSlice), true);
        public void SetRenderTarget(RenderTargetIdentifier color, RenderBufferLoadAction colorLoadAction, RenderBufferStoreAction colorStoreAction,
                                    RenderTargetIdentifier depth, RenderBufferLoadAction depthLoadAction, RenderBufferStoreAction depthStoreAction) =>
            SetTarget(new[] { color }, depth, true, colorLoadAction, colorStoreAction);
        public void SetRenderTarget(RenderTargetIdentifier[] colors, RenderTargetIdentifier depth) => SetTarget(colors, depth, true);
        public void SetRenderTarget(RenderTargetIdentifier[] colors, RenderTargetIdentifier depth, int mipLevel, CubemapFace cubemapFace, int depthSlice)
        {
            var at = new RenderTargetIdentifier[colors.Length];
            for (int i = 0; i < colors.Length; i++)
                at[i] = new RenderTargetIdentifier(colors[i], mipLevel, cubemapFace, depthSlice);
            SetTarget(at, new RenderTargetIdentifier(depth, mipLevel, cubemapFace, depthSlice), true);
        }

        public void GetTemporaryRT(int nameID, RenderTextureDescriptor desc, FilterMode filter) => RenderPipelineNative.GetTemporaryRT(Id, nameID, desc.Data, filter);
        public void GetTemporaryRT(int nameID, RenderTextureDescriptor desc) => GetTemporaryRT(nameID, desc, FilterMode.Point);
        public void GetTemporaryRT(int nameID, int width, int height, int depthBuffer = 0, FilterMode filter = FilterMode.Point,
                                   RenderTextureFormat format = RenderTextureFormat.Default, RenderTextureReadWrite readWrite = RenderTextureReadWrite.Default,
                                   int antiAliasing = 1, bool enableRandomWrite = false)
        {
            var desc = new RenderTextureDescriptor(width, height) { sRGB = readWrite == RenderTextureReadWrite.sRGB };
            desc.colorFormat = format;
            desc.depthBufferBits = depthBuffer;
            desc.msaaSamples = antiAliasing < 1 ? 1 : antiAliasing;
            desc.enableRandomWrite = enableRandomWrite;
            GetTemporaryRT(nameID, desc, filter);
        }
        public void GetTemporaryRT(int nameID, int width, int height, int depthBuffer, FilterMode filter, GraphicsFormat format, int antiAliasing = 1,
                                   bool enableRandomWrite = false)
        {
            var desc = new RenderTextureDescriptor(width, height, format, depthBuffer) { msaaSamples = antiAliasing < 1 ? 1 : antiAliasing, enableRandomWrite = enableRandomWrite };
            GetTemporaryRT(nameID, desc, filter);
        }
        public void GetTemporaryRTArray(int nameID, int width, int height, int slices, int depthBuffer = 0, FilterMode filter = FilterMode.Point,
                                        RenderTextureFormat format = RenderTextureFormat.Default, RenderTextureReadWrite readWrite = RenderTextureReadWrite.Default,
                                        int antiAliasing = 1, bool enableRandomWrite = false)
        {
            var desc = new RenderTextureDescriptor(width, height) { sRGB = readWrite == RenderTextureReadWrite.sRGB, dimension = TextureDimension.Tex2DArray };
            desc.volumeDepth = slices < 1 ? 1 : slices;
            desc.colorFormat = format;
            desc.depthBufferBits = depthBuffer;
            desc.msaaSamples = antiAliasing < 1 ? 1 : antiAliasing;
            desc.enableRandomWrite = enableRandomWrite;
            GetTemporaryRT(nameID, desc, filter);
        }
        public void GetTemporaryRTArray(int nameID, int width, int height, int slices, int depthBuffer, FilterMode filter, GraphicsFormat format,
                                        int antiAliasing = 1, bool enableRandomWrite = false, bool useDynamicScale = false)
        {
            var desc = new RenderTextureDescriptor(width, height, format, depthBuffer)
            {
                dimension = TextureDimension.Tex2DArray, volumeDepth = slices < 1 ? 1 : slices, msaaSamples = antiAliasing < 1 ? 1 : antiAliasing,
                enableRandomWrite = enableRandomWrite, useDynamicScale = useDynamicScale,
            };
            GetTemporaryRT(nameID, desc, filter);
        }
        public void ReleaseTemporaryRT(int nameID) => RenderPipelineNative.ReleaseTemporaryRT(Id, nameID);

        public void Blit(RenderTargetIdentifier source, RenderTargetIdentifier dest) => Blit(source, dest, null, -1);
        public void Blit(RenderTargetIdentifier source, RenderTargetIdentifier dest, Material mat) => Blit(source, dest, mat, -1);
        public void Blit(RenderTargetIdentifier source, RenderTargetIdentifier dest, Material mat, int pass) =>
            RenderPipelineNative.Blit(Id, source.Data, dest.Data, mat, pass, new Vector4(1, 1, 0, 0));
        public void Blit(RenderTargetIdentifier source, RenderTargetIdentifier dest, Vector2 scale, Vector2 offset) =>
            RenderPipelineNative.Blit(Id, source.Data, dest.Data, null, -1, new Vector4(scale.x, scale.y, offset.x, offset.y));
        public void Blit(Texture source, RenderTargetIdentifier dest) => Blit(new RenderTargetIdentifier(source), dest, null, -1);
        public void Blit(Texture source, RenderTargetIdentifier dest, Material mat) => Blit(new RenderTargetIdentifier(source), dest, mat, -1);
        public void Blit(Texture source, RenderTargetIdentifier dest, Material mat, int pass) => Blit(new RenderTargetIdentifier(source), dest, mat, pass);

        public void CopyTexture(RenderTargetIdentifier src, RenderTargetIdentifier dst) => RenderPipelineNative.CopyTexture(Id, src.Data, -1, -1, dst.Data, -1, -1);
        public void CopyTexture(RenderTargetIdentifier src, int srcElement, RenderTargetIdentifier dst, int dstElement) =>
            RenderPipelineNative.CopyTexture(Id, src.Data, srcElement, 0, dst.Data, dstElement, 0);
        public void CopyTexture(RenderTargetIdentifier src, int srcElement, int srcMip, RenderTargetIdentifier dst, int dstElement, int dstMip) =>
            RenderPipelineNative.CopyTexture(Id, src.Data, srcElement, srcMip, dst.Data, dstElement, dstMip);

        public void ResolveAntiAliasedSurface(RenderTexture rt, RenderTexture target = null)
        {
            if (rt == null)
                throw new ArgumentNullException(nameof(rt));
            RenderPipelineNative.ResolveAntiAliasedSurface(Id, rt, target);
        }

        public void GenerateMips(RenderTargetIdentifier rt) => RenderPipelineNative.GenerateMips(Id, rt.Data);
        public void GenerateMips(RenderTexture rt)
        {
            if (rt == null)
                throw new ArgumentNullException(nameof(rt));
            GenerateMips(new RenderTargetIdentifier(rt));
        }

        public override string ToString() => $"{name} (CommandBuffer)";
    }
}

namespace HelixEngine
{
    public static class QualitySettings
    {
        /// <summary>The asset of the current quality level (null: GraphicsSettings.defaultRenderPipeline renders).</summary>
        public static RenderPipelineAsset renderPipeline
        {
            get => RenderPipelineNative.QualityRenderPipeline() as RenderPipelineAsset;
            set => RenderPipelineNative.SetQualityRenderPipeline(value);
        }

        public static RenderPipelineAsset GetRenderPipelineAssetAt(int index) => RenderPipelineNative.GetRenderPipelineAssetAt(index) as RenderPipelineAsset;

        /// <summary>Screen heights of LOD groups are multiplied by it (above 1: detailed levels further away).</summary>
        public static float lodBias { get => CullingNative.LodBias(); set => CullingNative.SetLodBias(value); }
        /// <summary>Levels below it are not drawn (the next coarser one is).</summary>
        public static int maximumLODLevel { get => CullingNative.MaximumLODLevel(); set => CullingNative.SetMaximumLODLevel(value); }
    }

    public partial class Camera
    {
        /// <summary>The camera's frustum planes, culling mask and position for ScriptableRenderContext.Cull; false for a camera that draws nothing.</summary>
        public bool TryGetCullingParameters(out ScriptableCullingParameters cullingParameters) => TryGetCullingParameters(false, out cullingParameters);

        public bool TryGetCullingParameters(bool stereoAware, out ScriptableCullingParameters cullingParameters)
        {
            cullingParameters = default;
            Rect pr = pixelRect;
            if (pr.width <= 0 || pr.height <= 0 || nearClipPlane >= farClipPlane)
                return false;
            Matrix4x4 m = cullingMatrix;
            cullingParameters.cullingMask = (uint)cullingMask;
            cullingParameters.cullingMatrix = m;
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(m);
            cullingParameters.cullingPlaneCount = planes.Length;
            for (int i = 0; i < planes.Length; i++)
                cullingParameters.SetCullingPlane(i, planes[i]);
            cullingParameters.origin = transform.position;
            cullingParameters.isOrthographic = orthographic;
            cullingParameters.m_Camera = this;
            cullingParameters.cullingOptions = CullingOptions.NeedsLighting | CullingOptions.NeedsReflectionProbes | CullingOptions.ShadowCasters |
                                               (useOcclusionCulling ? CullingOptions.OcclusionCull : CullingOptions.None);
            cullingParameters.lodParameters = new LODParameters
            {
                isOrthographic = orthographic, cameraPosition = transform.position, fieldOfView = fieldOfView, orthoSize = orthographicSize,
                cameraPixelHeight = (int)pr.height,
            };
            cullingParameters.maximumVisibleLights = -1;
            float[] distances = layerCullDistances;
            for (int i = 0; i < 32 && i < distances.Length; i++)
                cullingParameters.SetLayerCullDistance(i, distances[i]);
            cullingParameters.layerCullSpherical = layerCullSpherical;
            return true;
        }
    }

    public static partial class Graphics
    {
        /// <summary>Runs a command buffer's commands now, into the engine's target.</summary>
        public static void ExecuteCommandBuffer(CommandBuffer buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            RenderPipelineNative.ExecuteNow(buffer.Id);
        }

        /// <summary>The devices have one queue: the commands run now, in order.</summary>
        public static void ExecuteCommandBufferAsync(CommandBuffer buffer, ComputeQueueType queueType) => ExecuteCommandBuffer(buffer);
    }
}

namespace HelixEngine.Internal
{
    /// <summary>The C# side of pipelines: the engine makes, renders and disposes C# pipelines and raises the manager's events through these.</summary>
    static unsafe class RenderPipelineBridge
    {
        internal static void Start(Func<string, IntPtr> export)
        {
            var set = (delegate* unmanaged[Cdecl]<void**, uint, byte>)export("HxEngineRendering_SetManaged");
            if (set == null)
                return;
            void** table = (void**)NativeMemory.Alloc((nuint)(6 * sizeof(void*)));
            table[0] = (void*)(delegate* unmanaged[Cdecl]<ulong, ulong>)&Create;
            table[1] = (void*)(delegate* unmanaged[Cdecl]<ulong, ulong, void>)&Render;
            table[2] = (void*)(delegate* unmanaged[Cdecl]<ulong, void>)&Dispose;
            table[3] = (void*)(delegate* unmanaged[Cdecl]<uint, ulong, void>)&Raise;
            table[4] = (void*)(delegate* unmanaged[Cdecl]<ulong, ulong, byte>)&Supports;
            table[5] = (void*)(delegate* unmanaged[Cdecl]<ulong, ulong, ulong, void>)&Process;
            set(table, 6);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static ulong Create(ulong assetHandle)
        {
            try
            {
                if (GCHandle.FromIntPtr((IntPtr)assetHandle).Target is not RenderPipelineAsset asset)
                {
                    Debug.LogError("A pipeline asset's script derives from RenderPipelineAsset.");
                    return 0;
                }
                RenderPipeline pipeline = asset.InternalCreatePipeline();
                if (pipeline == null)
                {
                    Debug.LogError($"{asset.GetType().Name}.CreatePipeline returned null.");
                    return 0;
                }
                RenderPipelineManager.s_CurrentPipeline = pipeline;
                return (ulong)GCHandle.ToIntPtr(GCHandle.Alloc(pipeline));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return 0;
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void Render(ulong handle, ulong context)
        {
            try
            {
                if (GCHandle.FromIntPtr((IntPtr)handle).Target is RenderPipeline pipeline)
                    pipeline.InternalRender(new ScriptableRenderContext(context), new List<Camera>(RenderPipelineNative.ContextCameras(context)));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void Dispose(ulong handle)
        {
            try
            {
                GCHandle h = GCHandle.FromIntPtr((IntPtr)handle);
                var pipeline = h.Target as RenderPipeline;
                h.Free();
                if (pipeline == null)
                    return;
                if (ReferenceEquals(RenderPipelineManager.s_CurrentPipeline, pipeline))
                    RenderPipelineManager.s_CurrentPipeline = null;
                pipeline.InternalDispose();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // The request: its object (a GC handle), or the engine's standard request.
        static object RequestOf(ulong request)
        {
            if (request != 0)
                return GCHandle.FromIntPtr((IntPtr)request).Target;
            return new RenderPipeline.StandardRequest
            {
                destination = RenderPipelineNative.EventRequestDestination(),
                mipLevel = RenderPipelineNative.EventRequestMip(),
                face = (CubemapFace)RenderPipelineNative.EventRequestFace(),
                slice = RenderPipelineNative.EventRequestSlice(),
            };
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static byte Supports(ulong handle, ulong request)
        {
            try
            {
                return GCHandle.FromIntPtr((IntPtr)handle).Target is RenderPipeline pipeline &&
                       pipeline.InternalSupports(RenderPipelineNative.EventCamera(), RequestOf(request))
                    ? (byte)1
                    : (byte)0;
            }
            catch (Exception e)
            {
                Debug.LogException(e is System.Reflection.TargetInvocationException t && t.InnerException != null ? t.InnerException : e);
                return 0;
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void Process(ulong handle, ulong context, ulong request)
        {
            try
            {
                if (GCHandle.FromIntPtr((IntPtr)handle).Target is RenderPipeline pipeline)
                    pipeline.InternalProcess(new ScriptableRenderContext(context), RenderPipelineNative.EventCamera(), RequestOf(request));
            }
            catch (Exception e)
            {
                Debug.LogException(e is System.Reflection.TargetInvocationException t && t.InnerException != null ? t.InnerException : e);
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void Raise(uint kind, ulong context)
        {
            try
            {
                RenderPipelineManager.Raise(kind, context);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
