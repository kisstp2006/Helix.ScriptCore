using System;
using System.Collections.Generic;
using HelixEngine.Internal;

namespace HelixEngine
{
    /// <summary>Drawings of the editor's scene view, made in OnDrawGizmos and OnDrawGizmosSelected: lines and wire shapes,
    /// solid shapes, icons, in the color and through the matrix set. Recorded only while the editor draws its gizmos
    /// (a player draws none).</summary>
    public static class Gizmos
    {
        public static Color color { get; set; } = Color.white;
        public static Matrix4x4 matrix { get; set; } = Matrix4x4.identity;
        public static bool enabled { get; set; } = true;
        public static Texture exposure { get; set; }
        public static float probeSize => 0.5f;

        static bool On => enabled && GizmoRecorder.recording;

        static Vector3 P(Vector3 p) => matrix.MultiplyPoint3x4(p);

        public static void DrawLine(Vector3 from, Vector3 to)
        {
            if (On)
                GizmoRecorder.Line(P(from), P(to), color);
        }

        public static void DrawRay(Vector3 from, Vector3 direction) => DrawLine(from, from + direction);
        public static void DrawRay(Ray r) => DrawLine(r.origin, r.origin + r.direction);

        /// <summary>Lines between each pair of points (0-1, 2-3 ...).</summary>
        public static void DrawLineList(ReadOnlySpan<Vector3> points)
        {
            for (int i = 0; i + 1 < points.Length; i += 2)
                DrawLine(points[i], points[i + 1]);
        }

        /// <summary>Lines between consecutive points, the last back to the first when looped.</summary>
        public static void DrawLineStrip(ReadOnlySpan<Vector3> points, bool looped)
        {
            for (int i = 0; i + 1 < points.Length; i++)
                DrawLine(points[i], points[i + 1]);
            if (looped && points.Length > 2)
                DrawLine(points[points.Length - 1], points[0]);
        }

        public static void DrawWireCube(Vector3 center, Vector3 size)
        {
            if (!On)
                return;
            Vector3 h = size * 0.5f;
            Span<Vector3> c = stackalloc Vector3[8];
            for (int i = 0; i < 8; i++)
                c[i] = P(center + new Vector3((i & 1) != 0 ? h.x : -h.x, (i & 2) != 0 ? h.y : -h.y, (i & 4) != 0 ? h.z : -h.z));
            for (int i = 0; i < 8; i++)
                for (int bit = 1; bit < 8; bit <<= 1)
                    if ((i & bit) == 0)
                        GizmoRecorder.Line(c[i], c[i | bit], color);
        }

        public static void DrawCube(Vector3 center, Vector3 size)
        {
            if (!On)
                return;
            Vector3 h = size * 0.5f;
            Vector3 V(int i) => P(center + new Vector3((i & 1) != 0 ? h.x : -h.x, (i & 2) != 0 ? h.y : -h.y, (i & 4) != 0 ? h.z : -h.z));
            // The six faces as two triangles each (corner bits: x 1, y 2, z 4).
            int[] faces = { 0, 2, 3, 1, 4, 5, 7, 6, 0, 1, 5, 4, 2, 6, 7, 3, 0, 4, 6, 2, 1, 3, 7, 5 };
            for (int f = 0; f < 24; f += 4)
            {
                GizmoRecorder.Triangle(V(faces[f]), V(faces[f + 1]), V(faces[f + 2]), color);
                GizmoRecorder.Triangle(V(faces[f]), V(faces[f + 2]), V(faces[f + 3]), color);
            }
        }

        const int kCircleSegments = 32;

        // A circle of the plane of two axes.
        static void Circle(Vector3 center, Vector3 a, Vector3 b, float radius)
        {
            Vector3 prev = P(center + a * radius);
            for (int i = 1; i <= kCircleSegments; i++)
            {
                double t = i * 2 * Math.PI / kCircleSegments;
                Vector3 p = P(center + (a * (float)Math.Cos(t) + b * (float)Math.Sin(t)) * radius);
                GizmoRecorder.Line(prev, p, color);
                prev = p;
            }
        }

        public static void DrawWireSphere(Vector3 center, float radius)
        {
            if (!On)
                return;
            Circle(center, Vector3.right, Vector3.up, radius);
            Circle(center, Vector3.up, Vector3.forward, radius);
            Circle(center, Vector3.forward, Vector3.right, radius);
        }

        public static void DrawSphere(Vector3 center, float radius)
        {
            if (!On)
                return;
            const int rings = 12, sectors = 24;
            Vector3 S(int r, int s)
            {
                double th = Math.PI * r / rings, ph = 2 * Math.PI * s / sectors;
                return P(center + new Vector3((float)(Math.Sin(th) * Math.Cos(ph)), (float)Math.Cos(th), (float)(Math.Sin(th) * Math.Sin(ph))) * radius);
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < sectors; s++)
                {
                    GizmoRecorder.Triangle(S(r, s), S(r + 1, s), S(r + 1, s + 1), color);
                    GizmoRecorder.Triangle(S(r, s), S(r + 1, s + 1), S(r, s + 1), color);
                }
        }

