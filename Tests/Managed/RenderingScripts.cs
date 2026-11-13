using System;
using System.Globalization;
using HelixEngine;
using HelixEngine.Rendering;

// The scenarios of the rendering and asset API (the C++ ones in Cpp/CppScripts.h write the same lines).
namespace Tests
{
    static class Text
    {
        public static string F(float v) => v.ToString("F2", CultureInfo.InvariantCulture);
        public static string V(Vector3 v) => "(" + F(v.x) + "," + F(v.y) + "," + F(v.z) + ")";
        public static string C(Color c) => "(" + F(c.r) + "," + F(c.g) + "," + F(c.b) + "," + F(c.a) + ")";

        // A quad of side 2 around the origin facing -z, with uvs.
        public static Mesh Quad()
        {
            var mesh = new Mesh();
            mesh.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(-1, 1, 0), new Vector3(1, 1, 0), new Vector3(1, -1, 0) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    // Shader, keywords, Material, MaterialPropertyBlock, Mesh and Texture2D.
    public class CsResources : GameBehaviour
    {
        void Start()
        {
            Shader s = Shader.Find("Tests/Surface");
            int props = s.GetPropertyCount();
            int tint = Shader.PropertyToID("tint");
            int index = s.FindPropertyIndex("tint");
            bool sameId = s.GetPropertyNameId(index) == tint;
            LocalKeywordSpace space = s.keywordSpace;
            LocalKeyword glow = space.FindKeyword("Glow");
            var m = new Material(s);
            m.SetFloat("smoothness", 0.25f);
            float smooth = m.GetFloat("smoothness");
            Color color = m.color;
            m.EnableKeyword(glow);
            bool on = m.IsKeywordEnabled("Glow");
            string keywords = string.Join("|", m.shaderKeywords);
            m.SetInteger("layers", 4);
            int layers = m.GetInteger("layers");
            var block = new MaterialPropertyBlock();
            bool empty = block.isEmpty;
            block.SetFloat("smoothness", 0.75f);
            float blockSmooth = block.GetFloat("smoothness");

            Mesh mesh = Text.Quad();
            Bounds b = mesh.bounds;
            string meshLine = mesh.vertexCount + "/" + mesh.triangles.Length + "/" + mesh.subMeshCount + "/" + mesh.uv.Length + "/" + mesh.GetIndexCount(0) + "/" +
                              Text.V(mesh.normals[0]) + "/" + Text.V(b.center) + Text.V(b.size);

            var tex = new Texture2D(4, 4);
            tex.SetPixel(1, 2, new Color(1, 0, 0, 1));
            tex.Apply();
            string texLine = tex.width + "x" + tex.height + "/" + (int)tex.format + "/" + tex.mipmapCount + "/" + Text.C(tex.GetPixel(1, 2)) + "/" +
                             (int)tex.filterMode + "/" + (int)tex.wrapMode;

            string line = "res:props=" + props + ";same=" + sameId + ";keywords=" + space.keywordCount + ";glow=" + glow.isValid + ";smooth=" + Text.F(smooth) +
                          ";color=" + Text.C(color) + ";on=" + on + ";enabled=" + keywords + ";layers=" + layers + ";queue=" + m.renderQueue +
                          ";passes=" + m.passCount + ";block=" + empty + "/" + block.isEmpty + "/" + Text.F(blockSmooth) + ";mesh=" + meshLine + ";tex=" + texLine;
            DestroyImmediate(m);
            DestroyImmediate(mesh);
            DestroyImmediate(tex);
            Notes.NoteRaw(line);
        }
    }

    // MeshFilter, MeshRenderer, property blocks, Camera, Light and GeometryUtility.
    public class CsComponents : GameBehaviour
    {
        void Start()
        {
            var shape = new GameObject("Shape");
            var filter = shape.AddComponent<MeshFilter>();
            var renderer = shape.AddComponent<MeshRenderer>();
            Mesh mesh = Text.Quad();
            filter.sharedMesh = mesh;
            var m = new Material(Shader.Find("Tests/Plain"));
            renderer.sharedMaterial = m;
            shape.transform.position = new Vector3(0, 0, 5);
            bool sameMesh = filter.sharedMesh == mesh;
            int materials = renderer.sharedMaterials.Length;
            Bounds rb = renderer.bounds;
            var block = new MaterialPropertyBlock();
            block.SetFloat("strength", 2);
            renderer.SetPropertyBlock(block);
            bool has = renderer.HasPropertyBlock();
            var got = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(got);
            float strength = got.GetFloat("strength");
            renderer.SetPropertyBlock(null);
            bool cleared = !renderer.HasPropertyBlock();

            var eye = new GameObject("Eye");
            var cam = eye.AddComponent<Camera>();
            eye.transform.position = new Vector3(0, 0, -10);
            cam.fieldOfView = 50;
            cam.aspect = 2;
            Vector3 vp = cam.WorldToViewportPoint(new Vector3(0, 0, 0));
            float focal = Camera.FieldOfViewToFocalLength(60, 24);
            float horizontal = Camera.VerticalToHorizontalFieldOfView(60, 2);
            int mask = cam.cullingMask;
            bool listed = Array.IndexOf(Camera.allCameras, cam) >= 0;
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
            bool inside = GeometryUtility.TestPlanesAABB(planes, rb);
            bool behind = GeometryUtility.TestPlanesAABB(planes, new Bounds(new Vector3(0, 0, -50), Vector3.one));
            cam.Reset();
            bool made = GeometryUtility.TryCreatePlaneFromPolygon(new[] { new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 0, 0) }, out Plane plane);
            bool tooFew = GeometryUtility.TryCreatePlaneFromPolygon(new[] { new Vector3(0, 0, 0) }, out Plane _);

            var lamp = new GameObject("Lamp");
            var light = lamp.AddComponent<Light>();
            light.intensity = 3;
            light.type = LightType.Directional;
            string lightBefore = Text.F(light.intensity) + "/" + (int)light.type;
            light.Reset();

            string line = "comp:mesh=" + sameMesh + ";materials=" + materials + ";bounds=" + Text.V(rb.center) + Text.V(rb.size) + ";block=" + has + "/" +
                          Text.F(strength) + "/" + cleared + ";viewport=" + Text.V(vp) + ";focal=" + Text.F(focal) + ";horizontal=" + Text.F(horizontal) +
                          ";mask=" + mask + ";listed=" + listed + ";planes=" + planes.Length + ";inside=" + inside + ";behind=" + behind + ";reset=" +
                          Text.F(cam.fieldOfView) + "/" + Text.F(cam.aspect) + ";plane=" + made + Text.V(plane.normal) + Text.F(plane.distance) + "/" + tooFew +
                          ";light=" + lightBefore + "/" + Text.F(light.intensity) + "/" + (int)light.type;
            DestroyImmediate(shape);
            DestroyImmediate(eye);
            DestroyImmediate(lamp);
            DestroyImmediate(m);
            DestroyImmediate(mesh);
            Notes.NoteRaw(line);
        }
    }

