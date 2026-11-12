#pragma once

#include <Cpp/CppScripts.h>

#include <Helix/Engine/Script/ScriptRuntime.h>
#include <Helix/Scripting/Bindings/Interop.h>
#include <Helix/Scripting/Host/DotnetHost.h>

#include <bit>
#include <cstdio>

HX_BINDINGS_EXPORT void HxBindings_ScriptCoreTests_Publish();

// The engine's C# side and the test scripts of both languages, started once per process: the .NET host, the
// HelixEngine.CoreModule assembly (its ScriptBridge.Start), the C# test scripts, the C++ test module.
namespace helix::scriptcore::testing
{
    inline void NoteFromCSharp(const char* text, i32 size) { Calls().Push(String(StringView(text, usize(size)))); }

    inline bool StartScripts()
    {
        static int state = 0;
        if (state != 0)
            return state > 0;
        state = -1;
        using namespace scripting;
        RegisterNativeExport("ScriptCoreTests.Note", std::bit_cast<const void*>(&NoteFromCSharp));
        String error;
        HxBindings_ScriptCoreTests_Publish();
        if (!engine::RegisterLinkedGameModule("ScriptCoreTests", error))
        {
            std::printf("    %.*s\n", int(error.Size()), error.Data());
            return false;
        }
        DotnetHostDesc desc;
        desc.runtimeAssembly = HX_TEST_RUNTIME_ASSEMBLY;
        if (!DotnetHost::Start(desc, error))
        {
            std::printf("    %.*s\n", int(error.Size()), error.Data());
            return false;
        }
        // Engine types are needed before the C# side takes the engine's bindings.
        (void)FindType("HelixEngine.Object");
        AssemblyHandle core = DotnetHost::LoadAssembly(HX_TEST_CORE_MODULE, error);
        auto start = std::bit_cast<i32 (*)()>(DotnetHost::GetFunction(core, "HelixEngine.Internal.ScriptBridge", "Start", error));
        auto load = std::bit_cast<i32 (*)(const char*, i32)>(DotnetHost::GetFunction(core, "HelixEngine.Internal.ScriptBridge", "LoadScripts", error));
        if (!start || !load || start() != 0)
        {
            std::printf("    the engine's C# side did not start: %.*s\n", int(error.Size()), error.Data());
            return false;
        }
        StringView scripts = HX_TEST_SCRIPTS;
        if (load(scripts.Data(), i32(scripts.Size())) <= 0)
        {
            std::printf("    no C# test scripts were registered\n");
            return false;
        }
        state = 1;
        return true;
    }

    inline const engine::ScriptClass& CSharp(const char* name)
    {
        const engine::ScriptClass* s = engine::FindScriptClass("HelixEngine.ScriptTests", "Tests", name);
        HX_ASSERT(s != nullptr);
        return *s;
    }

    inline const engine::ScriptClass& Cpp(const char* name)
    {
        const engine::ScriptClass* s = engine::FindScriptClass("ScriptCoreTests", "ScriptCoreTests", name);
        HX_ASSERT(s != nullptr);
        return *s;
    }
}
