#pragma once

#include <Helix/Core/Strings/Format.h>
#include <Helix/Engine/Script/GameBehaviour.h>
#include <Helix/Scripting/Bindings/Interop.h>

#include <bit>

// C++ scripts of a game module loaded at run time (ScriptCoreModule, a shared library against the shared engine).
// They write to the test program's list through its export by name, as the C# test scripts do.
namespace helix::scriptcore::module
{
    inline void Note(const engine::GameBehaviour* b, const char* message)
    {
        using NoteFunction = void (*)(const char* text, i32 size);
        auto note = std::bit_cast<NoteFunction>(scripting::FindNativeExport("ScriptCoreTests.Note"));
        String text = Format("{}:{}", b->gameObject() ? b->gameObject()->Name() : StringView("?"), message);
        if (note)
            note(text.Data(), i32(text.Size()));
    }

    HX_CLASS(Namespace = "ScriptCoreModule")
    class ModuleRecorder : public engine::GameBehaviour
    {
        void Awake() { Note(this, "mod.Awake"); }
        void OnEnable() { Note(this, "mod.OnEnable"); }
        void Start() { Note(this, "mod.Start"); }
        void Update() { Note(this, "mod.Update"); }
        void OnDisable() { Note(this, "mod.OnDisable"); }
        void OnDestroy() { Note(this, "mod.OnDestroy"); }
        void Ping() { Note(this, "mod.Ping"); }
    };
}
