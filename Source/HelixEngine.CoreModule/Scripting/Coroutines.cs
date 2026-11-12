using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Helix.Interop;
using HelixEngine.Internal;

namespace HelixEngine
{
    /// <summary>The base class of the instructions a coroutine yields.</summary>
    public class YieldInstruction
    {
    }

    /// <summary>A started coroutine: yield it to wait for it, pass it to StopCoroutine.</summary>
    public sealed class Coroutine : YieldInstruction
    {
        internal readonly ulong m_Handle;
        internal Coroutine(ulong handle) => m_Handle = handle;
    }

    /// <summary>Waits the given seconds of scaled time; resumes after Update.</summary>
    public sealed class WaitForSeconds : YieldInstruction
    {
        internal readonly float m_Seconds;
        public WaitForSeconds(float seconds) => m_Seconds = seconds;
    }

    /// <summary>Resumes at the end of the next fixed update step.</summary>
    public sealed class WaitForFixedUpdate : YieldInstruction
    {
    }

    /// <summary>Resumes at the end of the frame, after rendering.</summary>
    public sealed class WaitForEndOfFrame : YieldInstruction
    {
    }

    /// <summary>A yield instruction of script code: the coroutine waits while keepWaiting is true (asked once per
    /// frame after Update).</summary>
    public abstract class CustomYieldInstruction : IEnumerator
    {
        public abstract bool keepWaiting { get; }
        public object Current => null;
        public bool MoveNext() => keepWaiting;
        public virtual void Reset() { }
    }

    /// <summary>Waits the given seconds of real time (not scaled).</summary>
    public class WaitForSecondsRealtime : CustomYieldInstruction
    {
        double m_WaitUntilTime = -1;

        public WaitForSecondsRealtime(float time) => waitTime = time;

        public float waitTime { get; set; }

        public override bool keepWaiting
        {
            get
            {
                double now = Time.realtimeSinceStartupAsDouble;
                if (m_WaitUntilTime < 0)
                    m_WaitUntilTime = now + waitTime;
                bool wait = now < m_WaitUntilTime;
                if (!wait)
                    Reset();
                return wait;
            }
        }

        public override void Reset() => m_WaitUntilTime = -1;
    }

    /// <summary>How the timeout of WaitUntil and WaitWhile counts time.</summary>
    public enum WaitTimeoutMode
    {
        Realtime,
        InGameTime,
    }

    /// <summary>Waits until the predicate is true (asked once per frame after Update); with a timeout, onTimeout runs
    /// when the timeout ends the wait first.</summary>
    public sealed class WaitUntil : CustomYieldInstruction
    {
        readonly Func<bool> m_Predicate;
        readonly PredicateTimeout m_Timeout;

        public WaitUntil(Func<bool> predicate) => m_Predicate = predicate;

        public WaitUntil(Func<bool> predicate, TimeSpan timeout, Action onTimeout, WaitTimeoutMode timeoutMode = WaitTimeoutMode.Realtime)
        {
            m_Predicate = predicate;
            m_Timeout = new PredicateTimeout(timeout, onTimeout, timeoutMode);
        }

        public override bool keepWaiting => !m_Predicate() && (m_Timeout == null || m_Timeout.KeepWaiting());
    }

    /// <summary>Waits while the predicate is true; the timeout as WaitUntil's.</summary>
    public sealed class WaitWhile : CustomYieldInstruction
    {
        readonly Func<bool> m_Predicate;
        readonly PredicateTimeout m_Timeout;

        public WaitWhile(Func<bool> predicate) => m_Predicate = predicate;

        public WaitWhile(Func<bool> predicate, TimeSpan timeout, Action onTimeout, WaitTimeoutMode timeoutMode = WaitTimeoutMode.Realtime)
        {
            m_Predicate = predicate;
            m_Timeout = new PredicateTimeout(timeout, onTimeout, timeoutMode);
        }

        public override bool keepWaiting => m_Predicate() && (m_Timeout == null || m_Timeout.KeepWaiting());
    }

    // The timeout of WaitUntil and WaitWhile: from the instruction's creation.
    sealed class PredicateTimeout
    {
        readonly double m_End;
        readonly Action m_OnTimeout;
        readonly WaitTimeoutMode m_Mode;

        public PredicateTimeout(TimeSpan timeout, Action onTimeout, WaitTimeoutMode mode)
        {
            m_Mode = mode;
            m_OnTimeout = onTimeout;
            m_End = Now(mode) + timeout.TotalSeconds;
        }

        static double Now(WaitTimeoutMode mode) => mode == WaitTimeoutMode.Realtime ? Time.realtimeSinceStartupAsDouble : Time.timeAsDouble;

        public bool KeepWaiting()
        {
            if (Now(m_Mode) < m_End)
                return true;
            m_OnTimeout?.Invoke();
            return false;
        }
    }

