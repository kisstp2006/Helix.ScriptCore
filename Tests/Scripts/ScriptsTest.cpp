#include <Support/ScriptHost.h>

#include <Helix/Core/Logging/Log.h>
#include <Helix/Engine/Application/Application.h>
#include <Helix/Engine/Asset/Asset.h>
#include <Helix/Engine/GameObject/Lifecycle.h>
#include <Helix/Engine/Prefab/Prefab.h>
#include <Helix/Engine/PlayerLoop/PlayerLoop.h>
#include <Helix/Engine/Script/Coroutines.h>
#include <Helix/Engine/Time/Time.h>
#include <Helix/Engine/Scene/SceneFile.h>
#include <Helix/Engine/Scene/SceneManager.h>
#include <Helix/Testing/Test.h>

#include <Helix/Math/Random.h>

#include <bit>
#include <chrono>
#include <thread>
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
#ifndef HX_TEST_MODULE
    // No shared libraries (the Web): its modules are linked into the program.
    return;
#else
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
#endif
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

HX_TEST(Scripts, TheScriptApiGivesTheSameResultsInBothLanguages)
{
    HX_REQUIRE(StartScripts());
    Calls().Clear();
    GameObject* host = GameObject::Create("Host");
    host->AddComponent(CSharp("CsScenario").type);
    AddScript(host, Cpp("CppScenario"));
    RunScriptPhase(ScriptMessage::Update);
    Array<String>& calls = Calls();
    HX_REQUIRE(calls.Size() == 2);
    // The C++ script's line starts with its probe's name, the C# one with the host's (Notes).
    StringView cpp = calls[0], cs = calls[1];
    cpp = cpp.Substr(cpp.Find("scenario:"));
    cs = cs.Substr(cs.Find("scenario:"));
    if (cpp != cs)
        std::printf("    C++: %.*s\n    C#:  %.*s\n", int(cpp.Size()), cpp.Data(), int(cs.Size()), cs.Data());
    HX_CHECK(cpp == cs);
    HX_CHECK(cs == "scenario:pos=(2.00, 2.00, 3.00);child=(2.00, 3.00, 3.00);found=True;same=True;clone=Probe(Clone):1;destroyed=True;roots=+1;gone=True");
    DestroyImmediate(host);
    Calls().Clear();
}

HX_TEST(Scripts, TheValueTypesKeepTheDocumentedValues)
{
    HX_REQUIRE(StartScripts());
    Calls().Clear();
    GameObject* host = GameObject::Create("V");
    host->AddComponent(CSharp("CsValueChecks").type);
    RunScriptPhase(ScriptMessage::Update);
    // The C# checks, then C#'s first value of the engine's generator after InitState(7), as C++ gets it.
    Random::InitState(7);
    String random = Format("V:cs.random:{}", std::bit_cast<i32>(Random::value()));
    HX_CHECK(Expect({random.CStr(), "V:cs.checks.done"}));
    DestroyImmediate(host);
}

namespace
{
    // A scene file with one script (its MonoScript the asset of `guidText`) holding `fields`, on Holder; Holder has a
    // child Pin.
    Array<MonoScript*>& SceneScripts()
    {
        static Array<MonoScript*> assets;
        return assets;
    }

    // The script assets ScriptScene registered go (with every registered asset).
    void ForgetSceneScripts()
    {
        ResetAssets();
        for (MonoScript* m : SceneScripts())
            DestroyImmediate(m, true);
        SceneScripts().Clear();
    }

