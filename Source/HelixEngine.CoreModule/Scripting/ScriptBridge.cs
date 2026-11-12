using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Helix.Interop;
using Helix.Interop.Generated;
using Helix.Scripting;

namespace HelixEngine.Internal
{
    /// <summary>
    /// The C# side of scripts: it starts the engine's C# side (the bindings of the engine module, the functions the
    /// native script runtime calls), registers the GameBehaviour classes of script assemblies with their messages, makes
    /// the C# object of a script when its native GameBehaviour is made, and calls the scripts of a phase in one loop,
    /// each call in its own try.
    /// </summary>
    public static unsafe class ScriptBridge
    {
        // The engine's messages, in its order (Script/ScriptClass.h).
        static readonly string[] s_Messages =
        {
            "Awake", "OnEnable", "Start", "FixedUpdate", "Update", "LateUpdate", "OnDisable", "OnDestroy", "OnApplicationFocus", "OnApplicationPause",
            "OnApplicationQuit", "OnTransformParentChanged", "OnTransformChildrenChanged", "OnBecameVisible", "OnBecameInvisible", "OnValidate", "Reset",
        };
        const int FocusMessage = 8, PauseMessage = 9;

        sealed class ScriptType
        {
            public Type Type;
            public uint Index;
            public Action<object>[] Calls = new Action<object>[s_Messages.Length];
            public Action<object, bool>[] BoolCalls = new Action<object, bool>[s_Messages.Length];
            // Its serialized fields (its C# bases' too), null without any.
            public Action<object, IntPtr> Fields;
        }

        static readonly Dictionary<uint, ScriptType> s_ByIndex = new();
        // By the engine's script index (the batches name each script's class).
        static ScriptType[] s_Indexed = new ScriptType[64];
        static readonly Dictionary<Type, ScriptType> s_ByType = new();
        static delegate* unmanaged[Cdecl]<int, byte*, int, void> s_Log;
        static delegate* unmanaged[Cdecl]<byte*, int, byte*, int, byte*, int, uint, uint, int, byte, uint*, uint> s_RegisterClass;
        static bool s_Started;

        static IntPtr Export(string name) => (IntPtr)Host.FindExport(name);

        /// <summary>Starts the engine's C# side: 0 when it runs.</summary>
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static int Start()
        {
            try
            {
                if (s_Started)
                    return 0;
                HxModule_HelixEngine.Load(Export);
                s_Log = (delegate* unmanaged[Cdecl]<int, byte*, int, void>)Export("HxEngine_Log");
                s_RegisterClass = (delegate* unmanaged[Cdecl]<byte*, int, byte*, int, byte*, int, uint, uint, int, byte, uint*, uint>)Export("HxEngineScripts_RegisterClass");
                SerializedFields.Start(Export);
                Native.MakeException = (status, what) => status switch
                {
                    Native.MissingObject => new MissingReferenceException($"{what}: the object was destroyed; check it against null before using it"),
                    Native.NullArgument => new ArgumentNullException(what),
                    _ => new InvalidOperationException($"{what}: the call failed (status {status})"),
                };
                Native.ReportException = e => Log(4, e.ToString());
                var setManaged = (delegate* unmanaged[Cdecl]<void**, uint, byte>)Export("HxEngineScripts_SetManaged");
                if (setManaged == null || s_RegisterClass == null)
                    return 2;
                void** table = (void**)NativeMemory.Alloc((nuint)(5 * sizeof(void*)));
                table[0] = (void*)(delegate* unmanaged[Cdecl]<ulong, uint, ulong*, int>)&Create;
                table[1] = (void*)(delegate* unmanaged[Cdecl]<uint, ulong*, uint*, byte*, int, void>)&RunBatch;
                table[2] = (void*)(delegate* unmanaged[Cdecl]<ulong, uint, byte, void>)&RunOne;
                table[3] = (void*)(delegate* unmanaged[Cdecl]<ulong, byte*, int, MessageValue*, byte>)&Messages.Receive;
                table[4] = (void*)(delegate* unmanaged[Cdecl]<ulong, void*, void>)&TransferFields;
                if (setManaged(table, 5) == 0)
                    return 3;
                s_Started = true;
                return 0;
            }
            catch (Exception e)
            {
                Host.Report(e);
                return 1;
            }
        }

