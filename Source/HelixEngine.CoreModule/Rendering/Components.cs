using System;
using System.Collections.Generic;
using HelixEngine.Internal;
using HelixEngine.Rendering;

namespace HelixEngine
{
    // The property block and List overloads of Renderer.
    public partial class Renderer
    {
        /// <summary>Values on top of the materials; null removes them.</summary>
        public void SetPropertyBlock(MaterialPropertyBlock properties) => SetPropertyBlock(properties?.m_Block ?? default);
        public void SetPropertyBlock(MaterialPropertyBlock properties, int materialIndex) => SetPropertyBlock(properties?.m_Block ?? default, materialIndex);

        public void GetPropertyBlock(MaterialPropertyBlock properties)
        {
            if (properties == null)
                throw new ArgumentNullException(nameof(properties));
            GetPropertyBlock(properties.m_Block);
        }

        public void GetPropertyBlock(MaterialPropertyBlock properties, int materialIndex)
        {
            if (properties == null)
                throw new ArgumentNullException(nameof(properties));
            GetPropertyBlock(properties.m_Block, materialIndex);
        }

        public void GetMaterials(List<Material> m) => Fill(m, materials);
        public void GetSharedMaterials(List<Material> m) => Fill(m, sharedMaterials);
        public void SetMaterials(List<Material> materials) => this.materials = Items(materials);
        public void SetSharedMaterials(List<Material> materials) => sharedMaterials = Items(materials);

        static Material[] Items(List<Material> list)
        {
            if (list == null)
                throw new ArgumentNullException("materials");
            return list.ToArray();
        }

        static void Fill(List<Material> to, Material[] from)
        {
            if (to == null)
                throw new ArgumentNullException("m");
            to.Clear();
            to.AddRange(from);
        }
    }

    // The overloads of Camera that C# has on top of the native members.
    public partial class Camera
    {
        /// <summary>The eye of a stereo camera; a camera that is no stereo camera takes Mono for any.</summary>
        public enum MonoOrStereoscopicEye
        {
            Left = 0,
            Right = 1,
            Mono = 2,
        }

        public Vector3 WorldToScreenPoint(Vector3 position, MonoOrStereoscopicEye eye) => WorldToScreenPoint(position);
        public Vector3 WorldToViewportPoint(Vector3 position, MonoOrStereoscopicEye eye) => WorldToViewportPoint(position);
        public Vector3 ViewportToWorldPoint(Vector3 position, MonoOrStereoscopicEye eye) => ViewportToWorldPoint(position);
        public Vector3 ScreenToWorldPoint(Vector3 position, MonoOrStereoscopicEye eye) => ScreenToWorldPoint(position);
        public Ray ViewportPointToRay(Vector3 pos, MonoOrStereoscopicEye eye) => ViewportPointToRay(pos);
        public Ray ScreenPointToRay(Vector3 pos, MonoOrStereoscopicEye eye) => ScreenPointToRay(pos);

        /// <summary>The enabled cameras into the array; their count (at most the array's length).</summary>
        public static int GetAllCameras(Camera[] cameras)
        {
            if (cameras == null)
                throw new NullReferenceException();
            if (cameras.Length < allCamerasCount)
                throw new ArgumentException("Passed in array to fill with cameras is to small to hold the number of cameras. Use Camera.allCamerasCount to get the needed size.");
            Camera[] all = allCameras;
            Array.Copy(all, cameras, all.Length);
            return all.Length;
        }

        /// <summary>The corners of the viewport rectangle at distance z in camera space (bottom left, top left, top right, bottom right).</summary>
        public void CalculateFrustumCorners(Rect viewport, float z, MonoOrStereoscopicEye eye, Vector3[] outCorners)
        {
            if (outCorners == null)
                throw new ArgumentNullException(nameof(outCorners));
            if (outCorners.Length < 4)
                throw new ArgumentException("outCorners minimum size is 4", nameof(outCorners));
            Array.Copy(FrustumCorners(viewport, z), outCorners, 4);
        }
    }

    // The filling overloads of GeometryUtility.
    public static partial class GeometryUtility
    {
        public static void CalculateFrustumPlanes(Camera camera, Plane[] planes) => Copy(CalculateFrustumPlanes(camera), planes);
        public static void CalculateFrustumPlanes(Matrix4x4 worldToProjectionMatrix, Plane[] planes) => Copy(CalculateFrustumPlanes(worldToProjectionMatrix), planes);

