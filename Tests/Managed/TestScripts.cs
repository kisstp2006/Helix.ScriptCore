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

        public static void NoteRaw(string message)
        {
            if (s_Note == null)
                s_Note = (delegate* unmanaged[Cdecl]<byte*, int, void>)Host.FindExport("ScriptCoreTests.Note");
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            fixed (byte* p = bytes)
                s_Note(p, bytes.Length);
        }

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

namespace Tests
{
    public enum CsPace
    {
        Slow,
        Fast,
    }

    [Serializable]
    public class CsWaypoint
    {
        public Vector3 position;
        public int wait;
        public System.Collections.Generic.List<int> marks = new System.Collections.Generic.List<int>();
    }

    [Serializable]
    public struct CsPair
    {
        public int a;
        public string b;
    }

    [Serializable]
    public unsafe struct CsCorners
    {
        public fixed int corners[3];
    }

    // A Serializable class holding itself: the depth limit stops it.
    [Serializable]
    public class CsNode
    {
        public int value;
        public CsNode next;
    }

    // Serialized fields of the kinds C# scripts have, and members that are not serialized.
    public class CsFields : GameBehaviour
    {
        public int count = 3;
        public float speed = 1.5f;
        public bool on = true;
        public CsPace pace = CsPace.Fast;
        public string title = "fields";
        public char letter = 'a';
        public long big;
        public double precise;
        public int[] numbers;
        public System.Collections.Generic.List<string> words;
        public Vector3 offset;
        public Color tint = Color.white;
        public CsWaypoint home;
        public System.Collections.Generic.List<CsWaypoint> route;
        public CsPair pair;
        public CsPair[] pairs;
        public GameObject target;
        public Transform[] points;
        public System.Collections.Generic.List<Vector3> path;
        public CsNode chain;
        public CsCorners corner;
        [SerializeField] int secret = 7;
        [NonSerialized] public int scratch;
        public readonly int fixedValue = 1;
        public static int shared;
        public int Property { get; set; }
        [field: SerializeField] public int Backed { get; set; }
        public System.Collections.Generic.Dictionary<int, int> map;
        public int[,] grid;

        static string Join<T>(System.Collections.Generic.IEnumerable<T> items, Func<T, string> text)
        {
            if (items == null)
                return "null";
            var parts = new System.Collections.Generic.List<string>();
            foreach (T item in items)
                parts.Add(text(item));
            return string.Join("|", parts);
        }

        static string V(Vector3 v) => FormattableString.Invariant($"{v.x}/{v.y}/{v.z}");

        static string Name(HelixEngine.Object o) => o == null ? "none" : o is Component c ? c.gameObject.name + "." + o.GetType().Name : o.name;

        unsafe string Corners()
        {
            CsCorners c = corner;
            return c.corners[0] + "/" + c.corners[1] + "/" + c.corners[2];
        }

        void Report()
        {
            int depth = 0;
            for (CsNode n = chain; n != null; n = n.next)
                depth++;
            string line = FormattableString.Invariant(
                $"{count},{speed},{on},{pace},{title},{letter},{big},{precise},{Join(numbers, x => x.ToString())},{Join(words, x => x)},{V(offset)},{tint.g}") +
                $";home={(home == null ? "null" : V(home.position) + "/" + home.wait + "/" + Join(home.marks, x => x.ToString()))}" +
                $";route={Join(route, w => V(w.position) + "/" + w.wait)};pair={pair.a}/{pair.b};pairs={Join(pairs, p => p.a + "/" + p.b)}" +
                $";target={Name(target)};points={Join(points, p => Name(p))};path={Join(path, V)};chain={depth};corners={Corners()}" +
                $";secret={secret};scratch={scratch};backed={Backed};property={Property}";
            Notes.Note(this, line);
        }

        void Touch()
        {
            scratch = 99;
            Property = 5;
        }
    }

    // A derived script: its base's fields first.
    public class CsMoreFields : CsFields
    {
        public int extra = 1;

        void ReportExtra() => Notes.Note(this, "extra=" + extra);
    }
}

namespace Tests
{
    public interface IAnimal
    {
        string Describe();
    }

    [Serializable]
    public class CsDog : IAnimal
    {
        public string name = "dog";
        [SerializeReference] public IAnimal friend;
        public string Describe() => "dog:" + name;
    }

    [Serializable]
    public class CsCat : IAnimal
    {
        public int lives = 9;
        public string Describe() => "cat:" + lives;
    }