    String ScriptScene(const engine::ScriptClass& script, const char* guidText, StringView fields)
    {
        Guid guid;
        Guid::TryParse(guidText, guid);
        MonoScript* asset = MonoScript::FromScriptClass(script);
        RegisterAssetObject(guid, 11500000, asset, true);
        SceneScripts().Push(asset);
        String text = String(R"(%YAML 1.1
%TAG !h! tag:helixengine,2026:
--- !h!1 &100
GameObject:
  m_Component:
  - component: {fileID: 101}
  - component: {fileID: 102}
  m_Name: Holder
  m_IsActive: 1
--- !h!4 &101
Transform:
  m_GameObject: {fileID: 100}
  m_Children:
  - {fileID: 201}
  m_Father: {fileID: 0}
--- !h!114 &102
GameBehaviour:
  m_GameObject: {fileID: 100}
  m_Enabled: 1
  m_Script: {fileID: 11500000, guid: )");
        text.Append(guidText);
        text.Append(", type: 3}\n");
        text.Append(fields);
        text.Append(R"(--- !h!1 &200
GameObject:
  m_Component:
  - component: {fileID: 201}
  m_Name: Pin
  m_IsActive: 1
--- !h!4 &201
Transform:
  m_GameObject: {fileID: 200}
  m_Children: []
  m_Father: {fileID: 101}
)");
        return text;
    }

    constexpr const char* kCsFields = R"(  count: 9
  speed: 2.5
  on: 0
  pace: 0
  title: read
  letter: 98
  big: 12345678901
  precise: 0.25
  numbers:
  - 1
  - 2
  words:
  - a
  - b c
  offset: {x: 1, y: 2, z: 3}
  tint: {r: 1, g: 0.5, b: 0, a: 1}
  home:
    position: {x: 4, y: 5, z: 6}
    wait: 4
    marks:
    - 7
  route:
  - position: {x: 0, y: 0, z: 1}
    wait: 1
    marks: []
  - position: {x: 0, y: 0, z: 2}
    wait: 2
    marks: []
  pair:
    a: 5
    b: five
  pairs:
  - a: 1
    b: one
  target: {fileID: 200}
  points:
  - {fileID: 201}
  - {fileID: 101}
  path:
  - {x: 1, y: 0, z: 0}
  corner:
    corners:
    - 4
    - 0
    - 6
    - 9
  secret: 11
  scratch: 5
  <Backed>k__BackingField: 8
)";

    // What CsFields reports of kCsFields, with `chain` nodes and the name of the holder.
    String CsFieldsReport(StringView holder, i32 chain)
    {
        String line = Format("{}:9,2.5,False,Slow,read,b,12345678901,0.25,1|2,a|b c,1/2/3,0.5;home=4/5/6/4/7;route=0/0/1/1|0/0/2/2;pair=5/five;pairs=1/one", holder);
        line.Append(Format(";target=Pin;points=Pin.Transform|{}.Transform;path=1/0/0;chain={};corners=4/0/6;secret=11;scratch=0;backed=8;property=0", holder, chain));
        return line;
    }

    GameObject* LoadedRoot(Scene s, Span<const u8> bytes)
    {
        Array<String> errors;
        bool read = ReadSceneFile(bytes, s, errors);
        for (const String& e : errors)
            std::printf("    %.*s\n", int(e.Size()), e.Data());
        Array<GameObject*> roots = s.GetRootGameObjects();
        return read && errors.Empty() && roots.Size() == 1 ? roots[0] : nullptr;
    }

    GameObject* LoadedRoot(Scene s, const String& text) { return LoadedRoot(s, Span<const u8>(reinterpret_cast<const u8*>(text.Data()), text.Size())); }

    bool Reports(GameObject* g, const String& expected)
    {
        Calls().Clear();
        if (!SendScriptMessage(g, "Report"))
            return false;
        return Expect({expected.CStr()});
    }
}

