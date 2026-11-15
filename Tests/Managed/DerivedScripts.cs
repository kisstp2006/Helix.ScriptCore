using HelixEngine;
using ScriptCoreTests;

namespace Tests
{
    // A C# ScriptableObject class below the C++ CppShape: made as CppShape's derived C++ class; its Awake runs instead of
    // the C++ one, OnEnable is the C++ class's; C++ calls of Describe reach the override, base.Describe the C++ one.
    public class CsShape : CppShape
    {
        public int csAwakes;
        public string label = "cs";

        void Awake() => csAwakes++;

        public override string Describe() => "cs:" + base.Describe();
    }

    // A C# GameBehaviour below the C++ CppMover: Start is the C++ class's, Update the C# class's; the C++ Step calls Speed.
    public class CsMover : CppMover
    {
        public float factor = 3;

        public override float Speed() => factor;

        void Update() => Step();
    }

    // Below a C++ class that is abstract in C++: implements its pure virtual function, overrides its virtual property.
    public class CsTriangle : CppFigure
    {
        public override int Corners() => 3;

        public override int order => base.order + 1;
    }

    public class CsDerivedTests : GameBehaviour
    {
        void Start()
        {
            var shape = ScriptableObject.CreateInstance<CsShape>();
            shape.sides = 5;
            Notes.Note(this, $"shape:{shape is CppShape}:{shape.awakes}:{shape.enables}:{shape.csAwakes}:{shape.Report()}:{shape.Describe()}");
            CsShape copy = Instantiate(shape);
            copy.label = "copy";
            Notes.Note(this, $"copy:{copy.GetType().Name}:{copy.sides}:{copy.enables}:{copy.csAwakes}:{copy.Report()}:{shape.label}");
            DestroyImmediate(shape);
            DestroyImmediate(copy);
            var triangle = ScriptableObject.CreateInstance<CsTriangle>();
            CsTriangle triangleCopy = Instantiate(triangle);
            Notes.Note(this, $"figure:{triangle.Report()}:{triangle.order}:{triangleCopy.Report()}");
            DestroyImmediate(triangle);
            DestroyImmediate(triangleCopy);
        }
    }
}
