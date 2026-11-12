#pragma once

#include <Helix/Core/Strings/Format.h>
#include <Helix/Engine/Script/GameBehaviour.h>

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
    };
}
