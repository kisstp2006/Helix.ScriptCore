#include <Support/ScriptHost.h>

#include <Helix/Core/Logging/Log.h>
#include <Helix/Engine/Application/Application.h>
#include <Helix/Engine/GameObject/Lifecycle.h>
#include <Helix/Testing/Test.h>

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
