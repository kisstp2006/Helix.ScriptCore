using System;
using System.Collections.Generic;
using System.Reflection;

namespace HelixEngine.Events
{
    /// <summary>A persistent call's own argument (by its mode).</summary>
    [Serializable]
    class ArgumentCache
    {
        [SerializeField] Object m_ObjectArgument;
        [SerializeField] string m_ObjectArgumentAssemblyTypeName = "";
        [SerializeField] int m_IntArgument;
        [SerializeField] float m_FloatArgument;
        [SerializeField] string m_StringArgument = "";
        [SerializeField] bool m_BoolArgument;

        public Object helixObjectArgument { get => m_ObjectArgument; set => m_ObjectArgument = value; }
        public string helixObjectArgumentAssemblyTypeName => m_ObjectArgumentAssemblyTypeName;
        public int intArgument { get => m_IntArgument; set => m_IntArgument = value; }
        public float floatArgument { get => m_FloatArgument; set => m_FloatArgument = value; }
        public string stringArgument { get => m_StringArgument; set => m_StringArgument = value; }
        public bool boolArgument { get => m_BoolArgument; set => m_BoolArgument = value; }
    }

    /// <summary>A call of a method by name on a target object, kept in scene files.</summary>
    [Serializable]
    class PersistentCall
    {
        [SerializeField] internal Object m_Target;
        [SerializeField] internal string m_TargetAssemblyTypeName = "";
        [SerializeField] internal string m_MethodName = "";
        [SerializeField] internal PersistentListenerMode m_Mode = PersistentListenerMode.Void;
        [SerializeField] internal ArgumentCache m_Arguments = new ArgumentCache();
        [SerializeField] internal HelixEventCallState m_CallState = HelixEventCallState.RuntimeOnly;

        [NonSerialized] MethodInfo m_Method;
        [NonSerialized] bool m_Searched;

        // The method of the target the call names (found once).
        internal MethodInfo Method(HelixEventBase owner)
        {
            if (!m_Searched && m_Target != null && !string.IsNullOrEmpty(m_MethodName))
            {
                m_Searched = true;
                Type t = m_Target.GetType();
                m_Method = m_Mode switch
                {
                    PersistentListenerMode.EventDefined => owner.FindEventDefinedMethod(m_MethodName, t),
                    PersistentListenerMode.Void => HelixEventBase.GetValidMethodInfo(t, m_MethodName, Type.EmptyTypes),
                    PersistentListenerMode.Object => HelixEventBase.GetValidMethodInfo(t, m_MethodName,
                        new[] { m_Arguments.helixObjectArgument != null ? m_Arguments.helixObjectArgument.GetType() : typeof(Object) }),
                    PersistentListenerMode.Int => HelixEventBase.GetValidMethodInfo(t, m_MethodName, new[] { typeof(int) }),
                    PersistentListenerMode.Float => HelixEventBase.GetValidMethodInfo(t, m_MethodName, new[] { typeof(float) }),
                    PersistentListenerMode.String => HelixEventBase.GetValidMethodInfo(t, m_MethodName, new[] { typeof(string) }),
                    PersistentListenerMode.Bool => HelixEventBase.GetValidMethodInfo(t, m_MethodName, new[] { typeof(bool) }),
                    _ => null,
                };
            }
            return m_Method;
        }

        internal void Invoke(HelixEventBase owner, object[] eventArguments)
        {
            if (m_CallState == HelixEventCallState.Off || m_Target == null)
                return;
            MethodInfo m = Method(owner);
            if (m == null)
            {
                Debug.LogWarning($"HelixEvent: {m_Target.name} has no method {m_MethodName} that takes the call's arguments.");
                return;
            }
            object[] args = m_Mode switch
            {
                PersistentListenerMode.EventDefined => eventArguments,
                PersistentListenerMode.Object => new object[] { m_Arguments.helixObjectArgument },
                PersistentListenerMode.Int => new object[] { m_Arguments.intArgument },
                PersistentListenerMode.Float => new object[] { m_Arguments.floatArgument },
                PersistentListenerMode.String => new object[] { m_Arguments.stringArgument },
                PersistentListenerMode.Bool => new object[] { m_Arguments.boolArgument },
                _ => Array.Empty<object>(),
            };
            m.Invoke(m_Target, args);
        }
    }