HX_TEST(Scripts, CSharpScriptFieldsInSceneFiles)
{
    HX_REQUIRE(StartScripts());
    Calls().Clear();
    Scene s = SceneManager::CreateScene("CsFields");
    GameObject* holder = LoadedRoot(s, ScriptScene(CSharp("CsFields"), "e0000000000000000000000000000010", kCsFields));
    HX_REQUIRE(holder != nullptr);
    // A field the file does not name keeps its value: chain was null, a Serializable class field is an object.
    HX_CHECK(Reports(holder, CsFieldsReport("Holder", 1)));
    // Written: the serialized fields in order; NonSerialized, readonly, static, properties, dictionaries and
    // multidimensional arrays left out; a backing field with SerializeField by its name.
    String text, error;
    HX_REQUIRE(WriteSceneYaml(s, text, error));
    usize fields = text.View().Find("  count: 9\n  speed: 2.5\n  on: 0\n  pace: 0\n  title: read\n");
    HX_CHECK(fields != StringView::npos && text.View().Find("  m_Script: {fileID: 11500000, guid: e0000000000000000000000000000010") < fields);
    HX_CHECK(text.View().Contains("  letter: 98\n  big: 12345678901\n  precise: 0.25\n"));
    HX_CHECK(text.View().Contains("  offset: {x: 1, y: 2, z: 3}\n  tint: {r: 1, g: 0.5, b: 0, a: 1}\n  home:\n    position: {x: 4, y: 5, z: 6}\n"));
    HX_CHECK(text.View().Contains("  target: {fileID: 200}\n") && text.View().Contains("  secret: 11\n  <Backed>k__BackingField: 8\n"));
    for (const char* absent : {"scratch", "fixedValue", "shared", "Property", "map:", "grid:"})
        HX_CHECK(!text.View().Contains(absent));
    // The chain of a class holding itself, to the depth limit.
    i32 depth = 0;
    for (usize at = text.View().Find("chain:"); at != StringView::npos; at = text.View().Find("next:", at + 1))
        depth++;
    HX_CHECK(depth == 11);
    Scene t = SceneManager::CreateScene("CsFieldsCopy");
    GameObject* copy = LoadedRoot(t, text);
    HX_REQUIRE(copy != nullptr);
    HX_CHECK(Reports(copy, CsFieldsReport("Holder", 11)));
    for (bool trees : {false, true})
    {
        Array<u8> bytes;
        HX_REQUIRE(WriteSceneBinary(t, bytes, error, trees));
        Scene b = SceneManager::CreateScene(trees ? "CsFieldsTrees" : "CsFieldsBinary");
        GameObject* fromBinary = LoadedRoot(b, Span<const u8>(bytes));
        HX_REQUIRE(fromBinary != nullptr);
        HX_CHECK(Reports(fromBinary, CsFieldsReport("Holder", 11)));
        SceneManager::UnloadSceneAsync(b);
    }
    // Instantiate: the fields through their binary form, references to the copy's own objects.
    GameObject* clone = Instantiate(copy);
    HX_REQUIRE(clone != nullptr);
    HX_CHECK(Reports(clone, CsFieldsReport("Holder(Clone)", 11)));
    DestroyImmediate(clone);
    SceneManager::UnloadSceneAsync(s);
    SceneManager::UnloadSceneAsync(t);
    SceneManager::ProcessSceneLoads();
    ForgetSceneScripts();
    Calls().Clear();
}

HX_TEST(Scripts, DerivedCSharpScriptFields)
{
    HX_REQUIRE(StartScripts());
    Calls().Clear();
    Scene s = SceneManager::CreateScene("CsMoreFields");
    String fields = String(kCsFields);
    fields.Append("  extra: 5\n");
    GameObject* holder = LoadedRoot(s, ScriptScene(CSharp("CsMoreFields"), "e0000000000000000000000000000011", fields));
    HX_REQUIRE(holder != nullptr);
    HX_CHECK(Reports(holder, CsFieldsReport("Holder", 1)));
    Calls().Clear();
    HX_CHECK(SendScriptMessage(holder, "ReportExtra") && Expect({"Holder:extra=5"}));
    String text, error;
    HX_REQUIRE(WriteSceneYaml(s, text, error));
    HX_CHECK(text.View().Contains("  <Backed>k__BackingField: 8\n  extra: 5\n"));
    SceneManager::UnloadSceneAsync(s);
    SceneManager::ProcessSceneLoads();
    ForgetSceneScripts();
    Calls().Clear();
}

