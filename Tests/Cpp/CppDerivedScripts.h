#pragma once

#include <Helix/Core/Strings/Format.h>
#include <Helix/Engine/Script/GameBehaviour.h>
#include <Helix/Engine/Script/ScriptableObject.h>

// C++ script classes C# script classes derive from (CsShape, CsMover): their objects are made as these classes, a
// tagged virtual function reaches the C# override, the messages the C# class has none of run the C++ ones.
namespace helix::scriptcore::testing
{
    HX_CLASS(Namespace = "ScriptCoreTests")
    class CppShape : public engine::ScriptableObject
    {
    public:
        HX_FIELD() i32 sides = 3;
        HX_FIELD() i32 awakes = 0;
        HX_FIELD() i32 enables = 0;

        HX_FUNCTION() virtual String Describe() const { return Format("shape:{}", sides); }
        // C++ calling the virtual function.
        HX_FUNCTION() String Report() const { return Format("[{}]", Describe()); }

    private:
        void Awake() { awakes++; }
        void OnEnable() { enables++; }
    };

    // Abstract in C++: only the C# classes below it are made (as its derived class); a virtual property too.
    HX_CLASS(Namespace = "ScriptCoreTests")
    class CppFigure : public engine::ScriptableObject
    {
    public:
        HX_FUNCTION() virtual i32 Corners() const = 0;
        HX_PROPERTY() virtual i32 order() const { return 1; }
        HX_FUNCTION() String Report() const { return Format("{}/{}", Corners(), order()); }
    };

    HX_CLASS(Namespace = "ScriptCoreTests")
    class CppMover : public engine::GameBehaviour
    {
    public:
        HX_FIELD() i32 starts = 0;
        HX_FIELD() f32 distance = 0;

        HX_FUNCTION() virtual f32 Speed() const { return 1; }
        HX_FUNCTION() void Step() { distance += Speed(); }

    private:
        void Start() { starts++; }
    };
}
