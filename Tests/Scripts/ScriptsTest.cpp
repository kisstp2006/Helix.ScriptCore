#include <Support/ScriptHost.h>

#include <Helix/Core/Logging/Log.h>
#include <Helix/Engine/Application/Application.h>
#include <Helix/Engine/Asset/Asset.h>
#include <Helix/Engine/GameObject/Lifecycle.h>
#include <Helix/Engine/Scene/SceneFile.h>
#include <Helix/Engine/Scene/SceneManager.h>
#include <Helix/Testing/Test.h>

#include <cstring>
#include <initializer_list>

using namespace helix;
using namespace helix::engine;
using namespace helix::scriptcore::testing;

namespace
{
    bool Expect(std::initializer_list<const char*> expected)
    {
        Array<String>& calls = Calls();
        bool same = calls.Size() == expected.size();
        usize i = 0;
        for (const char* e : expected)
            same = same && i < calls.Size() && calls[i++] == e;
        if (!same)
        {
            std::printf("    calls:");
            for (const String& c : calls)
                std::printf(" %.*s", int(c.Size()), c.Data());
            std::printf("\n");
        }
        calls.Clear();
        return same;
    }

    void Frame()
    {
        RunScriptPhase(ScriptMessage::FixedUpdate);
        RunScriptPhase(ScriptMessage::Update);
        RunScriptPhase(ScriptMessage::LateUpdate);
    }

    struct Errors
    {
        Array<String> texts;
    };

    void CountErrors(const LogMessage& message, void* user)
    {
        if (message.level >= LogLevel::Error)
            static_cast<Errors*>(user)->texts.Push(String(message.text));
    }
}

HX_TEST(Scripts, CSharpScriptsGetTheReferencesOrder)
{
    HX_REQUIRE(StartScripts());
    Calls().Clear();
    GameObject* g = GameObject::Create("A");
    Component* c = g->AddComponent(CSharp("CsRecorder").type);
    HX_REQUIRE(c != nullptr && g->GetComponent(CSharp("CsRecorder").type) == c);
    HX_CHECK(Expect({"A:cs.Awake", "A:cs.OnEnable"}));
    Frame();
    HX_CHECK(Expect({"A:cs.Start", "A:cs.FixedUpdate", "A:cs.Update", "A:cs.LateUpdate"}));
    static_cast<Behaviour*>(c)->SetEnabled(false);
    static_cast<Behaviour*>(c)->SetEnabled(true);
    Application::SetFocused(false);
    Application::SetFocused(true);
    HX_CHECK(Expect({"A:cs.OnDisable", "A:cs.OnEnable", "A:cs.OnApplicationFocus(false)", "A:cs.OnApplicationFocus(true)"}));
    DestroyImmediate(g);
    HX_CHECK(Expect({"A:cs.OnDisable", "A:cs.OnDestroy"}));
}

HX_TEST(Scripts, BothLanguagesOnOneGameObject)
{
    HX_REQUIRE(StartScripts());
    Calls().Clear();
    GameObject* g = GameObject::Create("M");
    g->AddComponent(CSharp("CsRecorder").type);
    AddScript(g, Cpp("CppRecorder"));
    g->AddComponent(CSharp("CsEarly").type);
    HX_CHECK(Expect({"M:cs.Awake", "M:cs.OnEnable", "M:cpp.Awake", "M:cpp.OnEnable"}));
    Frame();
    Calls().Clear();
    // Execution order first; within one order the C++ scripts, then the C# ones.
    RunScriptPhase(ScriptMessage::Update);
    HX_CHECK(Expect({"M:cs.Early.Update", "M:cpp.Update", "M:cs.Update"}));
    DestroyImmediate(g);
    Calls().Clear();
}

HX_TEST(Scripts, AnExceptionStopsOnlyItsScript)
{
    HX_REQUIRE(StartScripts());
    Calls().Clear();
    GameObject* g = GameObject::Create("X");
    g->AddComponent(CSharp("CsThrower").type);
    g->AddComponent(CSharp("CsRecorder").type);
    Frame();
    Calls().Clear();
    Errors errors;
    AddLogSink(&CountErrors, &errors);
    RunScriptPhase(ScriptMessage::Update);
    RemoveLogSink(&CountErrors, &errors);
    HX_CHECK(Expect({"X:cs.Thrower.Update", "X:cs.Update"}));
    HX_CHECK(errors.texts.Size() == 1 && errors.texts[0].View().Contains("thrown by CsThrower") && errors.texts[0].View().Contains("Tests.CsThrower on 'X'"));
    DestroyImmediate(g);
    Calls().Clear();
}