HX_TEST(Scripts, SerializeReferenceGraphs)
{
    HX_REQUIRE(StartScripts());
    Calls().Clear();
    Scene s = SceneManager::CreateScene("CsRefs");
    GameObject* holder = LoadedRoot(s, ScriptScene(CSharp("CsRefs"), "e0000000000000000000000000000012", ""));
    HX_REQUIRE(holder != nullptr);
    HX_CHECK(SendScriptMessage(holder, "Build"));
    const char* built = "Holder:pet=dog:Rex;zoo=dog:Rex|cat:7|null;any=cat:3;none=null;shared=True;cycle=True;same=False";
    HX_CHECK(Reports(holder, String(built)));
    String text, error;
    HX_REQUIRE(WriteSceneYaml(s, text, error));
    HX_CHECK(text.View().Contains("  pet:\n    rid: 1000\n  zoo:\n  - rid: 1000\n  - rid: 1001\n  - rid: -2\n  any:\n    rid: 1002\n  none:\n    rid: -2\n"));
    HX_CHECK(text.View().Contains("  shelter:\n    resident:\n      rid: 1001\n  references:\n    version: 2\n    RefIds:\n    - rid: 1000\n"));
    HX_CHECK(text.View().Contains("      type: {class: CsDog, ns: Tests, asm: HelixEngine.ScriptTests}\n      data:\n        name: Rex\n        friend:\n          rid: 1000\n"));
    HX_CHECK(text.View().Contains("    - rid: 1002\n      type: {class: CsCat, ns: Tests, asm: HelixEngine.ScriptTests}\n      data:\n        lives: 3\n    - rid: -2\n"));
    // Written again: the objects keep their rids.
    String again;
    HX_REQUIRE(WriteSceneYaml(s, again, error));
    HX_CHECK(again == text);
    const char* read = "Holder:pet=dog:Rex;zoo=dog:Rex|cat:7|null;any=cat:3;none=null;shared=True;cycle=True;same=False";
    Scene t = SceneManager::CreateScene("CsRefsCopy");
    GameObject* copy = LoadedRoot(t, text);
    HX_REQUIRE(copy != nullptr);
    HX_CHECK(Reports(copy, String(read)));
    for (bool trees : {false, true})
    {
        Array<u8> bytes;
        HX_REQUIRE(WriteSceneBinary(t, bytes, error, trees));
        Scene b = SceneManager::CreateScene(trees ? "CsRefsTrees" : "CsRefsBinary");
        GameObject* fromBinary = LoadedRoot(b, Span<const u8>(bytes));
        HX_REQUIRE(fromBinary != nullptr);
        HX_CHECK(Reports(fromBinary, String(read)));
        SceneManager::UnloadSceneAsync(b);
    }
    // Instantiate copies the graph: new objects, the same sharing.
    HX_CHECK(Reports(copy, String(read)) || true);
    GameObject* clone = Instantiate(copy);
    HX_REQUIRE(clone != nullptr);
    HX_CHECK(Reports(clone, String("Holder(Clone):pet=dog:Rex;zoo=dog:Rex|cat:7|null;any=cat:3;none=null;shared=True;cycle=True;same=False")));
    DestroyImmediate(clone);
    SceneManager::UnloadSceneAsync(s);
    SceneManager::UnloadSceneAsync(t);
    SceneManager::ProcessSceneLoads();
    ForgetSceneScripts();
    Calls().Clear();
}

