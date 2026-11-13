#pragma once

#include <Cpp/CppScripts.h>

// The scenarios of Managed/RenderingScripts.cs on the C++ script API: the same lines.
namespace helix::scriptcore::testing
{
    namespace text
    {
        inline String F(f32 v) { return Format("{:.2f}", v); }
        inline String V(const Vector3& v) { return Format("({:.2f},{:.2f},{:.2f})", v.x, v.y, v.z); }
        inline String C(const Color& c) { return Format("({:.2f},{:.2f},{:.2f},{:.2f})", c.r, c.g, c.b, c.a); }
        inline const char* B(bool b) { return b ? "True" : "False"; }

        // A quad of side 2 around the origin facing -z, with uvs.
        inline ::HelixEngine::Mesh Quad()
        {
            using namespace ::HelixEngine;
            Mesh mesh = Mesh::New();
            const Vector3 vertices[] = {Vector3(-1, -1, 0), Vector3(-1, 1, 0), Vector3(1, 1, 0), Vector3(1, -1, 0)};
            const Vector2 uvs[] = {Vector2(0, 0), Vector2(0, 1), Vector2(1, 1), Vector2(1, 0)};
            const i32 triangles[] = {0, 1, 2, 0, 2, 3};
            mesh.vertices = Span<const Vector3>(vertices, 4);
            mesh.uv = Span<const Vector2>(uvs, 4);
            mesh.triangles = Span<const i32>(triangles, 6);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    // Shader, keywords, Material, MaterialPropertyBlock, Mesh and Texture2D.
    HX_CLASS(Namespace = "ScriptCoreTests")
    class CppResources : public ::HelixEngine::GameBehaviour
    {
        void Start()
        {
            using namespace ::HelixEngine;
            using namespace text;
            Shader s = Shader::Find("Tests/Surface");
            i32 props = s.GetPropertyCount();
            i32 tint = Shader::PropertyToID("tint");
            i32 index = s.FindPropertyIndex("tint");
            bool sameId = s.GetPropertyNameId(index) == tint;
            engine::LocalKeywordSpace space = s.keywordSpace;
            engine::LocalKeyword glow = space.FindKeyword("Glow");
            Material m = Material::New(s);
            m.SetFloat("smoothness", 0.25f);
            f32 smooth = m.GetFloat("smoothness");
            Color color = m.color;
            m.EnableKeyword(glow);
            bool on = m.IsKeywordEnabled("Glow");
            Array<String> names = m.shaderKeywords;
            String keywords;
            for (usize i = 0; i < names.Size(); i++)
                keywords.Append(i ? "|" : "").Append(names[i]);
            m.SetInteger("layers", 4);
            i32 layers = m.GetInteger("layers");
            engine::MaterialPropertyBlock block;
            bool empty = block.isEmpty();
            block.SetFloat("smoothness", 0.75f);
            f32 blockSmooth = block.GetFloat("smoothness");

            Mesh mesh = Quad();
            Bounds b = mesh.bounds;
            Array<i32> triangles = mesh.triangles;
            Array<Vector2> uv = mesh.uv;
            Array<Vector3> normals = mesh.normals;
            String meshLine = Format("{}/{}/{}/{}/{}/{}/{}{}", i32(mesh.vertexCount), triangles.Size(), i32(mesh.subMeshCount), uv.Size(), mesh.GetIndexCount(0),
                                     V(normals[0]), V(b.center), V(b.size()));

            Texture2D tex = Texture2D::New(4, 4);
            tex.SetPixel(1, 2, Color(1, 0, 0, 1));
            tex.Apply();
            String texLine = Format("{}x{}/{}/{}/{}/{}/{}", i32(tex.width), i32(tex.height), i32(engine::TextureFormat(tex.format)), i32(tex.mipmapCount),
                                    C(tex.GetPixel(1, 2)), i32(engine::FilterMode(tex.filterMode)), i32(engine::TextureWrapMode(tex.wrapMode)));

            bool afterEmpty = block.isEmpty();
            String line = Format("res:props={};same={};keywords={};glow={};smooth={};color={};on={};enabled={};layers={};queue={};passes={};block={}/{}/{};"
                                 "mesh={};tex={}",
                                 props, B(sameId), space.keywordCount(), B(glow.isValid()), F(smooth), C(color), B(on), keywords, layers, i32(m.renderQueue),
                                 i32(m.passCount), B(empty), B(afterEmpty), F(blockSmooth), meshLine, texLine);
            Object::DestroyImmediate(m);
            Object::DestroyImmediate(mesh);
            Object::DestroyImmediate(tex);
            Calls().Push(line);
        }
    };

    // MeshFilter, MeshRenderer, property blocks, Camera, Light and GeometryUtility.
    HX_CLASS(Namespace = "ScriptCoreTests")
    class CppComponents : public ::HelixEngine::GameBehaviour
    {
        void Start()
        {
            using namespace ::HelixEngine;
            using namespace text;
            GameObject shape = GameObject::New("Shape");
            MeshFilter filter = shape.AddComponent<MeshFilter>();
            MeshRenderer renderer = shape.AddComponent<MeshRenderer>();
            Mesh mesh = Quad();
            filter.sharedMesh = mesh;
            Material m = Material::New(Shader::Find("Tests/Plain"));
            renderer.sharedMaterial = m;
            Transform st = shape.transform;
            st.position = Vector3(0, 0, 5);
            bool sameMesh = Mesh(filter.sharedMesh) == mesh;
            Array<Material> shared = renderer.sharedMaterials;
            Bounds rb = renderer.bounds;
            engine::MaterialPropertyBlock block;
            block.SetFloat("strength", 2);
            renderer.SetPropertyBlock(block);
            bool has = renderer.HasPropertyBlock();
            engine::MaterialPropertyBlock got;
            renderer.GetPropertyBlock(got);
            f32 strength = got.GetFloat("strength");
            renderer.SetPropertyBlock(engine::PropertyBlockId{});
            bool cleared = !renderer.HasPropertyBlock();

            GameObject eye = GameObject::New("Eye");
            Camera cam = eye.AddComponent<Camera>();
            Transform et = eye.transform;
            et.position = Vector3(0, 0, -10);
            cam.fieldOfView = 50;
            cam.aspect = 2;
            Vector3 vp = cam.WorldToViewportPoint(Vector3(0, 0, 0));
            f32 focal = Camera::FieldOfViewToFocalLength(60, 24);
            f32 horizontal = Camera::VerticalToHorizontalFieldOfView(60, 2);
            i32 mask = cam.cullingMask;
            Array<Camera> all = Camera::allCameras;
            bool listed = false;
            for (const Camera& c : all)
                listed = listed || c == cam;
            Array<Plane> planes = GeometryUtility::CalculateFrustumPlanes(cam);
            bool inside = GeometryUtility::TestPlanesAABB(Span<const Plane>(planes.Data(), planes.Size()), rb);
            bool behind = GeometryUtility::TestPlanesAABB(Span<const Plane>(planes.Data(), planes.Size()), Bounds(Vector3(0, 0, -50), Vector3(1, 1, 1)));
            cam.Reset();
            const Vector3 triangle[] = {Vector3(0, 0, 0), Vector3(0, 1, 0), Vector3(1, 0, 0)};
            Plane plane, none;
            bool made = GeometryUtility::TryCreatePlaneFromPolygon(Span<const Vector3>(triangle, 3), plane);
            bool tooFew = GeometryUtility::TryCreatePlaneFromPolygon(Span<const Vector3>(triangle, 1), none);

            GameObject lamp = GameObject::New("Lamp");
            Light light = lamp.AddComponent<Light>();
            light.intensity = 3;
            light.type = engine::LightType::Directional;
            String lightBefore = Format("{}/{}", F(light.intensity), i32(engine::LightType(light.type)));
            light.Reset();

            String line = Format("comp:mesh={};materials={};bounds={}{};block={}/{}/{};viewport={};focal={};horizontal={};mask={};listed={};planes={};"
                                 "inside={};behind={};reset={}/{};plane={}{}{}/{};light={}/{}/{}",
                                 B(sameMesh), shared.Size(), V(rb.center), V(rb.size()), B(has), F(strength), B(cleared), V(vp), F(focal), F(horizontal), mask,
                                 B(listed), planes.Size(), B(inside), B(behind), F(cam.fieldOfView), F(cam.aspect), B(made), V(plane.normal), F(plane.distance),
                                 B(tooFew), lightBefore, F(light.intensity), i32(engine::LightType(light.type)));
            Object::DestroyImmediate(shape);
            Object::DestroyImmediate(eye);
            Object::DestroyImmediate(lamp);
            Object::DestroyImmediate(m);
            Object::DestroyImmediate(mesh);
            Calls().Push(line);
        }
    };

    // CsRendererEvents in C++.
    HX_CLASS(Namespace = "ScriptCoreTests")
    class CppRendererEvents : public ::HelixEngine::GameBehaviour
    {
        void OnWillRenderObject()
        {
            ::HelixEngine::Camera current = ::HelixEngine::Camera::current;
            Calls().Push(Format("cpp.OnWillRenderObject:{}", String(current.name)));
        }
        void OnRenderObject() { Calls().Push(String("cpp.OnRenderObject")); }
    };

    // A ScriptableObject of C++: its messages and fields.
    HX_CLASS(Namespace = "ScriptCoreTests")
    class CppData : public ::HelixEngine::ScriptableObject
    {
    public:
        HX_FIELD() i32 count = 3;
        HX_FIELD() String label = String("none");

        void Awake() { Calls().Push(Format("so:x.Awake:{}", count)); }
        void OnEnable() { Calls().Push(Format("so:x.OnEnable:{},{}", count, label)); }
        void OnDisable() { Calls().Push(Format("so:x.OnDisable:{}", count)); }
        void OnDestroy() { Calls().Push(String("so:x.OnDestroy")); }
    };

    // CreateInstance, Instantiate and Destroy of ScriptableObjects.
    HX_CLASS(Namespace = "ScriptCoreTests")
    class CppDataUser : public ::HelixEngine::GameBehaviour
    {
        void Start()
        {
            using namespace ::HelixEngine;
            using namespace text;
            auto d = ScriptableObject::CreateInstance<CppData>();
            d.Get()->count = 5;
            d.Get()->SetName("Made");
            Object copyObject = Object::Instantiate(Object::HxFromId(d.Id()));
            auto* copy = static_cast<CppData*>(engine::EntityIdToObject(engine::EntityId{copyObject.HxId()}));
            Object byNameObject = ScriptableObject::CreateInstance("CppData");
            auto* byName = static_cast<CppData*>(engine::EntityIdToObject(engine::EntityId{byNameObject.HxId()}));
            Object byType = Object::HxFromId(ScriptableObject::CreateInstance<CppData>().Id());
            Object plain = ScriptableObject::CreateInstance("ScriptableObject");
            Object wrong = ScriptableObject::CreateInstance("CppRecorder");
            auto found = Object::FindObjectsByType<CppData>(engine::FindObjectsSortMode::None);
            String line = Format("soapi:copy={}/{};byName={}/{};byType={};plain={};wrong={};found={}", copy ? copy->count : -1,
                                 copy ? copy->Name() : StringView("?"), B(byName != nullptr), byName ? byName->count : -1, B(byType != nullptr),
                                 B(plain != nullptr), B(wrong == nullptr), found.Size());
            Object::DestroyImmediate(Object::HxFromId(d.Id()));
            Object::DestroyImmediate(copyObject);
            Object::DestroyImmediate(byNameObject);
            Object::DestroyImmediate(byType);
            Object::DestroyImmediate(plain);
            Calls().Push(line);
        }
    };
}
