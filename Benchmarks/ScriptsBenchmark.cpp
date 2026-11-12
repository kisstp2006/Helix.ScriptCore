#include <Helix/Engine/GameObject/Lifecycle.h>
#include <Helix/Engine/Script/ScriptRuntime.h>
#include <Helix/Scripting/Host/DotnetHost.h>
#include <Helix/Testing/Bench.h>

#include <bit>
#include <cstdio>

using namespace helix;
using namespace helix::engine;

// One Update phase over 10 000 C# scripts: one call into C#, which calls each script (the M6 exit asks under 0.3 ms).
HX_BENCH(ScriptsUpdate10kCSharp)
{
    using namespace scripting;
    String error;
    DotnetHostDesc desc;
    desc.runtimeAssembly = HX_TEST_RUNTIME_ASSEMBLY;
    if (!DotnetHost::Start(desc, error))
    {
        std::printf("no .NET host: %.*s\n", int(error.Size()), error.Data());
        return;
    }
    (void)FindType("HelixEngine.Object");
    AssemblyHandle core = DotnetHost::LoadAssembly(HX_TEST_CORE_MODULE, error);
    auto start = std::bit_cast<i32 (*)()>(DotnetHost::GetFunction(core, "HelixEngine.Internal.ScriptBridge", "Start", error));
    auto load = std::bit_cast<i32 (*)(const char*, i32)>(DotnetHost::GetFunction(core, "HelixEngine.Internal.ScriptBridge", "LoadScripts", error));
    StringView scripts = HX_TEST_SCRIPTS;
    if (!start || !load || start() != 0 || load(scripts.Data(), i32(scripts.Size())) <= 0)
        return;
    const ScriptClass* counter = FindScriptClass("HelixEngine.ScriptTests", "Tests", "CsCounter");
    if (!counter)
        return;
    Array<GameObject*> objects;
    for (u32 i = 0; i < 10000; i++)
    {
        GameObject* g = GameObject::Create("Counter");
        AddScript(g, *counter);
        objects.Push(g);
    }
    RunScriptPhase(ScriptMessage::Update);
    while (state.KeepRunning())
        RunScriptPhase(ScriptMessage::Update);
    for (GameObject* g : objects)
        DestroyImmediate(g);
}
