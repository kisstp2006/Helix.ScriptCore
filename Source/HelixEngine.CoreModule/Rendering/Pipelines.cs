using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
            ulong results = RenderPipelineNative.Cull(m_Ptr, parameters.m_Camera, (int)parameters.cullingMask, parameters.cullingMatrix, parameters.Planes(),
                                                      (int)parameters.cullingOptions, parameters.origin, parameters.isOrthographic);
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
            RenderPipelineNative.DrawRenderers(m_Ptr, cullingResults.ptr, data, drawingSettings.PassNames(), drawingSettings.overrideMaterial,
                                               drawingSettings.overrideShader, drawingSettings.fallbackMaterial);
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
        public void SetGlobalMatrix(string name, Matrix4x4 value) => SetGlobalMatrix(Shader.PropertyToID(name), value);
        public void SetGlobalMatrix(int nameID, Matrix4x4 value) => RenderPipelineNative.SetGlobalMatrix(Id, nameID, value);
        public void SetGlobalTexture(string name, Texture value) => SetGlobalTexture(Shader.PropertyToID(name), value);
        public void SetGlobalTexture(int nameID, Texture value) => RenderPipelineNative.SetGlobalTexture(Id, nameID, value);

        public void EnableShaderKeyword(string keyword) => RenderPipelineNative.SetKeyword(Id, keyword, true);
        public void DisableShaderKeyword(string keyword) => RenderPipelineNative.SetKeyword(Id, keyword, false);
        public void EnableKeyword(in GlobalKeyword keyword) => RenderPipelineNative.SetKeyword(Id, keyword.name, true);
        public void DisableKeyword(in GlobalKeyword keyword) => RenderPipelineNative.SetKeyword(Id, keyword.name, false);
        public void SetKeyword(in GlobalKeyword keyword, bool value) => RenderPipelineNative.SetKeyword(Id, keyword.name, value);

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
            void** table = (void**)NativeMemory.Alloc((nuint)(4 * sizeof(void*)));
            table[0] = (void*)(delegate* unmanaged[Cdecl]<ulong, ulong>)&Create;
            table[1] = (void*)(delegate* unmanaged[Cdecl]<ulong, ulong, void>)&Render;
            table[2] = (void*)(delegate* unmanaged[Cdecl]<ulong, void>)&Dispose;
            table[3] = (void*)(delegate* unmanaged[Cdecl]<uint, ulong, void>)&Raise;
            set(table, 4);
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