HX_TEST(Scripts, CSharpFindsMessagesAndAddsScripts)
{
    HX_REQUIRE(StartScripts());
    Calls().Clear();
    // A message of a C# base class, an empty one too.
    HX_CHECK(CSharp("CsDerived").Has(ScriptMessage::Update) && CSharp("CsDerived").Has(ScriptMessage::LateUpdate) && !CSharp("CsDerived").Has(ScriptMessage::Start));
    GameObject* g = GameObject::Create("D");
    g->AddComponent(CSharp("CsDerived").type);
    g->AddComponent(CSharp("CsAdder").type);
    Frame();
    // CsAdder
    HX_CHECK(Expect({"D:cs.Awake", "D:cs.OnEnable", "D:cs.Adder.added", "D:cs.Start", "D:cs.FixedUpdate", "D:cs.Base.Update", "D:cs.Update", "D:cs.LateUpdate"}));
    DestroyImmediate(g);
    Calls().Clear();
}

HX_TEST(Scripts, CSharpScriptsDisableThemselvesAndSeeTheirDeath)
{
    HX_REQUIRE(StartScripts());
    Calls().Clear();
    GameObject* g = GameObject::Create("S");
    g->AddComponent(CSharp("CsSelfDisabler").type);
    Frame();
    Frame();
    HX_CHECK(Expect({"S:cs.SelfDisabler.Update"}));
    // OnDestroy runs while the native object lives: it is not null yet.
    DestroyImmediate(g);
    HX_CHECK(Expect({"S:cs.SelfDisabler.OnDestroy(alive)"}));
}

HX_TEST(Scripts, SendMessageAcrossTheLanguages)
{
    HX_REQUIRE(StartScripts());
    Calls().Clear();
    GameObject* g = GameObject::Create("R");
    g->AddComponent(CSharp("CsReceiver").type);
    AddScript(g, Cpp("CppRecorder"));
    Calls().Clear();
    // C++ to C#: a number converted to the parameter, text, no argument.
    HX_CHECK(SendScriptMessage(g, "Hit", scripting::Variant::Int(scripting::TypeOf<i32>(), 3)));
    HX_CHECK(SendScriptMessage(g, "Named", scripting::Variant::Text("bolt")));
    HX_CHECK(SendScriptMessage(g, "Ping"));
    HX_CHECK(Expect({"R:cs.Hit(3)", "R:cs.Named(bolt)", "R:cs.Ping", "R:cpp.Ping"}));
    // C# to both languages from CsReceiver's Start, and a C# object to C# scripts.
    RunScriptPhase(ScriptMessage::Update);
    HX_CHECK(Expect({"R:cpp.Start", "R:cs.Ping", "R:cpp.Ping", "R:cpp.Toggle(true)", "R:cs.Carry(7)", "R:cpp.Update"}));
    DestroyImmediate(g);
    Calls().Clear();
}

HX_TEST(Scripts, AGameModuleLoadsRunsAndUnloads)
{
    HX_REQUIRE(StartScripts());
    Calls().Clear();
    // A shared library against the shared engine: its scripts beside the C# ones, its messages, its unload, again.
    for (int round = 0; round < 2; round++)
    {
        String error;
        HX_REQUIRE(LoadGameModule(HX_TEST_MODULE, error));
        const ScriptClass* recorder = FindScriptClass("ScriptCoreModule", "ScriptCoreModule", "ModuleRecorder");
        HX_REQUIRE(recorder != nullptr);
        GameObject* g = GameObject::Create("D");
        g->AddComponent(CSharp("CsRecorder").type);
        GameObject* k = GameObject::Create("K");
        HX_REQUIRE(AddScript(k, *recorder) != nullptr);
        HX_REQUIRE(AddScript(g, *recorder) != nullptr);
        HX_CHECK(Expect({"D:cs.Awake", "D:cs.OnEnable", "K:mod.Awake", "K:mod.OnEnable", "D:mod.Awake", "D:mod.OnEnable"}));
        RunScriptPhase(ScriptMessage::Update);
        HX_CHECK(Expect({"K:mod.Start", "D:mod.Start", "D:cs.Start", "K:mod.Update", "D:mod.Update", "D:cs.Update"}));
        HX_CHECK(SendScriptMessage(g, "Ping"));
        HX_CHECK(Expect({"D:mod.Ping"}));
        // Unloading destroys the module's scripts; the C# one stays.
        UnloadGameModule("ScriptCoreModule");
        HX_CHECK(Expect({"K:mod.OnDisable", "K:mod.OnDestroy", "D:mod.OnDisable", "D:mod.OnDestroy"}));
        HX_CHECK(FindScriptClass("ScriptCoreModule", "ScriptCoreModule", "ModuleRecorder") == nullptr);
        HX_CHECK(g->GetComponent(CSharp("CsRecorder").type) != nullptr);
        RunScriptPhase(ScriptMessage::Update);
        HX_CHECK(Expect({"D:cs.Update"}));
        DestroyImmediate(g);
        DestroyImmediate(k);
        Calls().Clear();
    }
}

