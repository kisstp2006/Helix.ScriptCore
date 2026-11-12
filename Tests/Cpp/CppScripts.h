#pragma once

#include <Helix/Core/Strings/Format.h>
#include <Helix/Engine/Scene/SceneManager.h>
#include <Helix/Engine/Script/GameBehaviour.h>
#include <HelixEngine/HelixEngineApi.h>

// C++ scripts beside the C# ones (the test program's game module, ScriptCoreTests).
namespace helix::scriptcore::testing
{
    inline Array<String>& Calls()
    {
        static Array<String> calls;
        return calls;
    }

    inline void Note(const engine::GameBehaviour* b, const char* message)
    {
        Calls().Push(Format("{}:{}", b->gameObject() ? b->gameObject()->Name() : StringView("?"), message));
    }

    HX_CLASS(Namespace = "ScriptCoreTests")
    class CppRecorder : public engine::GameBehaviour
    {
        void Awake() { Note(this, "cpp.Awake"); }
        void OnEnable() { Note(this, "cpp.OnEnable"); }
        void Start() { Note(this, "cpp.Start"); }
        void Update() { Note(this, "cpp.Update"); }
        void OnDisable() { Note(this, "cpp.OnDisable"); }
        void OnDestroy() { Note(this, "cpp.OnDestroy"); }
        void Ping() { Note(this, "cpp.Ping"); }
        void Toggle(bool on) { Note(this, on ? "cpp.Toggle(true)" : "cpp.Toggle(false)"); }
    };
    // The scenario of CsScenario on the C++ script API: value handles, properties as in C#.
    HX_CLASS(Namespace = "ScriptCoreTests")
    class CppScenario : public ::HelixEngine::GameBehaviour
    {
        static String Vec(const Vector3& v) { return Format("({:.2f}, {:.2f}, {:.2f})", v.x, v.y, v.z); }
        static const char* Bool(bool b) { return b ? "True" : "False"; }

        void Start()
        {
            using namespace ::HelixEngine;
            i32 rootsBefore = i32(engine::SceneManager::GetActiveScene().rootCount());
            GameObject g = GameObject::New("ProbeCpp");
            i32 rootsAfter = i32(engine::SceneManager::GetActiveScene().rootCount());
            Transform t = g.transform;
            t.position = Vector3(1, 2, 3);
            t.Translate(Vector3(1, 0, 0));
            GameObject child = GameObject::New("Child");
            Transform ct = child.transform;
            ct.SetParent(t, false);
            ct.localPosition = Vector3(0, 1, 0);
            String childPosition = Vec(ct.position);
            bool found = GameObject::Find("ProbeCpp") == g;
            bool same = g.GetComponent<Transform>() == t;
            Object clone = Object::Instantiate(g);
            String cloneName = clone.name;
            GameObject cloneObject = GameObject::Find(cloneName);
            Transform cloneTransform = cloneObject.transform;
            i32 cloneChildren = cloneTransform.childCount;
            Object::DestroyImmediate(clone);
            bool destroyed = clone == nullptr;
            String position = Vec(t.position);
            Debug::Log("scenario done in C++");
            Object::DestroyImmediate(g);
            StringView cloned = cloneName;
            usize at = cloned.Find("Cpp");
            String shortName = at == StringView::npos ? String(cloned) : Format("{}{}", cloned.Substr(0, at), cloned.Substr(at + 3));
            Calls().Push(Format("ProbeCpp:scenario:pos={};child={};found={};same={};clone={}:{};destroyed={};roots=+{};gone={}", position, childPosition,
                                Bool(found), Bool(same), shortName, cloneChildren, Bool(destroyed), rootsAfter - rootsBefore, Bool(g == nullptr)));
        }
    };
}