    public partial class GameBehaviour
    {
        /// <summary>Starts a coroutine: it runs at once up to its first yield. Null when it ended there or the
        /// GameObject is inactive (logged).</summary>
        public Coroutine StartCoroutine(IEnumerator routine) => CoroutineRunner.Start(this, routine ?? throw new NullReferenceException("routine is null"), null);

        /// <summary>Starts the coroutine method of that name (any access, no parameter or one).</summary>
        public Coroutine StartCoroutine(string methodName, object value = null) => CoroutineRunner.StartByName(this, methodName, value);

        /// <summary>Stops the coroutine started with this enumerator.</summary>
        public void StopCoroutine(IEnumerator routine) => CoroutineRunner.Stop(this, routine ?? throw new NullReferenceException("routine is null"));

        public void StopCoroutine(Coroutine routine) => CoroutineRunner.Stop(routine ?? throw new NullReferenceException("routine is null"));

        /// <summary>Stops the coroutines started by this method name.</summary>
        public void StopCoroutine(string methodName) => CoroutineRunner.Stop(this, methodName);

        public void StopAllCoroutines() => CoroutineRunner.StopAll(this);

        /// <summary>Canceled when the behaviour is destroyed.</summary>
        public CancellationToken destroyCancellationToken => CoroutineRunner.DestroyToken(this);
    }
}

namespace HelixEngine.Internal
{
    // What a coroutine yielded (Script/Coroutines.h CoroutineYield).
    [StructLayout(LayoutKind.Sequential)]
    internal struct CoroutineYield
    {
        public uint Wait;
        public uint Reserved;
        public double Seconds;
        public ulong Inner;
    }

    /// <summary>C# coroutines on the engine's scheduler: the enumerators stepped here, their waits there.</summary>
    internal static unsafe class CoroutineRunner
    {
        const uint Ended = 0, NextFrame = 1, Seconds = 2, FixedUpdate = 4, EndOfFrame = 5, Poll = 6, Nested = 7;

        sealed class Routine
        {
            public GameBehaviour Owner;
            public IEnumerator Enumerator;
            public string Method;
            public object Waiting;
            public ulong Handle;
        }

        static delegate* unmanaged[Cdecl]<ulong, uint, ulong, ulong> s_Start;
        static delegate* unmanaged[Cdecl]<ulong, void> s_Stop;
        static delegate* unmanaged[Cdecl]<ulong, void> s_StopAll;
        static uint s_Language = uint.MaxValue;
        static readonly ConditionalWeakTable<GameBehaviour, List<Routine>> s_Routines = new();
        static readonly ConditionalWeakTable<GameBehaviour, CancellationTokenSource> s_Tokens = new();

        public static void Start(Func<string, IntPtr> export)
        {
            var register = (delegate* unmanaged[Cdecl]<void*, void*, void*, uint>)export("HxCoroutines_RegisterLanguage");
            s_Start = (delegate* unmanaged[Cdecl]<ulong, uint, ulong, ulong>)export("HxCoroutines_Start");
            s_Stop = (delegate* unmanaged[Cdecl]<ulong, void>)export("HxCoroutines_Stop");
            s_StopAll = (delegate* unmanaged[Cdecl]<ulong, void>)export("HxCoroutines_StopAll");
            if (register == null)
                return;
            s_Language = register((delegate* unmanaged[Cdecl]<ulong, ulong, CoroutineYield*, void>)&Resume,
                                  (delegate* unmanaged[Cdecl]<ulong, ulong, byte>)&PollWait, (delegate* unmanaged[Cdecl]<ulong, void>)&Release);
        }

        public static Coroutine Start(GameBehaviour owner, IEnumerator enumerator, string method)
        {
            if (s_Start == null || s_Language == uint.MaxValue)
                return null;
            var r = new Routine { Owner = owner, Enumerator = enumerator, Method = method };
            s_Routines.GetOrCreateValue(owner).Add(r);
            ulong state = (ulong)GCHandle.ToIntPtr(GCHandle.Alloc(r));
            ulong handle = s_Start(Objects.IdOf(owner), s_Language, state);
            return handle != 0 ? new Coroutine(handle) : null;
        }

        public static Coroutine StartByName(GameBehaviour owner, string methodName, object value)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (Type t = owner.GetType(); t != null && !Types.IsGenerated(t); t = t.BaseType)
                foreach (MethodInfo m in t.GetMethods(flags | BindingFlags.DeclaredOnly))
                {
                    if (m.Name != methodName || !typeof(IEnumerator).IsAssignableFrom(m.ReturnType) || m.GetParameters().Length > 1)
                        continue;
                    object[] args = m.GetParameters().Length == 1 ? new[] { value } : null;
                    if (m.Invoke(owner, args) is IEnumerator e)
                        return Start(owner, e, methodName);
                }
            ScriptBridge.Error($"Coroutine '{methodName}' couldn't be started!");
            return null;
        }