        /// <summary>Loads a script assembly into the engine's load context and registers its script classes; how many.</summary>
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static int LoadScripts(byte* path, int size)
        {
            try
            {
                string file = System.IO.Path.GetFullPath(Encoding.UTF8.GetString(path, size));
                Assembly assembly = Host.Engine.LoadFromAssemblyPath(file);
                return RegisterAssembly(assembly);
            }
            catch (Exception e)
            {
                Host.Report(e);
                return -1;
            }
        }

        /// <summary>The script classes of an assembly (GameBehaviour classes that are not abstract nor generic), bases first.</summary>
        public static int RegisterAssembly(Assembly assembly)
        {
            var types = new List<Type>();
            foreach (Type t in assembly.GetTypes())
                if (t.IsClass && !t.IsGenericTypeDefinition && typeof(GameBehaviour).IsAssignableFrom(t) && !Types.IsGenerated(t))
                    types.Add(t);
            types.Sort((a, b) => Depth(a).CompareTo(Depth(b)));
            int count = 0;
            foreach (Type t in types)
                if (Register(t) != null)
                    count++;
            return count;
        }

        static int Depth(Type t)
        {
            int d = 0;
            for (Type c = t; c != null; c = c.BaseType)
                d++;
            return d;
        }

        static ScriptType Register(Type t)
        {
            if (s_ByType.TryGetValue(t, out ScriptType known))
                return known;
            var st = new ScriptType { Type = t };
            uint mask = 0;
            for (int m = 0; m < s_Messages.Length; m++)
            {
                bool takesBool = m == FocusMessage || m == PauseMessage;
                MethodInfo method = FindMessage(t, s_Messages[m], takesBool);
                if (method == null)
                    continue;
                mask |= 1u << m;
                if (takesBool)
                    st.BoolCalls[m] = (Action<object, bool>)s_MakeBool.MakeGenericMethod(method.DeclaringType).Invoke(null, new object[] { method });
                else
                    st.Calls[m] = (Action<object>)s_Make.MakeGenericMethod(method.DeclaringType).Invoke(null, new object[] { method });
            }
            int order = t.GetCustomAttribute<DefaultExecutionOrder>()?.order ?? 0;
            st.Fields = SerializedFields.ForScript(t);
            uint baseType = Types.OfObject(t.BaseType);
            uint typeId;
            byte[] asm = Encoding.UTF8.GetBytes(t.Assembly.GetName().Name ?? "");
            byte[] ns = Encoding.UTF8.GetBytes(t.Namespace ?? "");
            byte[] name = Encoding.UTF8.GetBytes(t.Name);
            uint index;
            fixed (byte* a = asm, n = ns, c = name)
                index = s_RegisterClass(a, asm.Length, n, ns.Length, c, name.Length, baseType, mask, order, (byte)(st.Fields != null ? 1 : 0), &typeId);
            if (index == uint.MaxValue)
            {
                Log(4, $"{t.FullName}: its script class could not be registered");
                return null;
            }
            st.Index = index;
            Types.Register(t, typeId, generated: false);
            s_ByIndex[index] = st;
            if (index >= s_Indexed.Length)
                Array.Resize(ref s_Indexed, (int)Math.Max(index + 1, (uint)s_Indexed.Length * 2));
            s_Indexed[index] = st;
            s_ByType[t] = st;
            return st;
        }

        // A message: the nearest declaration in the script class or its C# bases (any access, empty ones too).
        static MethodInfo FindMessage(Type t, string name, bool takesBool)
        {
            Type[] parameters = takesBool ? new[] { typeof(bool) } : Type.EmptyTypes;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (Type c = t; c != null && !Types.IsGenerated(c); c = c.BaseType)
            {
                MethodInfo m = c.GetMethod(name, flags, null, parameters, null);
                if (m != null && m.ReturnType == typeof(void) && !m.IsGenericMethodDefinition)
                    return m;
            }
            return null;
        }

        static readonly MethodInfo s_Make = typeof(ScriptBridge).GetMethod(nameof(Make), BindingFlags.Static | BindingFlags.NonPublic);
        static readonly MethodInfo s_MakeBool = typeof(ScriptBridge).GetMethod(nameof(MakeBool), BindingFlags.Static | BindingFlags.NonPublic);

