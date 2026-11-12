using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Helix.Interop;
using Helix.Scripting;

namespace HelixEngine
{
    /// <summary>Whether SendMessage logs an error when no script has the method.</summary>
    public enum SendMessageOptions
    {
        RequireReceiver = 0,
        DontRequireReceiver = 1,
    }

    public partial class GameObject
    {
        public void SendMessage(string methodName) => Internal.Messages.Send(this, methodName, null, SendMessageOptions.RequireReceiver, 0);
        public void SendMessage(string methodName, object value) => Internal.Messages.Send(this, methodName, value, SendMessageOptions.RequireReceiver, 0);
        public void SendMessage(string methodName, object value, SendMessageOptions options) => Internal.Messages.Send(this, methodName, value, options, 0);
        public void SendMessage(string methodName, SendMessageOptions options) => Internal.Messages.Send(this, methodName, null, options, 0);

        public void BroadcastMessage(string methodName) => Internal.Messages.Send(this, methodName, null, SendMessageOptions.RequireReceiver, 1);
        public void BroadcastMessage(string methodName, object parameter) => Internal.Messages.Send(this, methodName, parameter, SendMessageOptions.RequireReceiver, 1);
        public void BroadcastMessage(string methodName, object parameter, SendMessageOptions options) => Internal.Messages.Send(this, methodName, parameter, options, 1);
        public void BroadcastMessage(string methodName, SendMessageOptions options) => Internal.Messages.Send(this, methodName, null, options, 1);

        public void SendMessageUpwards(string methodName) => Internal.Messages.Send(this, methodName, null, SendMessageOptions.RequireReceiver, 2);
        public void SendMessageUpwards(string methodName, object value) => Internal.Messages.Send(this, methodName, value, SendMessageOptions.RequireReceiver, 2);
        public void SendMessageUpwards(string methodName, object value, SendMessageOptions options) => Internal.Messages.Send(this, methodName, value, options, 2);
        public void SendMessageUpwards(string methodName, SendMessageOptions options) => Internal.Messages.Send(this, methodName, null, options, 2);
    }

    public partial class Component
    {
        public void SendMessage(string methodName) => gameObject.SendMessage(methodName);
        public void SendMessage(string methodName, object value) => gameObject.SendMessage(methodName, value);
        public void SendMessage(string methodName, object value, SendMessageOptions options) => gameObject.SendMessage(methodName, value, options);
        public void SendMessage(string methodName, SendMessageOptions options) => gameObject.SendMessage(methodName, options);

        public void BroadcastMessage(string methodName) => gameObject.BroadcastMessage(methodName);
        public void BroadcastMessage(string methodName, object parameter) => gameObject.BroadcastMessage(methodName, parameter);
        public void BroadcastMessage(string methodName, object parameter, SendMessageOptions options) => gameObject.BroadcastMessage(methodName, parameter, options);
        public void BroadcastMessage(string methodName, SendMessageOptions options) => gameObject.BroadcastMessage(methodName, options);

        public void SendMessageUpwards(string methodName) => gameObject.SendMessageUpwards(methodName);
        public void SendMessageUpwards(string methodName, object value) => gameObject.SendMessageUpwards(methodName, value);
        public void SendMessageUpwards(string methodName, object value, SendMessageOptions options) => gameObject.SendMessageUpwards(methodName, value, options);
        public void SendMessageUpwards(string methodName, SendMessageOptions options) => gameObject.SendMessageUpwards(methodName, options);
    }
}

namespace HelixEngine.Internal
{
    /// <summary>A SendMessage value between the languages (the same layout in ScriptRuntime.cpp).</summary>
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct MessageValue
    {
        public uint kind;
        public uint type;
        public long integer;
        public double real;
        public byte* text;
        public int textSize;
        public ulong obj;
    }

    /// <summary>SendMessage over both languages: C# sends through the engine, which calls C++ scripts itself and C# ones back here.</summary>
    static unsafe class Messages
    {
        static delegate* unmanaged[Cdecl]<ulong, byte*, int, MessageValue*, uint, uint, byte> s_Send;
        static readonly Dictionary<(Type, string), MethodInfo[]> s_Methods = new();