        public static void Stop(Coroutine routine)
        {
            if (s_Stop != null)
                s_Stop(routine.m_Handle);
        }

        public static void Stop(GameBehaviour owner, IEnumerator enumerator)
        {
            if (s_Routines.TryGetValue(owner, out List<Routine> list))
                foreach (Routine r in list.ToArray())
                    if (ReferenceEquals(r.Enumerator, enumerator) && r.Handle != 0)
                        s_Stop(r.Handle);
        }

        public static void Stop(GameBehaviour owner, string method)
        {
            if (s_Routines.TryGetValue(owner, out List<Routine> list))
                foreach (Routine r in list.ToArray())
                    if (r.Method == method && r.Handle != 0)
                        s_Stop(r.Handle);
        }

        public static void StopAll(GameBehaviour owner)
        {
            if (s_StopAll != null)
                s_StopAll(Objects.IdOf(owner));
        }

        public static CancellationToken DestroyToken(GameBehaviour owner)
        {
            if (!owner)
                return new CancellationToken(true);
            return s_Tokens.GetValue(owner, _ => new CancellationTokenSource()).Token;
        }

        // The behaviour was destroyed (the native side tells its C# object).
        public static void Destroyed(GameBehaviour owner)
        {
            if (s_Tokens.TryGetValue(owner, out CancellationTokenSource cts))
                cts.Cancel();
        }

        // ------------------------------------------------------------------------------------------- the scheduler's calls

        // Steps the enumerator to its next yield and says what it waits for.
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void Resume(ulong state, ulong handle, CoroutineYield* y)
        {
            *y = default;
            var r = (Routine)GCHandle.FromIntPtr((IntPtr)state).Target;
            if (r == null)
                return;
            r.Handle = handle;
            r.Waiting = null;
            try
            {
                if (!r.Enumerator.MoveNext())
                    return;
                Classify(r, r.Enumerator.Current, y);
            }
            catch (Exception e)
            {
                ScriptBridge.ReportCoroutine(r.Owner, e);
                y->Wait = Ended;
            }
        }

        static void Classify(Routine r, object current, CoroutineYield* y)
        {
            switch (current)
            {
                case null: y->Wait = NextFrame; return;
                case WaitForSeconds w:
                    y->Wait = Seconds;
                    y->Seconds = w.m_Seconds;
                    return;
                case WaitForFixedUpdate: y->Wait = FixedUpdate; return;
                case WaitForEndOfFrame: y->Wait = EndOfFrame; return;
                case Coroutine c:
                    y->Wait = Nested;
                    y->Inner = c.m_Handle;
                    return;
                case CustomYieldInstruction ci:
                    // Asked once when yielded (it starts its time there), then once per frame.
                    if (ci.keepWaiting)
                    {
                        r.Waiting = ci;
                        y->Wait = Poll;
                    }
                    else
                        y->Wait = NextFrame;
                    return;
                case AsyncOperation op:
                    if (op.isDone)
                        y->Wait = NextFrame;
                    else
                    {
                        r.Waiting = op;
                        y->Wait = Poll;
                    }
                    return;
                case Awaitable awaitable:
                    if (awaitable.IsCompleted)
                        y->Wait = NextFrame;
                    else
                    {
                        r.Waiting = awaitable;
                        y->Wait = Poll;
                    }
                    return;
                case IEnumerator nested:
                    // A nested coroutine of the same behaviour, waited for.
                    Coroutine inner = Start(r.Owner, nested, null);
                    if (inner == null)
                        y->Wait = NextFrame;
                    else
                    {
                        y->Wait = Nested;
                        y->Inner = inner.m_Handle;
                    }
                    return;
                default: y->Wait = NextFrame; return;
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static byte PollWait(ulong state, ulong handle)
        {
            var r = (Routine)GCHandle.FromIntPtr((IntPtr)state).Target;
            try
            {
                return r?.Waiting switch
                {
                    CustomYieldInstruction ci => ci.keepWaiting ? (byte)1 : (byte)0,
                    AsyncOperation op => op.isDone ? (byte)0 : (byte)1,
                    Awaitable awaitable => awaitable.IsCompleted ? (byte)0 : (byte)1,
                    _ => 0,
                };
            }
            catch (Exception e)
            {
                ScriptBridge.ReportCoroutine(r.Owner, e);
                return 0;
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void Release(ulong state)
        {
            GCHandle h = GCHandle.FromIntPtr((IntPtr)state);
            if (h.Target is Routine r && r.Owner != null && s_Routines.TryGetValue(r.Owner, out List<Routine> list))
                list.Remove(r);
            h.Free();
        }
    }
}