        /// <summary>A camera's frustum: its apex at center, looking along z, its field of view and aspect.</summary>
        public static void DrawFrustum(Vector3 center, float fov, float maxRange, float minRange, float aspect)
        {
            if (!On)
                return;
            float t = (float)Math.Tan(fov * 0.5 * Math.PI / 180);
            Span<Vector3> near = stackalloc Vector3[4], far = stackalloc Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                float sx = (i == 0 || i == 3) ? -1 : 1, sy = i < 2 ? -1 : 1;
                near[i] = P(center + new Vector3(sx * t * aspect * minRange, sy * t * minRange, minRange));
                far[i] = P(center + new Vector3(sx * t * aspect * maxRange, sy * t * maxRange, maxRange));
            }
            for (int i = 0; i < 4; i++)
            {
                GizmoRecorder.Line(near[i], near[(i + 1) % 4], color);
                GizmoRecorder.Line(far[i], far[(i + 1) % 4], color);
                GizmoRecorder.Line(near[i], far[i], color);
            }
        }

        public static void DrawWireMesh(Mesh mesh) => DrawWireMesh(mesh, -1, Vector3.zero, Quaternion.identity, Vector3.one);
        public static void DrawWireMesh(Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale) => DrawWireMesh(mesh, -1, position, rotation, scale);

        public static void DrawWireMesh(Mesh mesh, int submeshIndex, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (!On || mesh == null)
                return;
            Matrix4x4 m = matrix * Matrix4x4.TRS(position, rotation, scale);
            Vector3[] v = mesh.vertices;
            int[] t = submeshIndex >= 0 ? mesh.GetTriangles(submeshIndex) : mesh.triangles;
            for (int i = 0; i + 2 < t.Length; i += 3)
            {
                Vector3 a = m.MultiplyPoint3x4(v[t[i]]), b = m.MultiplyPoint3x4(v[t[i + 1]]), c = m.MultiplyPoint3x4(v[t[i + 2]]);
                GizmoRecorder.Line(a, b, color);
                GizmoRecorder.Line(b, c, color);
                GizmoRecorder.Line(c, a, color);
            }
        }

        public static void DrawMesh(Mesh mesh) => DrawMesh(mesh, -1, Vector3.zero, Quaternion.identity, Vector3.one);
        public static void DrawMesh(Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale) => DrawMesh(mesh, -1, position, rotation, scale);

        public static void DrawMesh(Mesh mesh, int submeshIndex, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (!On || mesh == null)
                return;
            Matrix4x4 m = matrix * Matrix4x4.TRS(position, rotation, scale);
            Vector3[] v = mesh.vertices;
            int[] t = submeshIndex >= 0 ? mesh.GetTriangles(submeshIndex) : mesh.triangles;
            for (int i = 0; i + 2 < t.Length; i += 3)
                GizmoRecorder.Triangle(m.MultiplyPoint3x4(v[t[i]]), m.MultiplyPoint3x4(v[t[i + 1]]), m.MultiplyPoint3x4(v[t[i + 2]]), color);
        }

        /// <summary>An icon of the editor's (or a texture asset's) name at a point, its size the same on the screen.</summary>
        public static void DrawIcon(Vector3 center, string name) => DrawIcon(center, name, true, Color.white);
        public static void DrawIcon(Vector3 center, string name, bool allowScaling) => DrawIcon(center, name, allowScaling, Color.white);

        public static void DrawIcon(Vector3 center, string name, bool allowScaling, Color tint)
        {
            if (On)
                GizmoRecorder.Icon(P(center), name, tint);
        }

        public static void DrawGUITexture(Rect screenRect, Texture texture) => DrawGUITexture(screenRect, texture, null);
        public static void DrawGUITexture(Rect screenRect, Texture texture, Material mat) => DrawGUITexture(screenRect, texture, 0, 0, 0, 0, mat);

        public static void DrawGUITexture(Rect screenRect, Texture texture, int leftBorder, int rightBorder, int topBorder, int bottomBorder, Material mat)
        {
            if (On && texture != null)
                GizmoRecorder.ScreenTexture(screenRect, texture, color);
        }

        /// <summary>How detailed a gizmo of the radius at the position may be (1 near, toward 0 far).</summary>
        public static float CalculateLOD(Vector3 position, float radius) => GizmoRecorder.lod?.Invoke(position, radius) ?? 1;
    }
}

namespace HelixEngine.Internal
{
    /// <summary>What Gizmos records while the editor draws gizmos: world space lines and triangles with their colors,
    /// icons and screen textures; the editor takes them and clears them each frame.</summary>
    public static class GizmoRecorder
    {
        public struct Vertex
        {
            public Vector3 position;
            public Color color;
        }

        public struct IconMark
        {
            public Vector3 position;
            public string name;
            public Color tint;
        }

        public struct ScreenMark
        {
            public Rect rect;
            public Texture texture;
            public Color tint;
        }

        public static bool recording { get; set; }
        public static readonly List<Vertex> lines = new();
        public static readonly List<Vertex> triangles = new();
        public static readonly List<IconMark> icons = new();
        public static readonly List<ScreenMark> screenTextures = new();
        public static Func<Vector3, float, float> lod;

        public static void Line(Vector3 a, Vector3 b, Color c)
        {
            lines.Add(new Vertex { position = a, color = c });
            lines.Add(new Vertex { position = b, color = c });
        }

        public static void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            triangles.Add(new Vertex { position = a, color = color });
            triangles.Add(new Vertex { position = b, color = color });
            triangles.Add(new Vertex { position = c, color = color });
        }

        public static void Icon(Vector3 p, string name, Color tint) => icons.Add(new IconMark { position = p, name = name, tint = tint });
        public static void ScreenTexture(Rect r, Texture t, Color tint) => screenTextures.Add(new ScreenMark { rect = r, texture = t, tint = tint });

        public static void Clear()
        {
            lines.Clear();
            triangles.Clear();
            icons.Clear();
            screenTextures.Clear();
        }
    }
}