        public static void Send(GameObject g, string name, object value, SendMessageOptions options, uint reach)
        {
            if (g is null)
                throw new NullReferenceException("SendMessage on null");
            if (s_Send == null)
                s_Send = (delegate* unmanaged[Cdecl]<ulong, byte*, int, MessageValue*, uint, uint, byte>)Host.FindExport("HxEngineScripts_SendMessage");
            byte[] nameBytes = Encoding.UTF8.GetBytes(name ?? "");
            byte[] textBytes = null;
            var v = new MessageValue();
            GCHandle passed = default;
            switch (value)
            {
                case null:
                    break;
                case bool b:
                    v.kind = 1;
                    v.integer = b ? 1 : 0;
                    break;
                case sbyte or byte or short or ushort or int or uint or long:
                    v.kind = 2;
                    v.integer = Convert.ToInt64(value);
                    break;
                case float or double:
                    v.kind = 3;
                    v.real = Convert.ToDouble(value);
                    break;
                case string s:
                    v.kind = 4;
                    textBytes = Encoding.UTF8.GetBytes(s);
                    v.textSize = textBytes.Length;
                    break;
                case Object o when Objects.IdOf(o) != 0:
                    InteropObject io = Objects.Pass(o);
                    v.kind = 5;
                    v.obj = io.id;
                    v.type = io.type;
                    break;
                default:
                    // Any other C# object: to C# scripts only, as it is.
                    passed = GCHandle.Alloc(value);
                    v.kind = 6;
                    v.obj = (ulong)GCHandle.ToIntPtr(passed);
                    break;
            }
            try
            {
                fixed (byte* n = nameBytes, t = textBytes)
                {
                    v.text = t;
                    s_Send(g.m_HxId, n, nameBytes.Length, &v, (uint)options, reach);
                }
            }
            finally
            {
                if (passed.IsAllocated)
                    passed.Free();
            }
        }

        static object ValueOf(MessageValue* v) => v == null ? null : v->kind switch
        {
            1 => v->integer != 0,
            2 => v->integer,
            3 => v->real,
            4 => Native.ReadString(v->text, v->textSize),
            5 => Objects.Wrap<Object>(v->obj, v->type),
            6 => GCHandle.FromIntPtr((IntPtr)v->obj).Target,
            _ => null,
        };

        static MethodInfo[] MethodsOf(Type type, string name)
        {
            lock (s_Methods)
            {
                if (s_Methods.TryGetValue((type, name), out MethodInfo[] known))
                    return known;
                var list = new List<MethodInfo>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                for (Type c = type; c != null && !Types.IsGenerated(c); c = c.BaseType)
                    foreach (MethodInfo m in c.GetMethods(flags))
                        if (m.Name == name && m.GetParameters().Length <= 1 && !m.IsGenericMethodDefinition)
                            list.Add(m);
                return s_Methods[(type, name)] = list.ToArray();
            }
        }

        // An argument for the parameter: the value itself, a number converted, or none.
        static bool Fits(Type parameter, object value, out object argument)
        {
            argument = value;
            if (value == null)
                return !parameter.IsValueType;
            if (parameter.IsInstanceOfType(value))
                return true;
            if (value is long or double && (parameter.IsPrimitive && parameter != typeof(bool) && parameter != typeof(char)))
            {
                try
                {
                    argument = Convert.ChangeType(value, parameter, System.Globalization.CultureInfo.InvariantCulture);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            return false;
        }

        // The engine calls this for each C# script of the GameObjects reached: 1 when a method was called.
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static byte Receive(ulong handle, byte* name, int size, MessageValue* value)
        {
            object o = GCHandle.FromIntPtr((IntPtr)handle).Target;
            if (o == null)
                return 0;
            string method = Encoding.UTF8.GetString(name, size);
            object arg = ValueOf(value);
            MethodInfo chosen = null;
            object[] args = null;
            foreach (MethodInfo m in MethodsOf(o.GetType(), method))
            {
                ParameterInfo[] ps = m.GetParameters();
                if (ps.Length == 1 && value != null && value->kind != 0 && Fits(ps[0].ParameterType, arg, out object a))
                {
                    chosen = m;
                    args = new[] { a };
                    break;
                }
                if (ps.Length == 0 && chosen == null)
                    chosen = m;
            }
            if (chosen == null)
                return 0;
            try
            {
                chosen.Invoke(o, args);
            }
            catch (TargetInvocationException e)
            {
                Native.ReportException(e.InnerException ?? e);
            }
            catch (Exception e)
            {
                Native.ReportException(e);
            }
            return 1;
        }
    }
}