    // A ScriptableObject of C#: its messages and fields.
    public class CsData : ScriptableObject
    {
        public int count = 3;
        public string label = "none";

        void Awake() => Notes.NoteRaw("so:x.Awake:" + count);
        void OnEnable() => Notes.NoteRaw("so:x.OnEnable:" + count + "," + label);
        void OnDisable() => Notes.NoteRaw("so:x.OnDisable:" + count);
        void OnDestroy() => Notes.NoteRaw("so:x.OnDestroy");
    }

    // CreateInstance, Instantiate and Destroy of ScriptableObjects.
    public class CsDataUser : GameBehaviour
    {
        void Start()
        {
            var d = ScriptableObject.CreateInstance<CsData>();
            d.count = 5;
            d.name = "Made";
            CsData copy = Instantiate(d);
            var byName = ScriptableObject.CreateInstance("CsData") as CsData;
            ScriptableObject byType = ScriptableObject.CreateInstance(typeof(CsData));
            var plain = ScriptableObject.CreateInstance<ScriptableObject>();
            ScriptableObject wrong = ScriptableObject.CreateInstance(typeof(CsRecorder));
            int found = FindObjectsByType<CsData>(FindObjectsSortMode.None).Length;
            string line = "soapi:copy=" + copy.count + "/" + copy.name + ";byName=" + (byName != null) + "/" + (byName != null ? byName.count : -1) + ";byType=" +
                          (byType is CsData) + ";plain=" + (plain != null) + ";wrong=" + (wrong == null) + ";found=" + found;
            DestroyImmediate(d);
            DestroyImmediate(copy);
            DestroyImmediate(byName);
            DestroyImmediate(byType);
            DestroyImmediate(plain);
            Notes.NoteRaw(line);
        }
    }

    // Graphics.RenderMesh each frame: a quad left of the renderer's (the golden image of both).
    public class CsDrawer : GameBehaviour
    {
        public Mesh mesh;
        public Material material;

        void Update()
        {
            var p = new RenderParams(material);
            Graphics.RenderMesh(p, mesh, 0, Matrix4x4.TRS(new Vector3(-2, 0, 0), Quaternion.identity, Vector3.one));
        }
    }

    // The scene of the golden image through the script API: a camera, a light, a lit renderer and a drawn mesh.
    public class CsGoldenScene : GameBehaviour
    {
        void Start()
        {
            var eye = new GameObject("GoldenEye");
            var cam = eye.AddComponent<Camera>();
            eye.transform.position = new Vector3(0, 0, -10);
            cam.orthographic = true;
            cam.orthographicSize = 4;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0.25f, 1);

            var sun = new GameObject("GoldenSun");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.5f;

            Mesh quad = Text.Quad();
            var lit = new Material(Shader.Find("Tests/Lambert"));
            lit.color = new Color(1, 0.5f, 0, 1);
            var shape = new GameObject("GoldenShape");
            shape.AddComponent<MeshFilter>().sharedMesh = quad;
            shape.AddComponent<MeshRenderer>().sharedMaterial = lit;
            shape.transform.position = new Vector3(2, 0, 0);

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            tex.SetPixels32(new[] { new Color32(255, 255, 0, 255), new Color32(0, 255, 0, 255), new Color32(0, 0, 255, 255), new Color32(255, 255, 255, 255) });
            tex.filterMode = FilterMode.Point;
            tex.Apply(false);
            var textured = new Material(Shader.Find("Tests/Textured"));
            textured.mainTexture = tex;
            var drawer = gameObject.AddComponent<CsDrawer>();
            drawer.mesh = quad;
            drawer.material = textured;
            eye.AddComponent<CsCameraEvents>();
            shape.AddComponent<CsRendererEvents>();
        }
    }

    // The camera messages of a camera's scripts.
    public class CsCameraEvents : GameBehaviour
    {
        void OnPreCull() => Notes.NoteRaw("cs.OnPreCull");
        void OnPreRender() => Notes.NoteRaw("cs.OnPreRender");
        void OnPostRender() => Notes.NoteRaw("cs.OnPostRender");
    }

    // The messages of a drawn renderer's scripts, and the one every script gets after a camera drew.
    public class CsRendererEvents : GameBehaviour
    {
        void OnWillRenderObject() => Notes.NoteRaw("cs.OnWillRenderObject:" + Camera.current.name);
        void OnRenderObject() => Notes.NoteRaw("cs.OnRenderObject");
    }
}