    [Serializable]
    class PersistentCallGroup
    {
        [SerializeField] internal List<PersistentCall> m_Calls = new List<PersistentCall>();
    }

    /// <summary>The base of the serialized events: their persistent calls (kept in scene files) and the listeners
    /// scripts add at run time. An event of a native component (the canvas UI's controls) keeps its persistent calls
    /// on the native side; its listeners are here.</summary>
    [Serializable]
    public abstract class HelixEventBase : ISerializationCallbackReceiver
    {
        [SerializeField] PersistentCallGroup m_PersistentCalls = new PersistentCallGroup();

        // A native component's event: its owner and index (HelixEventNative), and its raise.
        [NonSerialized] internal Object m_NativeOwner;
        [NonSerialized] internal int m_NativeIndex = -1;
        [NonSerialized] internal Action<object[]> m_NativeRaise;

        internal bool IsNative => m_NativeIndex >= 0;

        public int GetPersistentEventCount() =>
            IsNative ? HelixEventNative.PersistentCount(m_NativeOwner, m_NativeIndex) : m_PersistentCalls.m_Calls.Count;

        public Object GetPersistentTarget(int index)
        {
            if (IsNative)
                return HelixEventNative.PersistentTarget(m_NativeOwner, m_NativeIndex, index);
            return index >= 0 && index < m_PersistentCalls.m_Calls.Count ? m_PersistentCalls.m_Calls[index].m_Target : null;
        }

        public string GetPersistentMethodName(int index)
        {
            if (IsNative)
                return HelixEventNative.PersistentMethodName(m_NativeOwner, m_NativeIndex, index);
            return index >= 0 && index < m_PersistentCalls.m_Calls.Count ? m_PersistentCalls.m_Calls[index].m_MethodName : string.Empty;
        }

        public void SetPersistentListenerState(int index, HelixEventCallState state)
        {
            if (IsNative)
                HelixEventNative.SetPersistentState(m_NativeOwner, m_NativeIndex, index, state);
            else if (index >= 0 && index < m_PersistentCalls.m_Calls.Count)
                m_PersistentCalls.m_Calls[index].m_CallState = state;
        }

        public HelixEventCallState GetPersistentListenerState(int index)
        {
            if (IsNative)
                return HelixEventNative.GetPersistentState(m_NativeOwner, m_NativeIndex, index);
            return index >= 0 && index < m_PersistentCalls.m_Calls.Count ? m_PersistentCalls.m_Calls[index].m_CallState : HelixEventCallState.Off;
        }

        /// <summary>Removes the listeners scripts added (not the persistent calls).</summary>
        public abstract void RemoveAllListeners();

        /// <summary>A public or not instance method of the type (or its bases) with exactly these parameter types.</summary>
        public static MethodInfo GetValidMethodInfo(object obj, string functionName, Type[] argumentTypes) =>
            obj == null ? null : GetValidMethodInfo(obj.GetType(), functionName, argumentTypes);

        public static MethodInfo GetValidMethodInfo(Type objectType, string functionName, Type[] argumentTypes)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (Type t = objectType; t != null && t != typeof(object); t = t.BaseType)
            {
                MethodInfo m = t.GetMethod(functionName, flags, null, argumentTypes, null);
                if (m == null)
                    continue;
                // The parameters must be the types asked for (primitive ones exactly).
                ParameterInfo[] ps = m.GetParameters();
                bool ok = true;
                for (int i = 0; i < ps.Length && ok; i++)
                    ok = argumentTypes[i].IsPrimitive ? ps[i].ParameterType == argumentTypes[i] : ps[i].ParameterType.IsAssignableFrom(argumentTypes[i]);
                if (ok)
                    return m;
            }
            return null;
        }

        /// <summary>The event's own method of a target for persistent calls of mode EventDefined.</summary>
        protected abstract MethodInfo FindMethod_Impl(string name, Type targetObjType);

        internal MethodInfo FindEventDefinedMethod(string name, Type t) => FindMethod_Impl(name, t);

        // The persistent calls (of a C# event), then the listeners.
        internal void InvokeCalls(object[] eventArguments)
        {
            if (!IsNative)
                foreach (PersistentCall c in m_PersistentCalls.m_Calls)
                    c.Invoke(this, eventArguments);
        }

