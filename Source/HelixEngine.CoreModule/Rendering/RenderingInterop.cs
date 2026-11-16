using System;
using System.Runtime.InteropServices;
using HelixEngine;
using HelixEngine.Rendering;
using HelixEngine.Rendering.RendererUtils;

namespace Helix.Interop
{
    /// <summary>
    /// The native ids of the engine's rendering objects, for the C# sides of native modules (the render pipelines) that
    /// hand them to their native code, and C# objects over native ones the module's native code made.
    /// </summary>
    public static class RenderingInterop
    {
        /// <summary>The native buffer of a graphics or compute buffer (0: null).</summary>
        public static ulong IdOf(GraphicsBuffer buffer) => buffer?.m_Ptr ?? 0;

        public static ulong IdOf(ComputeBuffer buffer) => buffer?.m_Ptr ?? 0;

        /// <summary>A graphics buffer over a native one native code made and keeps (0: null): Release leaves it be.</summary>
        public static GraphicsBuffer BorrowGraphicsBuffer(ulong buffer) => buffer != 0 ? new GraphicsBuffer(buffer) : null;

        /// <summary>The native buffer of a command buffer.</summary>
        public static ulong IdOf(CommandBuffer buffer) => buffer?.Id ?? 0;

        /// <summary>A command buffer over a native buffer native code made and keeps: Release and Dispose leave it be,
        /// it is valid while the native buffer is.</summary>
        public static CommandBuffer Borrow(ulong buffer) => buffer != 0 ? new CommandBuffer(buffer) : null;

        public static ulong IdOf(ScriptableRenderContext context) => context.id;

        public static ScriptableRenderContext ContextOf(ulong context) => new ScriptableRenderContext(context);

        public static ulong IdOf(RendererList list) => list.m_Id;

        public static RendererList RendererListOf(ulong list) => new RendererList(list);

        public static ulong IdOf(CullingResults culling) => culling.ptr;

        public static CullingResults CullingResultsOf(ulong culling) => new CullingResults(culling);

        /// <summary>The descriptor native code keeps at an address (the engine's plain form of it).</summary>
        public static unsafe RenderTextureDescriptor DescriptorAt(ulong address) =>
            address != 0 ? RenderTextureDescriptor.From(*(HelixEngine.Internal.RenderTextureDescriptorData*)address) : default;

        public static unsafe void WriteDescriptorAt(ulong address, RenderTextureDescriptor descriptor)
        {
            if (address != 0)
                *(HelixEngine.Internal.RenderTextureDescriptorData*)address = descriptor.Data;
        }

        // The engine's ScriptableCullingParameters as it lies in native memory (the camera by its object id).
        [StructLayout(LayoutKind.Sequential)]
        unsafe struct NativeCullingParameters
        {
            public int cullingMask;
            public Matrix4x4 cullingMatrix;
            public fixed float planes[40];
            public int cullingPlaneCount;
            public int cullingOptions;
            public Vector3 origin;
            public byte isOrthographic;
            public ulong camera;
            public byte lodIsOrthographic;
            public Vector3 lodCameraPosition;
            public float lodFieldOfView;
            public float lodOrthoSize;
            public int lodCameraPixelHeight;
            public int maximumVisibleLights;
            public fixed float layerCullDistances[32];
            public byte layerCullSpherical;
            public float shadowDistance;
            public float shadowNearPlaneOffset;
            public int reflectionProbeSortingCriteria;
            public byte conservativeEnclosingSphere;
            public int numIterationsEnclosingSphere;
        }

        static int s_CullingSize = -1;

        /// <summary>The size of the culling parameters at a native address (what to reserve for them).</summary>
        public static int CullingParametersSize => HelixEngine.Internal.RenderPipelineNative.CullingParametersSize();

        static unsafe NativeCullingParameters* CullingAt(ulong address)
        {
            if (s_CullingSize < 0)
                s_CullingSize = HelixEngine.Internal.RenderPipelineNative.CullingParametersSize();
            if (s_CullingSize != sizeof(NativeCullingParameters))
                throw new InvalidOperationException($"ScriptableCullingParameters: the native size {s_CullingSize} differs from C#'s {sizeof(NativeCullingParameters)}");
            return (NativeCullingParameters*)address;
        }