        // One delegate per message: the cast and the call in one compiled body.
        static Action<object> Make<T>(MethodInfo m)
        {
            var o = System.Linq.Expressions.Expression.Parameter(typeof(object), "o");
            var call = System.Linq.Expressions.Expression.Call(System.Linq.Expressions.Expression.Convert(o, typeof(T)), m);
            return System.Linq.Expressions.Expression.Lambda<Action<object>>(call, o).Compile();
        }

        static Action<object, bool> MakeBool<T>(MethodInfo m)
        {
            var o = System.Linq.Expressions.Expression.Parameter(typeof(object), "o");
            var v = System.Linq.Expressions.Expression.Parameter(typeof(bool), "v");
            var call = System.Linq.Expressions.Expression.Call(System.Linq.Expressions.Expression.Convert(o, typeof(T)), m, v);
            return System.Linq.Expressions.Expression.Lambda<Action<object, bool>>(call, o, v).Compile();
        }

        internal static void Warn(string text) => Log(3, text);

        // A C# script's serialized fields through a native visitor.
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void TransferFields(ulong handle, void* visitor)
        {
            try
            {
                object o = handle != 0 ? GCHandle.FromIntPtr((IntPtr)handle).Target : null;
                if (o != null && s_ByType.TryGetValue(o.GetType(), out ScriptType st) && st.Fields != null)
                    SerializedFields.Transfer(o, st.Fields, (IntPtr)visitor);
            }
            catch (Exception e)
            {
                Host.Report(e);
            }
        }

        static void Log(int level, string text)
        {
            if (s_Log == null)
            {
                Host.Log((LogLevel)level, text);
                return;
            }
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            fixed (byte* p = bytes)
                s_Log(level, p, bytes.Length);
        }

        // An exception of a script's message, with the script and its GameObject.
        static void Report(object script, int message, Exception e)
        {
            string where = script.GetType().FullName;
            try
            {
                if (script is Component c && c.gameObject is GameObject g)
                    where += " on '" + g.name + "'";
            }
            catch (Exception)
            {
            }
            Log(4, $"{e}\n  in {s_Messages[message]} of {where}");
        }

        // ------------------------------------------------------------------------------------------- the native side calls

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static int Create(ulong objectId, uint scriptIndex, ulong* handle)
        {
            try
            {
                *handle = 0;
                if (!s_ByIndex.TryGetValue(scriptIndex, out ScriptType st))
                    return 1;
                // The object knows its native object before anything of it runs (its constructor runs first, as in
                // the reference; the engine is not to be used there).
                var o = (GameBehaviour)RuntimeHelpers.GetUninitializedObject(st.Type);
                o.m_HxId = objectId;
                ConstructorInfo ctor = st.Type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.EmptyTypes);
                ctor?.Invoke(o, null);
                Objects.Remember(o, objectId);
                *handle = (ulong)GCHandle.ToIntPtr(GCHandle.Alloc(o));
                return 0;
            }
            catch (Exception e)
            {
                Host.Report(e is TargetInvocationException t && t.InnerException != null ? t.InnerException : e);
                return 3;
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void RunBatch(uint message, ulong* handles, uint* scripts, byte* live, int count)
        {
            ScriptType[] indexed = s_Indexed;
            for (int i = 0; i < count; i++)
            {
                if (live[i] == 0)
                    continue;
                object o = GCHandle.FromIntPtr((IntPtr)handles[i]).Target;
                uint index = scripts[i];
                ScriptType st = index < (uint)indexed.Length ? indexed[index] : null;
                if (o == null || st == null)
                    continue;
                Action<object> call = st.Calls[message];
                if (call == null)
                    continue;
                try
                {
                    call(o);
                }
                catch (Exception e)
                {
                    Report(o, (int)message, e);
                }
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void RunOne(ulong handle, uint message, byte argument)
        {
            object o = GCHandle.FromIntPtr((IntPtr)handle).Target;
            if (o == null || !s_ByType.TryGetValue(o.GetType(), out ScriptType st))
                return;
            try
            {
                if (st.BoolCalls[message] is Action<object, bool> b)
                    b(o, argument != 0);
                else
                    st.Calls[message]?.Invoke(o);
            }
            catch (Exception e)
            {
                Report(o, (int)message, e);
            }
        }
    }
}