        // A native event raised on the native side (persistent calls and C++ listeners), which tells C#'s listeners
        // back through the component.
        internal void RaiseNative(object[] args)
        {
            if (m_NativeRaise != null)
            {
                m_NativeRaise(args);
                return;
            }
            if (args.Length == 0)
                HelixEventNative.Raise(m_NativeOwner, m_NativeIndex);
            else if (args[0] is bool b)
                HelixEventNative.RaiseBool(m_NativeOwner, m_NativeIndex, b);
            else if (args[0] is int n)
                HelixEventNative.RaiseInt(m_NativeOwner, m_NativeIndex, n);
            else if (args[0] is float f)
                HelixEventNative.RaiseFloat(m_NativeOwner, m_NativeIndex, f);
            else if (args[0] is string s)
                HelixEventNative.RaiseString(m_NativeOwner, m_NativeIndex, s);
            else if (args[0] is Vector2 v)
                HelixEventNative.RaiseVector2(m_NativeOwner, m_NativeIndex, v);
            else
                HelixEventNative.RaiseObject(m_NativeOwner, m_NativeIndex, args[0] as Object);
        }

        public override string ToString() => base.ToString() + " " + GetType().FullName;

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize() { }
    }

    /// <summary>A serialized event without arguments.</summary>
    [Serializable]
    public class HelixEvent : HelixEventBase
    {
        [NonSerialized] readonly List<HelixAction> m_Listeners = new List<HelixAction>();

        public HelixEvent() { }

        public void AddListener(HelixAction call)
        {
            if (call != null)
                m_Listeners.Add(call);
        }

        public void RemoveListener(HelixAction call) => m_Listeners.Remove(call);

        public override void RemoveAllListeners() => m_Listeners.Clear();

        /// <summary>The persistent calls, then the listeners.</summary>
        public void Invoke()
        {
            if (IsNative)
            {
                RaiseNative(Array.Empty<object>());
                return;
            }
            InvokeCalls(Array.Empty<object>());
            InvokeListeners();
        }

        internal void InvokeListeners()
        {
            foreach (HelixAction a in m_Listeners.ToArray())
                a();
        }

        protected override MethodInfo FindMethod_Impl(string name, Type targetObjType) => GetValidMethodInfo(targetObjType, name, Type.EmptyTypes);
    }

    [Serializable]
    public class HelixEvent<T0> : HelixEventBase
    {
        [NonSerialized] readonly List<HelixAction<T0>> m_Listeners = new List<HelixAction<T0>>();

        public HelixEvent() { }

        public void AddListener(HelixAction<T0> call)
        {
            if (call != null)
                m_Listeners.Add(call);
        }

        public void RemoveListener(HelixAction<T0> call) => m_Listeners.Remove(call);

        public override void RemoveAllListeners() => m_Listeners.Clear();

        public void Invoke(T0 arg0)
        {
            if (IsNative)
            {
                RaiseNative(new object[] { arg0 });
                return;
            }
            InvokeCalls(new object[] { arg0 });
            InvokeListeners(arg0);
        }

        internal void InvokeListeners(T0 arg0)
        {
            foreach (HelixAction<T0> a in m_Listeners.ToArray())
                a(arg0);
        }

        protected override MethodInfo FindMethod_Impl(string name, Type targetObjType) => GetValidMethodInfo(targetObjType, name, new[] { typeof(T0) });
    }

    [Serializable]
    public class HelixEvent<T0, T1> : HelixEventBase
    {
        [NonSerialized] readonly List<HelixAction<T0, T1>> m_Listeners = new List<HelixAction<T0, T1>>();

        public HelixEvent() { }

        public void AddListener(HelixAction<T0, T1> call)
        {
            if (call != null)
                m_Listeners.Add(call);
        }

        public void RemoveListener(HelixAction<T0, T1> call) => m_Listeners.Remove(call);

        public override void RemoveAllListeners() => m_Listeners.Clear();

        public void Invoke(T0 arg0, T1 arg1)
        {
            if (IsNative)
            {
                RaiseNative(new object[] { arg0, arg1 });
                return;
            }
            InvokeCalls(new object[] { arg0, arg1 });
            InvokeListeners(arg0, arg1);
        }

        internal void InvokeListeners(T0 arg0, T1 arg1)
        {
            foreach (HelixAction<T0, T1> a in m_Listeners.ToArray())
                a(arg0, arg1);
        }

        protected override MethodInfo FindMethod_Impl(string name, Type targetObjType) =>
            GetValidMethodInfo(targetObjType, name, new[] { typeof(T0), typeof(T1) });
    }

    [Serializable]
    public class HelixEvent<T0, T1, T2> : HelixEventBase
    {
        [NonSerialized] readonly List<HelixAction<T0, T1, T2>> m_Listeners = new List<HelixAction<T0, T1, T2>>();

        public HelixEvent() { }

        public void AddListener(HelixAction<T0, T1, T2> call)
        {
            if (call != null)
                m_Listeners.Add(call);
        }

        public void RemoveListener(HelixAction<T0, T1, T2> call) => m_Listeners.Remove(call);

        public override void RemoveAllListeners() => m_Listeners.Clear();

        public void Invoke(T0 arg0, T1 arg1, T2 arg2)
        {
            if (IsNative)
            {
                RaiseNative(new object[] { arg0, arg1, arg2 });
                return;
            }
            InvokeCalls(new object[] { arg0, arg1, arg2 });
            InvokeListeners(arg0, arg1, arg2);
        }

        internal void InvokeListeners(T0 arg0, T1 arg1, T2 arg2)
        {
            foreach (HelixAction<T0, T1, T2> a in m_Listeners.ToArray())
                a(arg0, arg1, arg2);
        }

        protected override MethodInfo FindMethod_Impl(string name, Type targetObjType) =>
            GetValidMethodInfo(targetObjType, name, new[] { typeof(T0), typeof(T1), typeof(T2) });
    }

    [Serializable]
    public class HelixEvent<T0, T1, T2, T3> : HelixEventBase
    {
        [NonSerialized] readonly List<HelixAction<T0, T1, T2, T3>> m_Listeners = new List<HelixAction<T0, T1, T2, T3>>();

        public HelixEvent() { }

        public void AddListener(HelixAction<T0, T1, T2, T3> call)
        {
            if (call != null)
                m_Listeners.Add(call);
        }

        public void RemoveListener(HelixAction<T0, T1, T2, T3> call) => m_Listeners.Remove(call);

        public override void RemoveAllListeners() => m_Listeners.Clear();

        public void Invoke(T0 arg0, T1 arg1, T2 arg2, T3 arg3)
        {
            if (IsNative)
            {
                RaiseNative(new object[] { arg0, arg1, arg2, arg3 });
                return;
            }
            InvokeCalls(new object[] { arg0, arg1, arg2, arg3 });
            InvokeListeners(arg0, arg1, arg2, arg3);
        }

        internal void InvokeListeners(T0 arg0, T1 arg1, T2 arg2, T3 arg3)
        {
            foreach (HelixAction<T0, T1, T2, T3> a in m_Listeners.ToArray())
                a(arg0, arg1, arg2, arg3);
        }

        protected override MethodInfo FindMethod_Impl(string name, Type targetObjType) =>
            GetValidMethodInfo(targetObjType, name, new[] { typeof(T0), typeof(T1), typeof(T2), typeof(T3) });
    }
}

namespace HelixEngine.Internal
{
    using HelixEngine.Events;

    /// <summary>For the bindings of native components: a HelixEvent tied to a native component's event (its
    /// persistent calls there, raised there), and the listeners told when the native side raises it.</summary>
    public static class NativeEvents
    {
        public static T Bind<T>(T e, Object owner, int index, Action<object[]> raise = null) where T : HelixEventBase
        {
            e.m_NativeOwner = owner;
            e.m_NativeIndex = index;
            e.m_NativeRaise = raise;
            return e;
        }

        public static void Tell(HelixEvent e) => e.InvokeListeners();
        public static void Tell<T0>(HelixEvent<T0> e, T0 a) => e.InvokeListeners(a);
        public static void Tell<T0, T1>(HelixEvent<T0, T1> e, T0 a, T1 b) => e.InvokeListeners(a, b);
        public static void Tell<T0, T1, T2>(HelixEvent<T0, T1, T2> e, T0 a, T1 b, T2 c) => e.InvokeListeners(a, b, c);
    }
}
