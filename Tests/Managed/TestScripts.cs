using System;
using System.Text;
using Helix.Scripting;
using HelixEngine;
using HelixEngine.SceneManagement;

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

    // SendMessage targets: a value of a type, a number converted, no argument; and a sender of messages to C++.
    public class CsReceiver : GameBehaviour
    {
        void Ping() => Notes.Note(this, "cs.Ping");
        void Hit(float damage) => Notes.Note(this, "cs.Hit(" + damage.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")");
        void Named(string text) => Notes.Note(this, "cs.Named(" + text + ")");
        void Carry(Payload p) => Notes.Note(this, "cs.Carry(" + p.value + ")");
        void Start()
        {
            // C# to the scripts of both languages, a C# object to C# ones.
            gameObject.SendMessage("Ping");
            BroadcastMessage("Toggle", true, SendMessageOptions.DontRequireReceiver);
            SendMessage("Carry", new Payload { value = 7 });
        }
    }

    // The value types against the reference's documented values; each failed check is noted.
    public class CsValueChecks : GameBehaviour
    {
        void Check(bool ok, string what)
        {
            if (!ok)
                Notes.Note(this, "cs.check.failed:" + what);
        }

        void Start()
        {
            Check(new Vector3(1.234f, 5.678f, 9.012f).ToString() == "(1.23, 5.68, 9.01)", "Vector3.ToString");
            Check(new Vector2(1, 2).ToString() == "(1.00, 2.00)", "Vector2.ToString");
            Check(new Vector3(1, 2, 3).ToString("F1") == "(1.0, 2.0, 3.0)", "Vector3.ToString(F1)");
            Check(Quaternion.identity.ToString() == "(0.00000, 0.00000, 0.00000, 1.00000)", "Quaternion.ToString");
            Check(new Color(1, 0, 1, 1).ToString() == "RGBA(1.000, 0.000, 1.000, 1.000)", "Color.ToString");
            Check(new Color(1, 0, 1).ToString("F5") == "RGBA(1.00000, 0.00000, 1.00000, 1.00000)", "Color.ToString(F5)");
            Check(new Vector2Int(3, -4).ToString() == "(3, -4)" && new Vector2Int(3, 4).sqrMagnitude == 25, "Vector2Int");
            Check(Mathf.Approximately(1f, 1f + 1e-7f) && !Mathf.Approximately(1f, 1.001f), "Mathf.Approximately");
            Check(Mathf.Clamp01(2) == 1 && Mathf.Repeat(370, 360) == 10 && Mathf.DeltaAngle(10, 350) == -20, "Mathf");
            Check(Quaternion.Euler(0, 90, 0) * Vector3.forward == Vector3.right, "Quaternion.Euler");
            Check(Mathf.Abs(Quaternion.Euler(30, 60, 10).eulerAngles.y - 60) < 1e-3f, "Quaternion.eulerAngles");
            Matrix4x4 m = Matrix4x4.TRS(new Vector3(1, 2, 3), Quaternion.identity, Vector3.one);
            Check(m[0, 3] == 1 && m[12] == 1 && m.m13 == 2 && m.GetPosition() == new Vector3(1, 2, 3), "Matrix4x4.TRS");
            Check(m.inverse.MultiplyPoint(new Vector3(1, 2, 3)) == Vector3.zero, "Matrix4x4.inverse");
            Check(new Rect(0, 0, 4, 2).Contains(new Vector2(3.9f, 1.9f)) && !new Rect(0, 0, 4, 2).Contains(new Vector2(4, 1)), "Rect.Contains");
            Check(Color.yellow.r == 1 && Color.clear.a == 0 && Color.HSVToRGB(0, 1, 1) == Color.red, "Color");
            HelixEngine.Random.InitState(42);
            float first = HelixEngine.Random.value;
            HelixEngine.Random.InitState(42);
            Check(HelixEngine.Random.value == first, "HelixEngine.Random.InitState");
            bool inRange = true;
            for (int i = 0; i < 1000; i++)
            {
                int a = HelixEngine.Random.Range(0, 10), b = HelixEngine.Random.Range(10, 0);
                float f = HelixEngine.Random.Range(-1f, 1f);
                inRange &= a >= 0 && a <= 9 && b >= 1 && b <= 10 && f >= -1 && f <= 1 && HelixEngine.Random.value >= 0 && HelixEngine.Random.value <= 1;
            }
            Check(inRange, "HelixEngine.Random.Range");
            // The sequence the engine's generator gives C++ too.
            HelixEngine.Random.InitState(7);
            Notes.Note(this, "cs.random:" + System.BitConverter.SingleToInt32Bits(HelixEngine.Random.value));
            Notes.Note(this, "cs.checks.done");
        }
    }

    // A scenario on the script API, written in both languages (CppScenario): its results as one line.
    public class CsScenario : GameBehaviour
    {
        void Start()
        {
            int rootsBefore = SceneManager.GetActiveScene().rootCount;
            var g = new GameObject("ProbeCs");
            int rootsAfter = SceneManager.GetActiveScene().rootCount;
            Transform t = g.transform;
            t.position = new Vector3(1, 2, 3);
            t.Translate(new Vector3(1, 0, 0));
            var child = new GameObject("Child");
            Transform ct = child.transform;
            ct.SetParent(t, false);
            ct.localPosition = new Vector3(0, 1, 0);
            string childPosition = ct.position.ToString();
            bool found = GameObject.Find("ProbeCs") == g;
            bool same = g.GetComponent<Transform>() == t;
            HelixEngine.Object clone = HelixEngine.Object.Instantiate((HelixEngine.Object)g);
            string cloneName = clone.name;
            GameObject cloneObject = GameObject.Find(cloneName);
            int cloneChildren = cloneObject.transform.childCount;
            HelixEngine.Object.DestroyImmediate(clone);
            bool destroyed = clone == null;
            string position = t.position.ToString();
            Debug.Log("scenario done in C#");
            HelixEngine.Object.DestroyImmediate(g);
            string line = "pos=" + position + ";child=" + childPosition + ";found=" + found + ";same=" + same + ";clone=" + cloneName.Replace("Cs", "") + ":" +
                          cloneChildren + ";destroyed=" + destroyed + ";roots=+" + (rootsAfter - rootsBefore) + ";gone=" + (g == null);
            Notes.Note(this, "scenario:" + line);
        }
    }

    public class Payload
    {
        public int value;
    }
}
