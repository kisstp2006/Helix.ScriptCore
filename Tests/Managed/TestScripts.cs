using System;
using System.Text;
using Helix.Scripting;
using HelixEngine;

namespace Tests
{
    // The messages the C# test scripts get, written to the list of the C++ test program (its export by name).
    static unsafe class Notes
    {
        static delegate* unmanaged[Cdecl]<byte*, int, void> s_Note;

        public static void Note(GameBehaviour b, string message)
        {
            if (s_Note == null)
                s_Note = (delegate* unmanaged[Cdecl]<byte*, int, void>)Host.FindExport("ScriptCoreTests.Note");
            byte[] bytes = Encoding.UTF8.GetBytes(b.gameObject.name + ":" + message);
            fixed (byte* p = bytes)
                s_Note(p, bytes.Length);
        }
    }

    public class CsRecorder : GameBehaviour
    {
        void Awake() => Notes.Note(this, "cs.Awake");
        void OnEnable() => Notes.Note(this, "cs.OnEnable");
        void Start() => Notes.Note(this, "cs.Start");
        void FixedUpdate() => Notes.Note(this, "cs.FixedUpdate");
        void Update() => Notes.Note(this, "cs.Update");
        void LateUpdate() => Notes.Note(this, "cs.LateUpdate");
        void OnDisable() => Notes.Note(this, "cs.OnDisable");
        void OnDestroy() => Notes.Note(this, "cs.OnDestroy");
        void OnApplicationFocus(bool focus) => Notes.Note(this, focus ? "cs.OnApplicationFocus(true)" : "cs.OnApplicationFocus(false)");
    }

    [DefaultExecutionOrder(-10)]
    public class CsEarly : GameBehaviour
    {
        void Update() => Notes.Note(this, "cs.Early.Update");
    }

    // Throws in its Update: logged, the scripts after it are still called.
    public class CsThrower : GameBehaviour
    {
        void Update()
        {
            Notes.Note(this, "cs.Thrower.Update");
            throw new InvalidOperationException("thrown by CsThrower");
        }
    }

    // A message of a C# base class (private there) and an empty one: both count.
    public class CsBase : GameBehaviour
    {
        void Update() => Notes.Note(this, "cs.Base.Update");
        void LateUpdate() { }
    }

    public class CsDerived : CsBase
    {
    }

    // C# adds a script in Start: AddComponent<T> of a C# script type, and the script finds itself.
    public class CsAdder : GameBehaviour
    {
        void Start()
        {
            CsRecorder added = gameObject.AddComponent<CsRecorder>();
            Notes.Note(this, added != null && gameObject.GetComponent<CsRecorder>() == added ? "cs.Adder.added" : "cs.Adder.failed");
        }
    }

    // Disables itself in Update; its destroyed self compares equal to null.
    public class CsSelfDisabler : GameBehaviour
    {
        void Update()
        {
            Notes.Note(this, "cs.SelfDisabler.Update");
            enabled = false;
        }

        void OnDestroy() => Notes.Note(this, this != null && (bool)this ? "cs.SelfDisabler.OnDestroy(alive)" : "cs.SelfDisabler.OnDestroy(dead)");
    }

    public class CsCounter : GameBehaviour
    {
        public static long updates;
        void Update() => updates++;
    }
}