    // A Serializable class held inline: its reference belongs to the host.
    [Serializable]
    public class CsShelter
    {
        [SerializeReference] public IAnimal resident;
    }

    // SerializeReference fields: polymorphism, null, one object in several fields, a cycle.
    public class CsRefs : GameBehaviour
    {
        [SerializeReference] public IAnimal pet;
        [SerializeReference] public System.Collections.Generic.List<IAnimal> zoo;
        [SerializeReference] public object any;
        [SerializeReference] public IAnimal none;
        public CsShelter shelter;

        static object s_LastPet;

        void Build()
        {
            var dog = new CsDog { name = "Rex" };
            dog.friend = dog;
            var cat = new CsCat { lives = 7 };
            pet = dog;
            zoo = new System.Collections.Generic.List<IAnimal> { dog, cat, null };
            any = new CsCat { lives = 3 };
            none = null;
            shelter = new CsShelter { resident = cat };
        }

        void Report()
        {
            string zooText = zoo == null ? "null" : string.Join("|", zoo.ConvertAll(a => a == null ? "null" : a.Describe()));
            bool shared = zoo != null && zoo.Count > 1 && ReferenceEquals(pet, zoo[0]) && ReferenceEquals(shelter?.resident, zoo[1]);
            bool cycle = pet is CsDog d && ReferenceEquals(d.friend, d);
            string line = $"pet={pet?.Describe()};zoo={zooText};any={(any as IAnimal)?.Describe()};none={(none == null ? "null" : "set")};shared={shared};cycle={cycle};" +
                          $"same={ReferenceEquals(pet, s_LastPet)}";
            s_LastPet = pet;
            Notes.Note(this, line);
        }
    }
}

namespace Tests
{
    [Serializable]
    public class CsHook : ISerializationCallbackReceiver
    {
        public string tag = "inline";
        public void OnBeforeSerialize() => CsCallbacks.Log.Add("before:" + tag);
        public void OnAfterDeserialize() => CsCallbacks.Log.Add("after:" + tag);
    }

    // ISerializationCallbackReceiver on the script, on a Serializable class inline and on a managed reference.
    public class CsCallbacks : GameBehaviour, ISerializationCallbackReceiver
    {
        public static readonly System.Collections.Generic.List<string> Log = new System.Collections.Generic.List<string>();
        public int value = 1;
        public CsHook inline = new CsHook();
        [SerializeReference] public object referenced = new CsHook { tag = "ref" };
        [NonSerialized] public int derived;

        public void OnBeforeSerialize() => Log.Add("before:host");

        public void OnAfterDeserialize()
        {
            derived = value * 10;
            Log.Add("after:host");
        }

        void Report()
        {
            Notes.Note(this, string.Join(",", Log) + ";derived=" + derived);
            Log.Clear();
        }
    }
}

namespace Tests
{
    // C# coroutines: every yield instruction, nested coroutines, the stop rules (the C++ Routines' twin).
    public class CsRoutines : GameBehaviour
    {
        public bool ready;
        public bool trace = true;
        Coroutine m_Running;
        static CsRoutines s_Remembered;

        void Start()
        {
            if (trace)
                m_Running = StartCoroutine(Trace());
        }
        void FixedUpdate() => Notes.Note(this, "FixedUpdate");
        void Update() => Notes.Note(this, "Update");
        void LateUpdate() => Notes.Note(this, "LateUpdate");

        System.Collections.IEnumerator Trace()
        {
            Notes.Note(this, "co.start");
            yield return null;
            Notes.Note(this, "co.frame");
            yield return new WaitForFixedUpdate();
            Notes.Note(this, "co.fixed");
            yield return new WaitForEndOfFrame();
            Notes.Note(this, "co.end");
            yield return new WaitUntil(() => ready);
            Notes.Note(this, "co.ready");
            yield return Inner();
            Notes.Note(this, "co.done");
        }

        System.Collections.IEnumerator Inner()
        {
            Notes.Note(this, "inner.start");
            yield return null;
            Notes.Note(this, "inner.end");
        }

        System.Collections.IEnumerator Forever()
        {
            for (;;)
            {
                Notes.Note(this, "tick");
                yield return null;
            }
        }

        System.Collections.IEnumerator Seconds(float s)
        {
            yield return new WaitForSeconds(s);
            Notes.Note(this, "seconds");
            yield return new WaitForSecondsRealtime(s);
            Notes.Note(this, "realtime");
        }