HX_TEST(Scripts, ASceneOfBothLanguagesLoadsAndUnloads)
{
    HX_REQUIRE(StartScripts());
    Calls().Clear();
    // The script assets of a C# and a C++ class, a scene with both on one GameObject and the C++ one on another.
    Guid cs, cpp;
    Guid::TryParse("e0000000000000000000000000000001", cs);
    Guid::TryParse("e0000000000000000000000000000002", cpp);
    MonoScript* csAsset = MonoScript::FromScriptClass(CSharp("CsRecorder"));
    MonoScript* cppAsset = MonoScript::FromScriptClass(Cpp("CppRecorder"));
    RegisterAssetObject(cs, 11500000, csAsset, true);
    RegisterAssetObject(cpp, 11500000, cppAsset, true);
    const char* file = R"(%YAML 1.1
%TAG !h! tag:helixengine,2026:
--- !h!1 &100
GameObject:
  m_Component:
  - component: {fileID: 101}
  - component: {fileID: 102}
  - component: {fileID: 103}
  m_Name: A
  m_IsActive: 1
--- !h!4 &101
Transform:
  m_GameObject: {fileID: 100}
  m_Children: []
  m_Father: {fileID: 0}
--- !h!114 &102
GameBehaviour:
  m_GameObject: {fileID: 100}
  m_Enabled: 1
  m_Script: {fileID: 11500000, guid: e0000000000000000000000000000001, type: 3}
--- !h!114 &103
GameBehaviour:
  m_GameObject: {fileID: 100}
  m_Enabled: 1
  m_Script: {fileID: 11500000, guid: e0000000000000000000000000000002, type: 3}
--- !h!1 &200
GameObject:
  m_Component:
  - component: {fileID: 201}
  - component: {fileID: 202}
  m_Name: B
  m_IsActive: 1
--- !h!4 &201
Transform:
  m_GameObject: {fileID: 200}
  m_Children: []
  m_Father: {fileID: 0}
--- !h!114 &202
GameBehaviour:
  m_GameObject: {fileID: 200}
  m_Enabled: 1
  m_Script: {fileID: 11500000, guid: e0000000000000000000000000000002, type: 3}
)";
    Scene s = SceneManager::CreateScene("Mixed");
    Array<String> errors;
    HX_CHECK(ReadSceneFile(Span<const u8>(reinterpret_cast<const u8*>(file), std::strlen(file)), s, errors) && errors.Empty());
    // Awake and OnEnable of every script of the scene before any Start; the Starts before the first Update.
    HX_CHECK(Expect({"A:cs.Awake", "A:cs.OnEnable", "A:cpp.Awake", "A:cpp.OnEnable", "B:cpp.Awake", "B:cpp.OnEnable"}));
    Frame();
    HX_CHECK(Expect({"A:cpp.Start", "B:cpp.Start", "A:cs.Start", "A:cs.FixedUpdate", "A:cpp.Update", "B:cpp.Update", "A:cs.Update",
                     "A:cs.LateUpdate"}));
    SceneManager::UnloadSceneAsync(s);
    SceneManager::ProcessSceneLoads();
    // Unloading destroys the components in reverse order, each script's OnDisable before its OnDestroy.
    HX_CHECK(Expect({"A:cpp.OnDisable", "A:cpp.OnDestroy", "A:cs.OnDisable", "A:cs.OnDestroy", "B:cpp.OnDisable", "B:cpp.OnDestroy"}));
    ResetAssets();
    DestroyImmediate(csAsset, true);
    DestroyImmediate(cppAsset, true);
}