HX_TEST(Scripts, SerializationCallbacksAndDefaults)
{
    HX_REQUIRE(StartScripts());
    Calls().Clear();
    Scene s = SceneManager::CreateScene("CsCallbacks");
    const char* fields = R"(  value: 4
  inline:
    tag: inline
  referenced:
    rid: 1000
  references:
    version: 2
    RefIds:
    - rid: 1000
      type: {class: CsHook, ns: Tests, asm: HelixEngine.ScriptTests}
      data:
        tag: ref
)";
    GameObject* holder = LoadedRoot(s, ScriptScene(CSharp("CsCallbacks"), "e0000000000000000000000000000013", fields));
    HX_REQUIRE(holder != nullptr);
    // After the read: the host's first, then the objects it holds (inline, then its managed references).
    HX_CHECK(Reports(holder, String("Holder:after:host,after:inline,after:ref;derived=40")));
    String text, error;
    HX_REQUIRE(WriteSceneYaml(s, text, error));
    HX_CHECK(Reports(holder, String("Holder:before:host,before:inline,before:ref;derived=40")));
    HX_CHECK(text.View().Contains("  value: 4\n  inline:\n    tag: inline\n  referenced:\n    rid: 1000\n"));
    // A script added by AddComponent has its field initializers' values.
    GameObject* g = GameObject::Create("G");
    HX_REQUIRE(g->AddComponent(CSharp("CsFields").type) != nullptr);
    Calls().Clear();
    HX_CHECK(SendScriptMessage(g, "Report") && Calls().Size() == 1 &&
             Calls()[0].View().StartsWith("G:3,1.5,True,Fast,fields,a,0,0,null,null,0/0/0,1;home=null;route=null;pair=0/;pairs=null;target=none;points=null"));
    if (Calls().Size() == 1)
        std::printf("    %s\n", Calls()[0].CStr());
    Calls().Clear();
    DestroyImmediate(g);
    SceneManager::UnloadSceneAsync(s);
    SceneManager::ProcessSceneLoads();
    ForgetSceneScripts();
    Calls().Clear();
}

namespace
{
    // A modification of the prefab's script (fileID 102).
    String ScriptModification(const char* guid, const char* path, const char* value, i64 reference = 0)
    {
        return Format("    - target: {{fileID: 102, guid: {}, type: 3}}\n      propertyPath: {}\n      value: {}\n      objectReference: {{fileID: {}}}\n", guid, path,
                      value, reference);
    }