        System.Collections.IEnumerator Timeout()
        {
            yield return new WaitUntil(() => false, TimeSpan.FromSeconds(0.1), () => Notes.Note(this, "timeout"), WaitTimeoutMode.InGameTime);
            Notes.Note(this, "after.timeout");
            yield return new WaitWhile(() => !ready);
            Notes.Note(this, "after.while");
        }

        System.Collections.IEnumerator Ends()
        {
            yield break;
        }

        System.Collections.IEnumerator Throws()
        {
            Notes.Note(this, "throws");
            yield return null;
            throw new InvalidOperationException("coroutine failure");
        }

        // Entry points for the test program (SendMessage).
        void NoTrace() => trace = false;
        void Ready() => ready = true;
        void StartForever() => m_Running = StartCoroutine(Forever());
        void StartForeverByName() => StartCoroutine("Forever");
        void StopForeverByName() => StopCoroutine("Forever");
        void StopRunning() => StopCoroutine(m_Running);
        void StopAll() => StopAllCoroutines();
        void StartSeconds() => StartCoroutine(Seconds(0.2f));
        void StartTimeout() => StartCoroutine(Timeout());
        void StartEnds() => Notes.Note(this, StartCoroutine(Ends()) == null ? "ends.null" : "ends.set");
        void StartThrows() => StartCoroutine(Throws());
        void Remember() => s_Remembered = this;
        void StartOnRemembered() => Notes.Note(this, s_Remembered.StartCoroutine(Forever()) == null ? "remembered.null" : "remembered.set");
        void WatchDestroy() => destroyCancellationToken.Register(() => Notes.NoteRaw("destroyed.token"));
    }
}

namespace Tests
{
    // Awaitables: every wait, thread switches, results, completion sources, cancellation, Tasks, order.
    public class CsAwaits : GameBehaviour
    {
        static int s_Main;
        readonly AwaitableCompletionSource m_Source = new AwaitableCompletionSource();

        void Awake() => s_Main = Environment.CurrentManagedThreadId;

        async void StartSequence()
        {
            await Sequence();
            Notes.Note(this, "seq.done");
        }

        async Awaitable Sequence()
        {
            Notes.Note(this, "a.start");
            await Awaitable.NextFrameAsync();
            Notes.Note(this, "a.frame");
            await Awaitable.FixedUpdateAsync();
            Notes.Note(this, "a.fixed");
            await Awaitable.EndOfFrameAsync();
            Notes.Note(this, "a.end");
            int v = await Compute();
            Notes.Note(this, "a.result=" + v);
            await Awaitable.BackgroundThreadAsync();
            bool background = Environment.CurrentManagedThreadId != s_Main;
            await Awaitable.MainThreadAsync();
            Notes.Note(this, "a.main bg=" + background + " main=" + (Environment.CurrentManagedThreadId == s_Main));
            await m_Source.Awaitable;
            Notes.Note(this, "a.source");
        }

        async Awaitable<int> Compute()
        {
            await Awaitable.NextFrameAsync();
            return 7;
        }

        void Complete() => m_Source.SetResult();

        async void StartCancel()
        {
            Awaitable a = Awaitable.WaitForSecondsAsync(10);
            a.Cancel();
            try
            {
                await a;
                Notes.Note(this, "not canceled");
            }
            catch (OperationCanceledException)
            {
                Notes.Note(this, "canceled");
            }
            var cts = new System.Threading.CancellationTokenSource();
            Awaitable b = Awaitable.NextFrameAsync(cts.Token);
            cts.Cancel();
            try
            {
                await b;
            }
            catch (OperationCanceledException)
            {
                Notes.Note(this, "token");
            }
            var source = new AwaitableCompletionSource<string>();
            source.SetResult("value");
            Notes.Note(this, "source=" + await source.Awaitable + " again=" + source.TrySetResult("x"));
        }

        async void StartTask()
        {
            await System.Threading.Tasks.Task.Yield();
            Notes.Note(this, "task");
        }

        void StartCoroutineAwait() => StartCoroutine(CoAwait());

        System.Collections.IEnumerator CoAwait()
        {
            yield return Awaitable.WaitForSecondsAsync(0.1f);
            Notes.Note(this, "co.awaited");
        }

        void StartOrder()
        {
            _ = Order("x");
            _ = Order("y");
        }

        async Awaitable Order(string name)
        {
            await Awaitable.NextFrameAsync();
            Notes.Note(this, "order." + name);
        }
    }
}