        /// <summary>The culling parameters native code keeps at an address.</summary>
        public static unsafe ScriptableCullingParameters CullingParametersAt(ulong address)
        {
            var p = new ScriptableCullingParameters();
            if (address == 0)
                return p;
            NativeCullingParameters* n = CullingAt(address);
            p.cullingMask = (uint)n->cullingMask;
            p.cullingMatrix = n->cullingMatrix;
            p.cullingPlaneCount = Math.Clamp(n->cullingPlaneCount, 0, ScriptableCullingParameters.maximumCullingPlaneCount);
            for (int i = 0; i < p.cullingPlaneCount; i++)
                p.SetCullingPlane(i, new Plane(new Vector3(n->planes[i * 4], n->planes[i * 4 + 1], n->planes[i * 4 + 2]), n->planes[i * 4 + 3]));
            p.cullingOptions = (CullingOptions)n->cullingOptions;
            p.origin = n->origin;
            p.isOrthographic = n->isOrthographic != 0;
            p.m_Camera = n->camera != 0 ? global::Helix.Interop.Objects.Wrap<HelixEngine.Object>(n->camera, global::Helix.Interop.Types.Of<Camera>()) as Camera : null;
            p.lodParameters = new LODParameters
            {
                isOrthographic = n->lodIsOrthographic != 0, cameraPosition = n->lodCameraPosition, fieldOfView = n->lodFieldOfView,
                orthoSize = n->lodOrthoSize, cameraPixelHeight = n->lodCameraPixelHeight,
            };
            p.maximumVisibleLights = n->maximumVisibleLights;
            for (int i = 0; i < 32; i++)
                p.SetLayerCullDistance(i, n->layerCullDistances[i]);
            p.layerCullSpherical = n->layerCullSpherical != 0;
            p.shadowDistance = n->shadowDistance;
            p.shadowNearPlaneOffset = n->shadowNearPlaneOffset;
            p.reflectionProbeSortingCriteria = (ReflectionProbeSortingCriteria)n->reflectionProbeSortingCriteria;
            p.conservativeEnclosingSphere = n->conservativeEnclosingSphere != 0;
            p.numIterationsEnclosingSphere = n->numIterationsEnclosingSphere;
            return p;
        }

        public static unsafe void WriteCullingParametersAt(ulong address, in ScriptableCullingParameters p)
        {
            if (address == 0)
                return;
            NativeCullingParameters* n = CullingAt(address);
            n->cullingMask = (int)p.cullingMask;
            n->cullingMatrix = p.cullingMatrix;
            n->cullingPlaneCount = p.cullingPlaneCount;
            for (int i = 0; i < ScriptableCullingParameters.maximumCullingPlaneCount; i++)
            {
                Plane plane = i < p.cullingPlaneCount ? p.GetCullingPlane(i) : default;
                n->planes[i * 4] = plane.normal.x;
                n->planes[i * 4 + 1] = plane.normal.y;
                n->planes[i * 4 + 2] = plane.normal.z;
                n->planes[i * 4 + 3] = plane.distance;
            }
            n->cullingOptions = (int)p.cullingOptions;
            n->origin = p.origin;
            n->isOrthographic = (byte)(p.isOrthographic ? 1 : 0);
            n->camera = global::Helix.Interop.Objects.IdOf(p.m_Camera);
            LODParameters lod = p.lodParameters;
            n->lodIsOrthographic = (byte)(lod.isOrthographic ? 1 : 0);
            n->lodCameraPosition = lod.cameraPosition;
            n->lodFieldOfView = lod.fieldOfView;
            n->lodOrthoSize = lod.orthoSize;
            n->lodCameraPixelHeight = lod.cameraPixelHeight;
            n->maximumVisibleLights = p.maximumVisibleLights;
            for (int i = 0; i < 32; i++)
                n->layerCullDistances[i] = p.GetLayerCullDistance(i);
            n->layerCullSpherical = (byte)(p.layerCullSpherical ? 1 : 0);
            n->shadowDistance = p.shadowDistance;
            n->shadowNearPlaneOffset = p.shadowNearPlaneOffset;
            n->reflectionProbeSortingCriteria = (int)p.reflectionProbeSortingCriteria;
            n->conservativeEnclosingSphere = (byte)(p.conservativeEnclosingSphere ? 1 : 0);
            n->numIterationsEnclosingSphere = p.numIterationsEnclosingSphere;
        }
    }
}