    // A scene with Root holding an instance of the prefab of `guid` with `mods`.
    String PrefabInstanceScene(const String& guid, const String& mods)
    {
        String text = String(R"(%YAML 1.1
%TAG !h! tag:helixengine,2026:
--- !h!1 &1
GameObject:
  m_Component:
  - component: {fileID: 2}
  m_Name: Root
  m_IsActive: 1
--- !h!4 &2
Transform:
  m_GameObject: {fileID: 1}
  m_Children:
  - {fileID: 9001}
  m_Father: {fileID: 0}
--- !h!1001 &5000
PrefabInstance:
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {fileID: 2}
    m_Modifications:
)");
        text.Append(mods);
        text.Append(R"(    m_RemovedComponents: []
    m_RemovedGameObjects: []
    m_AddedGameObjects: []
    m_AddedComponents: []
  m_SourcePrefab: {fileID: 100100000, guid: )");
        text.Append(guid);
        text.Append(R"(, type: 3}
--- !h!4 &9001 stripped
Transform:
  m_CorrespondingSourceObject: {fileID: 101, guid: )");
        text.Append(guid);
        text.Append(R"(, type: 3}
  m_PrefabInstance: {fileID: 5000}
  m_PrefabAsset: {fileID: 0}
--- !h!1660057539 &9223372036854775807
SceneRoots:
  m_Roots:
  - {fileID: 2}
)");
        return text;
    }
}

HX_TEST(Scripts, CSharpScriptFieldsInPrefabs)
{
    HX_REQUIRE(StartScripts());
    Calls().Clear();
    const char* guid = "e0000000000000000000000000000014";
    Guid g;
    Guid::TryParse(guid, g);
    Array<String> errors;
    GameObject* prefab = LoadPrefab(StringView(ScriptScene(CSharp("CsFields"), "e0000000000000000000000000000015", kCsFields)), errors, g);
    for (const String& e : errors)
        std::printf("    %.*s\n", int(e.Size()), e.Data());
    HX_REQUIRE(prefab != nullptr);
    String mods = ScriptModification(guid, "count", "12");
    mods.Append(ScriptModification(guid, "words.Array.data[1]", "z"));
    mods.Append(ScriptModification(guid, "home.wait", "6"));
    Scene s = SceneManager::CreateScene("CsFieldsPrefab");
    String file = PrefabInstanceScene(String(guid), mods);
    errors.Clear();
    HX_CHECK(ReadSceneFile(StringView(file), s, errors) && errors.Empty());
    for (const String& e : errors)
        std::printf("    %.*s\n", int(e.Size()), e.Data());
    Array<GameObject*> roots = s.GetRootGameObjects();
    HX_REQUIRE(roots.Size() == 1 && roots[0]->transform()->childCount() == 1);
    GameObject* holder = roots[0]->transform()->GetChild(0)->gameObject();
    Calls().Clear();
    HX_CHECK(SendScriptMessage(holder, "Report") && Calls().Size() == 1 && Calls()[0].View().StartsWith("Holder:12,2.5,False,Slow,read,b,12345678901,0.25,1|2,a|z,1/2/3,0.5;home=4/5/6/6/7"));
    if (Calls().Size() == 1)
        std::printf("    %s\n", Calls()[0].CStr());
    SceneManager::UnloadSceneAsync(s);
    SceneManager::ProcessSceneLoads();
    ResetAssets();
    DestroyImmediate(prefab, true);
    ForgetSceneScripts();
    Calls().Clear();
}

namespace
{
    // Frames by hand: 1/16 s each, one fixed step of 1/16 s per frame.
    struct CoroutineFrames
    {
        struct Clock final : TimeClock
        {
            double now = 10;
            double Now() override { return now; }
        } clock;

        CoroutineFrames()
        {
            Time::SetClock(&clock);
            Time::Reset();
            Time::SetFixedDeltaTime(0.0625f);
            PlayerLoop::SetPlayerLoop(PlayerLoop::GetDefaultPlayerLoop());
            PlayerLoop::RunFrame();
            Calls().Clear();
        }
        ~CoroutineFrames()
        {
            Time::SetClock(nullptr);
            Time::Reset();
            Calls().Clear();
        }
        void Frame()
        {
            clock.now += 0.0625;
            PlayerLoop::RunFrame();
        }
    };

    GameObject* WithRoutines(const char* name, bool trace)
    {
        GameObject* g = GameObject::Create(name);
        g->AddComponent(CSharp("CsRoutines").type);
        if (!trace)
            SendScriptMessage(g, "NoTrace");
        return g;
    }
}

HX_TEST(Scripts, CSharpCoroutinesResumeAtTheReferencesPoints)
{
    HX_REQUIRE(StartScripts());
    CoroutineFrames frames;
    GameObject* g = WithRoutines("C", true);
    frames.Frame();
    // The same trace as the C++ Routines.
    HX_CHECK(Expect({"C:co.start", "C:FixedUpdate", "C:Update", "C:LateUpdate"}));
    frames.Frame();
    HX_CHECK(Expect({"C:FixedUpdate", "C:Update", "C:co.frame", "C:LateUpdate"}));
    frames.Frame();
    HX_CHECK(Expect({"C:FixedUpdate", "C:co.fixed", "C:Update", "C:LateUpdate", "C:co.end"}));
    frames.Frame();
    HX_CHECK(Expect({"C:FixedUpdate", "C:Update", "C:LateUpdate"}));
    SendScriptMessage(g, "Ready");
    frames.Frame();
    HX_CHECK(Expect({"C:FixedUpdate", "C:Update", "C:co.ready", "C:inner.start", "C:LateUpdate"}));
    frames.Frame();
    HX_CHECK(Expect({"C:FixedUpdate", "C:Update", "C:inner.end", "C:co.done", "C:LateUpdate"}));
    DestroyImmediate(g);
    HX_CHECK(CoroutineCount() == 0);
}

HX_TEST(Scripts, CSharpCoroutinesStopByTheReferencesRules)
{
    HX_REQUIRE(StartScripts());
    CoroutineFrames frames;
    GameObject* g = WithRoutines("S", false);
    frames.Frame();
    auto* b = static_cast<GameBehaviour*>(g->GetComponent(CSharp("CsRoutines").type));
    Calls().Clear();
    auto ticks = [&] {
        Array<String> left;
        for (const String& c : Calls())
            if (!c.View().EndsWith("Update"))
                left.Push(c);
        Calls() = Move(left);
    };
    // Disabling the behaviour does not stop its coroutines; StopCoroutine(Coroutine) does.
    SendScriptMessage(g, "StartForever");
    b->SetEnabled(false);
    frames.Frame();
    ticks();
    HX_CHECK(Expect({"S:tick", "S:tick"}));
    b->SetEnabled(true);
    SendScriptMessage(g, "StopRunning");
    frames.Frame();
    ticks();
    HX_CHECK(Expect({}));
    // By name; StopAllCoroutines; an inactive GameObject stops the others.
    SendScriptMessage(g, "StartForeverByName");
    SendScriptMessage(g, "StopForeverByName");
    frames.Frame();
    ticks();
    HX_CHECK(Expect({"S:tick"}));
    SendScriptMessage(g, "StartForever");
    SendScriptMessage(g, "StartForever");
    SendScriptMessage(g, "StopAll");
    HX_CHECK(CoroutineCount() == 0);
    SendScriptMessage(g, "StartForever");
    HX_CHECK(CoroutineCount() == 1);
    g->SetActive(false);
    HX_CHECK(CoroutineCount() == 0);
    // Started on an inactive GameObject: the reference's error, null.
    g->SetActive(true);
    SendScriptMessage(g, "Remember");
    g->SetActive(false);
    GameObject* other = WithRoutines("O", false);
    Errors errors;
    AddLogSink(&CountErrors, &errors);
    Calls().Clear();
    SendScriptMessage(other, "StartOnRemembered");
    RemoveLogSink(&CountErrors, &errors);
    HX_CHECK(Expect({"O:remembered.null"}));
    HX_CHECK(errors.texts.Size() == 1 && errors.texts[0].View().Contains("Coroutine couldn't be started because the the game object 'S' is inactive!"));
    g->SetActive(true);
    // A coroutine that ends without yielding answers null; an exception ends it and is logged.
    Calls().Clear();
    SendScriptMessage(g, "StartEnds");
    HX_CHECK(Expect({"S:ends.null"}));
    AddLogSink(&CountErrors, &errors);
    errors.texts.Clear();
    SendScriptMessage(g, "StartThrows");
    frames.Frame();
    RemoveLogSink(&CountErrors, &errors);
    ticks();
    HX_CHECK(Expect({"S:throws"}) && errors.texts.Size() == 1 && errors.texts[0].View().Contains("coroutine failure") && CoroutineCount() == 0);
    // Destroying the behaviour stops its coroutines and cancels its destroyCancellationToken.
    SendScriptMessage(g, "StartForever");
    SendScriptMessage(g, "WatchDestroy");
    Calls().Clear();
    DestroyImmediate(b);
    HX_CHECK(CoroutineCount() == 0 && Expect({"destroyed.token"}));
    DestroyImmediate(g);
    DestroyImmediate(other);
}

HX_TEST(Scripts, CSharpCoroutinesWaitForTimeAndPredicates)
{
    HX_REQUIRE(StartScripts());
    CoroutineFrames frames;
    GameObject* g = WithRoutines("T", false);
    frames.Frame();
    Calls().Clear();
    SendScriptMessage(g, "StartSeconds");
    SendScriptMessage(g, "StartTimeout");
    String text;
    for (int i = 0; i < 12; i++)
    {
        frames.Frame();
        for (const String& c : Calls())
            if (!c.View().EndsWith("Update"))
                text.Append(Format("{}:{} ", i, c));
        Calls().Clear();
        if (i == 6)
            SendScriptMessage(g, "Ready");
    }
    // The C++ Routines' timeline.
    HX_CHECK(text == StringView("1:T:timeout 1:T:after.timeout 3:T:seconds 7:T:after.while 7:T:realtime "));
    if (text != StringView("1:T:timeout 1:T:after.timeout 3:T:seconds 7:T:after.while 7:T:realtime "))
        std::printf("    %s\n", text.CStr());
    DestroyImmediate(g);
}

HX_TEST(Scripts, AwaitablesResumeAtTheReferencesPoints)
{
    HX_REQUIRE(StartScripts());
    CoroutineFrames frames;
    GameObject* g = GameObject::Create("A");
    g->AddComponent(CSharp("CsAwaits").type);
    Calls().Clear();
    SendScriptMessage(g, "StartSequence");
    HX_CHECK(Expect({"A:a.start"}));
    frames.Frame();
    HX_CHECK(Expect({"A:a.frame"}));
    frames.Frame();
    HX_CHECK(Expect({"A:a.fixed", "A:a.end"}));
    frames.Frame();
#if HX_PLATFORM_WEB
    // The Web has no threads: BackgroundThreadAsync continues on the main thread at once.
    HX_CHECK(Expect({"A:a.result=7", "A:a.main bg=False main=True"}));
#else
    HX_CHECK(Expect({"A:a.result=7"}));
    // To a thread pool thread and back: the main thread takes it in a later frame's Update.
    for (int i = 0; i < 400 && Calls().Empty(); i++)
    {
        std::this_thread::sleep_for(std::chrono::milliseconds(1));
        frames.Frame();
    }
    HX_CHECK(Expect({"A:a.main bg=True main=True"}));
#endif
    SendScriptMessage(g, "Complete");
    HX_CHECK(Expect({"A:a.source", "A:seq.done"}));
    // Cancellation (Cancel and a token) and a completion source with a result.
    SendScriptMessage(g, "StartCancel");
    HX_CHECK(Expect({"A:canceled", "A:token", "A:source=value again=False"}));
    HX_CHECK(CoroutineCount() == 0);
    // A Task continues through the main thread's context in the next frame's Update; awaitables of one point in the
    // order they were awaited; a coroutine yields an awaitable.
    SendScriptMessage(g, "StartTask");
    SendScriptMessage(g, "StartOrder");
    SendScriptMessage(g, "StartCoroutineAwait");
    HX_CHECK(Expect({}));
    frames.Frame();
    HX_CHECK(Expect({"A:order.x", "A:order.y", "A:task"}));
    frames.Frame();
    HX_CHECK(Expect({"A:co.awaited"}));
    DestroyImmediate(g);
    HX_CHECK(CoroutineCount() == 0);
}

HX_TEST(Scripts, CppApiCoroutinesGiveTheCSharpTrace)
{
    HX_REQUIRE(StartScripts());
    CoroutineFrames frames;
    GameObject* g = GameObject::Create("C");
    HX_REQUIRE(AddScript(g, Cpp("CppApiRoutines")) != nullptr);
    frames.Frame();
    // CSharpCoroutinesResumeAtTheReferencesPoints' trace.
    HX_CHECK(Expect({"C:co.start", "C:FixedUpdate", "C:Update", "C:LateUpdate"}));
    frames.Frame();
    HX_CHECK(Expect({"C:FixedUpdate", "C:Update", "C:co.frame", "C:LateUpdate"}));
    frames.Frame();
    HX_CHECK(Expect({"C:FixedUpdate", "C:co.fixed", "C:Update", "C:LateUpdate", "C:co.end"}));
    frames.Frame();
    HX_CHECK(Expect({"C:FixedUpdate", "C:Update", "C:LateUpdate"}));
    SendScriptMessage(g, "Ready");
    frames.Frame();
    HX_CHECK(Expect({"C:FixedUpdate", "C:Update", "C:co.ready", "C:inner.start", "C:LateUpdate"}));
    frames.Frame();
    HX_CHECK(Expect({"C:FixedUpdate", "C:Update", "C:inner.end", "C:co.done", "C:LateUpdate"}));
    DestroyImmediate(g);
    HX_CHECK(CoroutineCount() == 0);
}