        static void Copy(Plane[] from, Plane[] to)
        {
            if (to == null)
                throw new ArgumentNullException("planes");
            if (to.Length != 6)
                throw new ArgumentException("Planes array must be of length 6.", "planes");
            Array.Copy(from, to, 6);
        }
    }

    /// <summary>How Graphics.RenderMesh draws a mesh.</summary>
    public struct RenderParams
    {
        public RenderParams(Material mat)
        {
            layer = 0;
            renderingLayerMask = 1;
            rendererPriority = 0;
            worldBounds = new Bounds(Vector3.zero, Vector3.zero);
            camera = null;
            material = mat;
            matProps = null;
            shadowCastingMode = ShadowCastingMode.Off;
            receiveShadows = false;
        }

        /// <summary>The layer index cameras' culling masks test.</summary>
        public int layer { get; set; }
        public uint renderingLayerMask { get; set; }
        public int rendererPriority { get; set; }
        /// <summary>Zero size: the mesh's bounds under the matrix.</summary>
        public Bounds worldBounds { get; set; }
        /// <summary>Null draws in every camera.</summary>
        public Camera camera { get; set; }
        public Material material { get; set; }
        public MaterialPropertyBlock matProps { get; set; }
        public ShadowCastingMode shadowCastingMode { get; set; }
        public bool receiveShadows { get; set; }
    }

    /// <summary>Drawing outside of renderers.</summary>
    public static partial class Graphics
    {
        /// <summary>A submesh drawn by this frame's cameras (or the camera of the params).</summary>
        public static void RenderMesh(in RenderParams rparams, Mesh mesh, int submeshIndex, Matrix4x4 objectToWorld, Matrix4x4? prevObjectToWorld = null)
        {
            if (mesh == null)
                throw new ArgumentNullException(nameof(mesh));
            GraphicsNative.RenderMesh(rparams.material, rparams.camera, rparams.matProps?.m_Block ?? default, rparams.layer, rparams.renderingLayerMask,
                                      rparams.rendererPriority, rparams.worldBounds, rparams.shadowCastingMode, rparams.receiveShadows, mesh, submeshIndex,
                                      objectToWorld);
        }

        public static void DrawMesh(Mesh mesh, Vector3 position, Quaternion rotation, Material material, int layer, Camera camera = null, int submeshIndex = 0,
                                    MaterialPropertyBlock properties = null, bool castShadows = true, bool receiveShadows = true, bool useLightProbes = true) =>
            DrawMesh(mesh, Matrix4x4.TRS(position, rotation, Vector3.one), material, layer, camera, submeshIndex, properties,
                     castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off, receiveShadows, null, useLightProbes);

        public static void DrawMesh(Mesh mesh, Vector3 position, Quaternion rotation, Material material, int layer, Camera camera, int submeshIndex,
                                    MaterialPropertyBlock properties, ShadowCastingMode castShadows, bool receiveShadows = true, Transform probeAnchor = null,
                                    bool useLightProbes = true) =>
            DrawMesh(mesh, Matrix4x4.TRS(position, rotation, Vector3.one), material, layer, camera, submeshIndex, properties, castShadows, receiveShadows,
                     probeAnchor, useLightProbes);

        public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer, Camera camera = null, int submeshIndex = 0,
                                    MaterialPropertyBlock properties = null, bool castShadows = true, bool receiveShadows = true, bool useLightProbes = true) =>
            DrawMesh(mesh, matrix, material, layer, camera, submeshIndex, properties, castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off, receiveShadows,
                     null, useLightProbes);

        public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer, Camera camera, int submeshIndex, MaterialPropertyBlock properties,
                                    ShadowCastingMode castShadows, bool receiveShadows = true, Transform probeAnchor = null, bool useLightProbes = true)
        {
            if (mesh == null)
                throw new ArgumentNullException(nameof(mesh));
            // Light probes come with M7's pipelines.
            var p = new RenderParams(material)
            {
                layer = layer,
                camera = camera,
                matProps = properties,
                shadowCastingMode = castShadows,
                receiveShadows = receiveShadows,
            };
            RenderMesh(p, mesh, submeshIndex, matrix);
        }
    }
}
