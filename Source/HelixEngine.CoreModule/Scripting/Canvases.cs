using System;
using System.Collections.Generic;

namespace HelixEngine
{
    /// <summary>Asks an element whether a raycast at the screen point hits it (CanvasGroup, masks and images answer).</summary>
    public interface ICanvasRaycastFilter
    {
        bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera);
    }

    public partial class CanvasGroup : ICanvasRaycastFilter
    {
    }

    public partial class Canvas
    {
        public delegate void WillRenderCanvases();

        static event WillRenderCanvases s_PreWill, s_Will;
        // Hooked to the native events while there are listeners.
        static readonly Action s_PreForward = () => s_PreWill?.Invoke(), s_Forward = () => s_Will?.Invoke();

        /// <summary>Raised before willRenderCanvases, each frame before the canvases batch.</summary>
        public static event WillRenderCanvases preWillRenderCanvases
        {
            add
            {
                if (s_PreWill == null)
                    preWillRenderCanvasesInternal += s_PreForward;
                s_PreWill += value;
            }
            remove
            {
                s_PreWill -= value;
                if (s_PreWill == null)
                    preWillRenderCanvasesInternal -= s_PreForward;
            }
        }

        /// <summary>Raised each frame before the canvases batch (the layout and the graphics' rebuilds run here).</summary>
        public static event WillRenderCanvases willRenderCanvases
        {
            add
            {
                if (s_Will == null)
                    willRenderCanvasesInternal += s_Forward;
                s_Will += value;
            }
            remove
            {
                s_Will -= value;
                if (s_Will == null)
                    willRenderCanvasesInternal -= s_Forward;
            }
        }

        [Obsolete("Shared default material now used for text and general UI elements, call Canvas.GetDefaultCanvasMaterial()")]
        public static Material GetDefaultCanvasTextMaterial() => GetDefaultCanvasTextMaterialInternal();

        [Obsolete("Setting normalizedSize via a int is not supported. Please use normalizedSortingGridSize")]
        public int sortingGridNormalizedSize
        {
            get => (int)normalizedSortingGridSize;
            set => normalizedSortingGridSize = value;
        }
    }

    public partial struct UIVertex
    {
        /// <summary>A white vertex at the origin facing -z.</summary>
        public static UIVertex simpleVert = new UIVertex
        {
            position = Vector3.zero,
            normal = Vector3.back,
            tangent = new Vector4(1, 0, 0, -1),
            color = new Color32(255, 255, 255, 255),
            uv0 = Vector4.zero,
            uv1 = Vector4.zero,
            uv2 = Vector4.zero,
            uv3 = Vector4.zero,
        };
    }

    public partial class CanvasRenderer
    {
        public delegate void OnRequestRebuild();

        static event OnRequestRebuild s_Rebuild;
        static readonly Action s_RebuildForward = () => s_Rebuild?.Invoke();

        /// <summary>Raised when the graphics must give their geometry again (the device's resources went).</summary>
        public static event OnRequestRebuild onRequestRebuild
        {
            add
            {
                if (s_Rebuild == null)
                    onRequestRebuildInternal += s_RebuildForward;
                s_Rebuild += value;
            }
            remove
            {
                s_Rebuild -= value;
                if (s_Rebuild == null)
                    onRequestRebuildInternal -= s_RebuildForward;
            }
        }

        [Obsolete("isMask is no longer supported. See EnableClipping for vertex clipping configuration", false)]
        public bool isMask { get; set; }

        [Obsolete("UI System now uses meshes. Generate a mesh and use 'SetMesh' instead", false)]
        public void SetVertices(List<UIVertex> vertices) => SetVertices(vertices.ToArray(), vertices.Count);

        // -------------------------------------------------------------------------------------------- vertex streams

        static bool Same(in UIVertex a, in UIVertex b) =>
            a.position == b.position && a.normal == b.normal && a.tangent == b.tangent && a.color.r == b.color.r && a.color.g == b.color.g &&
            a.color.b == b.color.b && a.color.a == b.color.a && a.uv0 == b.uv0 && a.uv1 == b.uv1 && a.uv2 == b.uv2 && a.uv3 == b.uv3;

        /// <summary>A triangle list of vertices into separate streams and indices (a vertex repeated within the last few kept once).</summary>
        public static void SplitUIVertexStreams(List<UIVertex> verts, List<Vector3> positions, List<Color32> colors, List<Vector4> uv0S, List<Vector4> uv1S,
                                                List<Vector4> uv2S, List<Vector4> uv3S, List<Vector3> normals, List<Vector4> tangents, List<int> indices)
        {
            positions.Clear(); colors.Clear(); uv0S.Clear(); uv1S.Clear(); uv2S?.Clear(); uv3S?.Clear(); normals.Clear(); tangents.Clear(); indices.Clear();
            for (int i = 0; i < verts.Count; i++)
            {
                UIVertex v = verts[i];
                int found = -1;
                for (int k = positions.Count - 1; k >= 0 && k >= positions.Count - 8 && found < 0; k--)
                {
                    var kept = new UIVertex
                    {
                        position = positions[k], color = colors[k], uv0 = uv0S[k], uv1 = uv1S[k], uv2 = uv2S != null ? uv2S[k] : Vector4.zero,
                        uv3 = uv3S != null ? uv3S[k] : Vector4.zero, normal = normals[k], tangent = tangents[k],
                    };
                    if (uv2S == null)
                        kept.uv2 = v.uv2;
                    if (uv3S == null)
                        kept.uv3 = v.uv3;
                    if (Same(kept, v))
                        found = k;
                }
                if (found < 0)
                {
                    found = positions.Count;
                    positions.Add(v.position); colors.Add(v.color); uv0S.Add(v.uv0); uv1S.Add(v.uv1); uv2S?.Add(v.uv2); uv3S?.Add(v.uv3);
                    normals.Add(v.normal); tangents.Add(v.tangent);
                }
                indices.Add(found);
            }
        }

        public static void SplitUIVertexStreams(List<UIVertex> verts, List<Vector3> positions, List<Color32> colors, List<Vector4> uv0S, List<Vector4> uv1S,
                                                List<Vector3> normals, List<Vector4> tangents, List<int> indices) =>
            SplitUIVertexStreams(verts, positions, colors, uv0S, uv1S, null, null, normals, tangents, indices);

        static UIVertex At(int k, List<Vector3> positions, List<Color32> colors, List<Vector4> uv0S, List<Vector4> uv1S, List<Vector4> uv2S, List<Vector4> uv3S,
                           List<Vector3> normals, List<Vector4> tangents)
        {
            UIVertex v = UIVertex.simpleVert;
            v.position = positions[k];
            if (colors != null && k < colors.Count) v.color = colors[k];
            if (uv0S != null && k < uv0S.Count) v.uv0 = uv0S[k];
            if (uv1S != null && k < uv1S.Count) v.uv1 = uv1S[k];
            if (uv2S != null && k < uv2S.Count) v.uv2 = uv2S[k];
            if (uv3S != null && k < uv3S.Count) v.uv3 = uv3S[k];
            if (normals != null && k < normals.Count) v.normal = normals[k];
            if (tangents != null && k < tangents.Count) v.tangent = tangents[k];
            return v;
        }

        /// <summary>The streams and indices back into a triangle list of vertices.</summary>
        public static void CreateUIVertexStream(List<UIVertex> verts, List<Vector3> positions, List<Color32> colors, List<Vector4> uv0S, List<Vector4> uv1S,
                                                List<Vector4> uv2S, List<Vector4> uv3S, List<Vector3> normals, List<Vector4> tangents, List<int> indices)
        {
            verts.Clear();
            foreach (int i in indices)
                if (i >= 0 && i < positions.Count)
                    verts.Add(At(i, positions, colors, uv0S, uv1S, uv2S, uv3S, normals, tangents));
        }

        public static void CreateUIVertexStream(List<UIVertex> verts, List<Vector3> positions, List<Color32> colors, List<Vector4> uv0S, List<Vector4> uv1S,
                                                List<Vector3> normals, List<Vector4> tangents, List<int> indices) =>
            CreateUIVertexStream(verts, positions, colors, uv0S, uv1S, null, null, normals, tangents, indices);

        /// <summary>The streams appended to the vertices, one vertex each.</summary>
        public static void AddUIVertexStream(List<UIVertex> verts, List<Vector3> positions, List<Color32> colors, List<Vector4> uv0S, List<Vector4> uv1S,
                                             List<Vector4> uv2S, List<Vector4> uv3S, List<Vector3> normals, List<Vector4> tangents)
        {
            for (int k = 0; k < positions.Count; k++)
                verts.Add(At(k, positions, colors, uv0S, uv1S, uv2S, uv3S, normals, tangents));
        }

        public static void AddUIVertexStream(List<UIVertex> verts, List<Vector3> positions, List<Color32> colors, List<Vector4> uv0S, List<Vector4> uv1S,
                                             List<Vector3> normals, List<Vector4> tangents) =>
            AddUIVertexStream(verts, positions, colors, uv0S, uv1S, null, null, normals, tangents);
    }
}
