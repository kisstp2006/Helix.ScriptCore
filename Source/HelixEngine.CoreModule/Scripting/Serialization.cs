using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Helix.Interop;

namespace HelixEngine.Internal
{
    // The C ABI of fields known at run time (Helix.Serialization's Transfer/Dynamic.h): the native side turns every
    // call into its visitor's own field, so the fields of C# scripts reach every visitor of the engine.
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct DynamicVisitor
    {
        public void* Self;
        public VisitorTable* Table;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct VisitorTable
    {
        public delegate* unmanaged[Cdecl]<void*, byte*, uint, uint, void*, void> Value;
        public delegate* unmanaged[Cdecl]<void*, byte*, uint, void*, DynamicString*, void> Text;
        public delegate* unmanaged[Cdecl]<void*, byte*, uint, ulong*, void> Reference;
        public delegate* unmanaged[Cdecl]<void*, byte*, uint, void*, DynamicStruct*, void> Structure;
        public delegate* unmanaged[Cdecl]<void*, byte*, uint, void*, DynamicArray*, void> Array;
        public delegate* unmanaged[Cdecl]<void*, int> Version;
        public delegate* unmanaged[Cdecl]<void*, ulong*, void> ObjectReference;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct DynamicString
    {
        public delegate* unmanaged[Cdecl]<void*, DynamicString*, byte**, uint*, void> Get;
        public delegate* unmanaged[Cdecl]<void*, DynamicString*, byte*, uint, void> Set;
        public void* User;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct DynamicReference
    {
        public delegate* unmanaged[Cdecl]<void*, DynamicReference*, ulong> Get;
        public delegate* unmanaged[Cdecl]<void*, DynamicReference*, ulong, void> Set;
        public void* User;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct DynamicStruct
    {
        public byte* TypeName;
        public uint TypeNameSize;
        // 1: flow (YAML on one line); 2: opaque (its own type tree and bytes in the binary form).
        public uint Flags;
        public int Version;
        public delegate* unmanaged[Cdecl]<void*, DynamicVisitor*, DynamicStruct*, void> Transfer;
        public void* User;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct DynamicArray
    {
        public uint ItemKind;
        public DynamicStruct* ItemStruct;
        public DynamicString* ItemString;
        public DynamicReference* ItemReference;
        public delegate* unmanaged[Cdecl]<void*, DynamicArray*, uint> Size;
        public delegate* unmanaged[Cdecl]<void*, uint, DynamicArray*, void> Resize;
        public delegate* unmanaged[Cdecl]<void*, uint, DynamicArray*, void*> Item;
        public delegate* unmanaged[Cdecl]<DynamicArray*, void*> Prototype;
        public void* User;
    }

    // ValueKind of Helix.Serialization, and the item kind of object references.
    internal enum FieldKind : uint
    {
        Bool,
        Int8,
        UInt8,
        Int16,
        UInt16,
        Int32,
        UInt32,
        Int64,
        UInt64,
        Float,
        Double,
        String,
        Guid,
        Array,
        FixedArray,
        Struct,
        Reference = 255,
    }

    // The items of an array while the native side visits it: copies in native memory, resized by the native side.
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct ItemBuffer
    {
        public byte* Data;
        public uint Count;
        public uint Capacity;
        public uint ItemSize;
        public int Items;
    }

    // A string while the native side visits it: its UTF-8 text, and the string it was set to (an index of the results).
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct StringSlot
    {
        public byte* Data;
        public uint Size;
        public int Result;
    }

    /// <summary>
    /// The serialized fields of C# scripts and of the Serializable classes and structs they hold: found by reflection
    /// once per class (public fields and those with SerializeField that are not static, const, readonly or
    /// NonSerialized, of the supported types), driven through compiled delegates (no reflection per object).
    /// </summary>
    internal static unsafe partial class SerializedFields
    {
        // Serializable classes and structs nest inline up to this depth.
        public const int DepthLimit = 10;

        static delegate* unmanaged[Cdecl]<byte*, int, uint, DynamicStruct*> s_ValueType;
        static delegate* unmanaged[Cdecl]<ulong, uint> s_ObjectType;
        static delegate* unmanaged[Cdecl]<ulong, uint, byte> s_ObjectIsA;
        static DynamicString* s_StringType;
        static DynamicReference* s_ReferenceType;
        static byte* s_Zeros;

        // Objects of Serializable types the native side holds by slot during a call, and the strings it set.
        static readonly List<object> s_Slots = new();
        static readonly List<string> s_Results = new();
        static readonly List<GCHandle> s_Pins = new();
        static int s_Depth;

        static readonly Dictionary<Type, Action<object, IntPtr>> s_Scripts = new();
        static readonly Dictionary<Type, bool> s_ScriptReferences = new();
        static readonly Dictionary<Type, SerializedType> s_Types = new();
        static readonly List<SerializedType> s_TypeList = new();
        static readonly Dictionary<Type, ItemOps> s_Items = new();
        static readonly List<ItemOps> s_ItemList = new();
        static readonly Dictionary<Type, IntPtr> s_ValueTypes = new();
        static readonly Dictionary<string, IntPtr> s_Names = new();
        static readonly HashSet<Type> s_DepthWarned = new();

        public static void Start(Func<string, IntPtr> export)
        {
            s_ValueType = (delegate* unmanaged[Cdecl]<byte*, int, uint, DynamicStruct*>)export("HxEngineScripts_ValueType");
            s_ObjectType = (delegate* unmanaged[Cdecl]<ulong, uint>)export("HxEngineScripts_ObjectType");
            s_ObjectIsA = (delegate* unmanaged[Cdecl]<ulong, uint, byte>)export("HxEngineScripts_ObjectIsA");
            s_StringType = (DynamicString*)NativeMemory.AllocZeroed((nuint)sizeof(DynamicString));
            s_StringType->Get = &StringGet;
            s_StringType->Set = &StringSet;
            s_ReferenceType = (DynamicReference*)NativeMemory.AllocZeroed((nuint)sizeof(DynamicReference));
            s_ReferenceType->Get = &ReferenceGet;
            s_ReferenceType->Set = &ReferenceSet;
            s_Zeros = (byte*)NativeMemory.AllocZeroed(256);
            StartReferences();
        }

        // ------------------------------------------------------------------------------------------- scripts

        /// <summary>The serialized fields of a script class (its C# bases' first), null when it has none.</summary>
        public static Action<object, IntPtr> ForScript(Type script)
        {
            if (s_Scripts.TryGetValue(script, out Action<object, IntPtr> known))
                return known;
            var chain = new List<Type>();
            for (Type c = script; c != null && !Types.IsGenerated(c); c = c.BaseType)
                chain.Insert(0, c);
            var fields = new List<FieldInfo>();
            foreach (Type c in chain)
                fields.AddRange(Declared(c));
            Action<object, IntPtr> transfer = Compile(script, x => x, fields, out bool references);
            s_Scripts[script] = transfer;
            s_ScriptReferences[script] = references;
            return transfer;
        }

        /// <summary>A script's fields through a native visitor (the native side calls it for its C# object), then its
        /// managed references when its class has SerializeReference fields.</summary>
        public static void Transfer(object script, Action<object, IntPtr> fields, IntPtr visitor)
        {
            int slots = s_Slots.Count, results = s_Results.Count, pins = s_Pins.Count;
            List<object> saved = s_Read;
            bool reading = Reading(visitor);
            s_Read = reading ? new List<object>() : null;
            try
            {
                if ((V(visitor)->Flags & 4) != 0)
                    BeforeSerialize(script);
                if (s_ScriptReferences.TryGetValue(script.GetType(), out bool references) && references)
                    TransferWithReferences(script, fields, visitor);
                else
                    fields(script, visitor);
                if (reading)
                {
                    // The host's first, then the objects it holds in the order they were read.
                    AfterDeserialize(script);
                    foreach (object o in s_Read)
                        AfterDeserialize(o);
                }
            }
            finally
            {
                s_Read = saved;
                Release(slots, results, pins);
            }
        }

        // The objects of Serializable types read during a host's read (their OnAfterDeserialize after the host's).
        static List<object> s_Read;

        static void BeforeSerialize(object o)
        {
            try
            {
                if (o is ISerializationCallbackReceiver r)
                    r.OnBeforeSerialize();
                else if (o is IStrongBox box && box.Value is ISerializationCallbackReceiver boxed)
                {
                    boxed.OnBeforeSerialize();
                    box.Value = boxed;
                }
            }
            catch (Exception e)
            {
                Native.ReportException(e);
            }
        }

        static void AfterDeserialize(object o)
        {
            try
            {
                if (o is ISerializationCallbackReceiver r)
                    r.OnAfterDeserialize();
                else if (o is IStrongBox box && box.Value is ISerializationCallbackReceiver boxed)
                {
                    boxed.OnAfterDeserialize();
                    box.Value = boxed;
                }
            }
            catch (Exception e)
            {
                Native.ReportException(e);
            }
        }

        // A class's own serialized fields in declaration order.
        static List<FieldInfo> Declared(Type t)
        {
            var list = new List<FieldInfo>();
            foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                if (IsSerialized(f))
                    list.Add(f);
            list.Sort((a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
            return list;
        }

        // NonSerialized and Serializable are metadata flags; .NET marks their names obsolete with its old formatters.
#pragma warning disable SYSLIB0050
        const FieldAttributes NotSerialized = FieldAttributes.NotSerialized;
        const TypeAttributes Serializable = TypeAttributes.Serializable;
#pragma warning restore SYSLIB0050

        static bool IsSerialized(FieldInfo f) =>
            !f.IsStatic && !f.IsInitOnly && !f.IsLiteral && (f.Attributes & NotSerialized) == 0 && (f.IsPublic || f.IsDefined(typeof(SerializeField), true));

        // The fields of `type` (the object or its box) through the helpers below; null without any. `references`: a
        // SerializeReference field among them or in a Serializable type they hold inline.
        static Action<object, IntPtr> Compile(Type type, Func<Expression, Expression> target, List<FieldInfo> fields, out bool references)
        {
            ParameterExpression o = Expression.Parameter(typeof(object), "o");
            ParameterExpression v = Expression.Parameter(typeof(IntPtr), "v");
            Expression instance = target(Expression.Convert(o, type));
            var calls = new List<Expression>();
            references = false;
            foreach (FieldInfo f in fields)
            {
                Call c = f.IsDefined(typeof(SerializeReference), true) ? ReferenceCall(f.FieldType)
                         : f.GetCustomAttribute<FixedBufferAttribute>() is FixedBufferAttribute fixedBuffer ? FixedBuffer(f.FieldType, fixedBuffer)
                                                                                                           : Classify(f.FieldType);
                if (c.Method == null)
                    continue;
                (IntPtr name, uint size) = Name(f.Name);
                var args = new List<Expression> { v, Expression.Constant(name), Expression.Constant(size) };
                if (c.Extra != null)
                    args.Add(Expression.Constant(c.Extra, c.ExtraType));
                if (c.Reference)
                {
                    // Set once the references are read: the field's setter and its owner.
                    ParameterExpression owner = Expression.Parameter(typeof(object), "owner");
                    ParameterExpression value = Expression.Parameter(typeof(object), "value");
                    Expression assign = Expression.Assign(Expression.Field(target(Expression.Convert(owner, type)), f), Expression.Convert(value, f.FieldType));
                    args.Add(Expression.Constant(Expression.Lambda<Action<object, object>>(assign, owner, value).Compile()));
                    args.Add(o);
                    references = true;
                }
                references |= c.Holds?.HasReferences == true;
                args.Add(Expression.Field(instance, f));
                calls.Add(Expression.Call(c.Method, args));
            }
            if (calls.Count == 0)
                return null;
            return Expression.Lambda<Action<object, IntPtr>>(Expression.Block(calls), o, v).Compile();
        }

        static (IntPtr, uint) Name(string name)
        {
            if (!s_Names.TryGetValue(name, out IntPtr p))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(name);
                p = (IntPtr)NativeMemory.Alloc((nuint)Math.Max(bytes.Length, 1));
                bytes.AsSpan().CopyTo(new Span<byte>((void*)p, bytes.Length));
                s_Names[name] = p;
            }
            return (p, (uint)Encoding.UTF8.GetByteCount(name));
        }

        // ------------------------------------------------------------------------------------------- the field types

        struct Call
        {
            public MethodInfo Method;
            public object Extra;
            public Type ExtraType;
            // A SerializeReference field (its helper takes the field's setter and owner).
            public bool Reference;
            // The Serializable type it holds inline.
            public SerializedType Holds;
        }

        static readonly Dictionary<Type, FieldKind> s_Numbers = new()
        {
            [typeof(bool)] = FieldKind.Bool, [typeof(sbyte)] = FieldKind.Int8, [typeof(byte)] = FieldKind.UInt8, [typeof(short)] = FieldKind.Int16,
            [typeof(ushort)] = FieldKind.UInt16, [typeof(char)] = FieldKind.UInt16, [typeof(int)] = FieldKind.Int32, [typeof(uint)] = FieldKind.UInt32,
            [typeof(long)] = FieldKind.Int64, [typeof(ulong)] = FieldKind.UInt64, [typeof(float)] = FieldKind.Float, [typeof(double)] = FieldKind.Double,
        };

        static MethodInfo Helper(string name) => typeof(SerializedFields).GetMethod(name, BindingFlags.Static | BindingFlags.Public);

        // The helper a field of type `t` goes through; no method for types that are not serialized.
        static Call Classify(Type t)
        {
            if (s_Numbers.TryGetValue(t, out FieldKind kind))
                return new Call { Method = Helper(nameof(Number)).MakeGenericMethod(t), Extra = (uint)kind, ExtraType = typeof(uint) };
            if (t.IsEnum)
            {
                Type raw = Enum.GetUnderlyingType(t);
                if (raw == typeof(long) || raw == typeof(ulong) || !s_Numbers.TryGetValue(raw, out kind))
                    return default;
                return new Call { Method = Helper(nameof(EnumValue)).MakeGenericMethod(t, raw), Extra = (uint)kind, ExtraType = typeof(uint) };
            }
            if (t == typeof(string))
                return new Call { Method = Helper(nameof(Text)) };
            if (typeof(Object).IsAssignableFrom(t))
                return new Call { Method = Helper(nameof(Reference)).MakeGenericMethod(t) };
            IntPtr value = NativeValue(t);
            if (value != IntPtr.Zero)
                return new Call { Method = Helper(nameof(NativeValue)).MakeGenericMethod(t), Extra = value, ExtraType = typeof(IntPtr) };
            if (t.IsArray && t.GetArrayRank() == 1 && ItemsOf(t.GetElementType()) is ItemOps arrayItems)
                return new Call
                {
                    Method = Helper(nameof(Sequence)).MakeGenericMethod(t.GetElementType()),
                    Extra = arrayItems,
                    ExtraType = arrayItems.GetType(),
                    Holds = arrayItems.Holds,
                };
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>) && ItemsOf(t.GetGenericArguments()[0]) is ItemOps listItems)
                return new Call
                {
                    Method = Helper(nameof(List)).MakeGenericMethod(t.GetGenericArguments()[0]),
                    Extra = listItems,
                    ExtraType = listItems.GetType(),
                    Holds = listItems.Holds,
                };
            if (TypeOf(t) is SerializedType st)
                return new Call
                {
                    Method = Helper(t.IsValueType ? nameof(ManagedStruct) : nameof(ManagedClass)).MakeGenericMethod(t),
                    Extra = st,
                    ExtraType = typeof(SerializedType),
                    Holds = st,
                };
            return default;
        }

        // A fixed size buffer (of an unsafe struct): an array that keeps its length.
        static Call FixedBuffer(Type buffer, FixedBufferAttribute fixedBuffer)
        {
            if (ItemsOf(fixedBuffer.ElementType) is not ItemOps items)
                return default;
            return new Call
            {
                Method = Helper(nameof(Fixed)).MakeGenericMethod(buffer, fixedBuffer.ElementType),
                Extra = new FixedItems(items, fixedBuffer.Length),
                ExtraType = typeof(FixedItems),
            };
        }

        // A Helix.Math value type: its native descriptor (the same layout), else zero.
        static IntPtr NativeValue(Type t)
        {
            if (!t.IsValueType || t.IsPrimitive || t.IsEnum || t.Assembly != typeof(Object).Assembly || s_ValueType == null)
                return IntPtr.Zero;
            if (s_ValueTypes.TryGetValue(t, out IntPtr known))
                return known;
            byte[] name = Encoding.UTF8.GetBytes(t.FullName ?? t.Name);
            IntPtr found;
            fixed (byte* p = name)
                found = (IntPtr)s_ValueType(p, name.Length, (uint)Marshal.SizeOf(t));
            s_ValueTypes[t] = found;
            return found;
        }

        // A Serializable class or struct of script code: inline by value.
        static SerializedType TypeOf(Type t)
        {
            if (s_Types.TryGetValue(t, out SerializedType known))
                return known;
            bool serializable = (t.Attributes & Serializable) != 0 && !t.IsAbstract && !t.IsGenericTypeDefinition && !t.IsPrimitive && !t.IsEnum && !t.IsArray &&
                                !t.IsPointer && !typeof(Delegate).IsAssignableFrom(t) && t.Namespace?.StartsWith("System", StringComparison.Ordinal) != true &&
                                (t.IsValueType || t.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.EmptyTypes) != null);
            if (!serializable)
            {
                s_Types[t] = null;
                return null;
            }
            var st = new SerializedType { Type = t, Index = s_TypeList.Count };
            s_Types[t] = st;
            s_TypeList.Add(st);
            byte[] name = Encoding.UTF8.GetBytes(t.Name);
            st.Desc = (DynamicStruct*)NativeMemory.AllocZeroed((nuint)sizeof(DynamicStruct));
            st.Desc->TypeName = (byte*)NativeMemory.Alloc((nuint)Math.Max(name.Length, 1));
            name.AsSpan().CopyTo(new Span<byte>(st.Desc->TypeName, name.Length));
            st.Desc->TypeNameSize = (uint)name.Length;
            st.Desc->Version = 1;
            st.Desc->Transfer = &StructTransfer;
            st.Desc->User = (void*)st.Index;
            // The same as a managed reference's data: opaque (its layout is the entry's type's).
            st.DataDesc = (DynamicStruct*)NativeMemory.Alloc((nuint)sizeof(DynamicStruct));
            *st.DataDesc = *st.Desc;
            st.DataDesc->Flags |= 2;
            // Its fields (and those of its bases), the box for structs.
            var chain = new List<Type>();
            for (Type c = t; c != null && c != typeof(object) && c != typeof(ValueType); c = c.BaseType)
                chain.Insert(0, c);
            var fields = new List<FieldInfo>();
            foreach (Type c in chain)
                fields.AddRange(Declared(c));
            bool references;
            if (t.IsValueType)
            {
                Type box = typeof(StrongBox<>).MakeGenericType(t);
                st.New = () => Activator.CreateInstance(box);
                st.Transfer = Compile(box, x => Expression.Field(x, box.GetField("Value")), fields, out references);
            }
            else
            {
                st.New = () => Activator.CreateInstance(t, true);
                st.Transfer = Compile(t, x => x, fields, out references);
            }
            st.HasReferences = references;
            return st;
        }

        static ItemOps ItemsOf(Type t)
        {
            if (s_Items.TryGetValue(t, out ItemOps known))
                return known;
            ItemOps ops = null;
            if (s_Numbers.TryGetValue(t, out FieldKind kind))
                ops = MakeItems(typeof(UnmanagedItems<>), t, (uint)kind, IntPtr.Zero);
            else if (t.IsEnum)
            {
                Type raw = Enum.GetUnderlyingType(t);
                if (raw != typeof(long) && raw != typeof(ulong) && s_Numbers.TryGetValue(raw, out kind))
                    ops = MakeItems(typeof(UnmanagedItems<>), t, (uint)kind, IntPtr.Zero);
            }
            else if (t == typeof(string))
                ops = new StringItems();
            else if (typeof(Object).IsAssignableFrom(t))
                ops = (ItemOps)Activator.CreateInstance(typeof(ReferenceItems<>).MakeGenericType(t));
            else if (NativeValue(t) is IntPtr value && value != IntPtr.Zero)
                ops = MakeItems(typeof(UnmanagedItems<>), t, (uint)FieldKind.Struct, value);
            else if (!t.IsArray && !(t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)) && TypeOf(t) is SerializedType st)
                ops = (ItemOps)Activator.CreateInstance(typeof(ManagedItems<>).MakeGenericType(t), st);
            s_Items[t] = ops;
            if (ops != null)
                ops.Register(s_ItemList);
            return ops;
        }

        static ItemOps MakeItems(Type generic, Type item, uint kind, IntPtr itemStruct) =>
            (ItemOps)Activator.CreateInstance(generic.MakeGenericType(item), kind, itemStruct);

        // ------------------------------------------------------------------------------------------- the helpers

        static DynamicVisitor* V(IntPtr v) => (DynamicVisitor*)v;
        static bool Reading(IntPtr v) => (V(v)->Flags & 1) != 0;

        public static void Number<T>(IntPtr v, IntPtr name, uint size, uint kind, ref T field) where T : unmanaged
        {
            T value = field;
            V(v)->Table->Value(V(v)->Self, (byte*)name, size, kind, &value);
            field = value;
        }

        public static void EnumValue<T, TRaw>(IntPtr v, IntPtr name, uint size, uint kind, ref T field) where T : unmanaged where TRaw : unmanaged =>
            Number(v, name, size, kind, ref Unsafe.As<T, TRaw>(ref field));

        public static void Text(IntPtr v, IntPtr name, uint size, ref string field)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(field ?? "");
            StringSlot slot;
            fixed (byte* p = bytes)
            {
                slot.Data = p;
                slot.Size = (uint)bytes.Length;
                slot.Result = -1;
                V(v)->Table->Text(V(v)->Self, (byte*)name, size, &slot, s_StringType);
            }
            if (slot.Result >= 0)
                field = s_Results[slot.Result];
            else
                field ??= "";
        }

        public static void Reference<T>(IntPtr v, IntPtr name, uint size, ref T field) where T : class
        {
            ulong id = Objects.IdOf(field), before = id;
            V(v)->Table->Reference(V(v)->Self, (byte*)name, size, &id);
            if (id != before)
                field = Resolve<T>(id);
        }

        public static void NativeValue<T>(IntPtr v, IntPtr name, uint size, IntPtr type, ref T field) where T : unmanaged
        {
            T value = field;
            V(v)->Table->Structure(V(v)->Self, (byte*)name, size, &value, (DynamicStruct*)type);
            field = value;
        }

        public static void ManagedClass<T>(IntPtr v, IntPtr name, uint size, SerializedType type, ref T field) where T : class
        {
            // A null field is an object with default fields (as the reference does).
            field ??= (T)type.New();
            int slot = Push(field);
            V(v)->Table->Structure(V(v)->Self, (byte*)name, size, &slot, type.Desc);
        }

        public static void ManagedStruct<T>(IntPtr v, IntPtr name, uint size, SerializedType type, ref T field) where T : struct
        {
            var box = new StrongBox<T>(field);
            int slot = Push(box);
            V(v)->Table->Structure(V(v)->Self, (byte*)name, size, &slot, type.Desc);
            field = box.Value;
        }

        public static void Sequence<T>(IntPtr v, IntPtr name, uint size, ItemOps<T> items, ref T[] field)
        {
            int count = field?.Length ?? 0;
            ItemBuffer b = items.Begin(count);
            for (int i = 0; i < count; i++)
                items.Store(b.Data + (long)i * b.ItemSize, field[i]);
            try
            {
                V(v)->Table->Array(V(v)->Self, (byte*)name, size, &b, items.Desc);
                if (Reading(v) || items.WritesBack || field == null)
                {
                    var result = new T[b.Count];
                    for (int i = 0; i < result.Length; i++)
                        result[i] = items.Load(b.Data + (long)i * b.ItemSize, field != null && i < field.Length ? field[i] : default);
                    field = result;
                }
            }
            finally
            {
                NativeMemory.Free(b.Data);
            }
        }

        public static void List<T>(IntPtr v, IntPtr name, uint size, ItemOps<T> items, ref List<T> field)
        {
            int count = field?.Count ?? 0;
            ItemBuffer b = items.Begin(count);
            for (int i = 0; i < count; i++)
                items.Store(b.Data + (long)i * b.ItemSize, field[i]);
            try
            {
                V(v)->Table->Array(V(v)->Self, (byte*)name, size, &b, items.Desc);
                if (Reading(v) || items.WritesBack || field == null)
                {
                    var result = new List<T>((int)b.Count);
                    for (int i = 0; i < (int)b.Count; i++)
                        result.Add(items.Load(b.Data + (long)i * b.ItemSize, field != null && i < field.Count ? field[i] : default));
                    if (field == null)
                        field = result;
                    else
                    {
                        field.Clear();
                        field.AddRange(result);
                    }
                }
            }
            finally
            {
                NativeMemory.Free(b.Data);
            }
        }

        public static void Fixed<TBuffer, T>(IntPtr v, IntPtr name, uint size, FixedItems fixedItems, ref TBuffer field) where TBuffer : unmanaged where T : unmanaged
        {
            var items = (ItemOps<T>)fixedItems.Items;
            Span<T> span = MemoryMarshal.CreateSpan(ref Unsafe.As<TBuffer, T>(ref field), fixedItems.Length);
            ItemBuffer b = items.Begin(span.Length);
            for (int i = 0; i < span.Length; i++)
                items.Store(b.Data + (long)i * b.ItemSize, span[i]);
            try
            {
                V(v)->Table->Array(V(v)->Self, (byte*)name, size, &b, items.Desc);
                if (Reading(v))
                    for (int i = 0; i < Math.Min(span.Length, (int)b.Count); i++)
                        span[i] = items.Load(b.Data + (long)i * b.ItemSize, span[i]);
            }
            finally
            {
                NativeMemory.Free(b.Data);
            }
        }

        // The object of a reference read from a file: its wrapper when it is a T, else null.
        internal static T Resolve<T>(ulong id) where T : class
        {
            if (id == 0 || s_ObjectType == null)
                return null;
            uint type = s_ObjectType(id);
            if (type == 0)
                return null;
            if (Objects.Wrap<Object>(id, type) is T known)
                return known;
            // An object of a type C# has no class for (a C++ script): wrapped as the field's type when it is one.
            uint wanted = Types.Of(typeof(T));
            return wanted != 0 && s_ObjectIsA != null && s_ObjectIsA(id, wanted) != 0 ? Objects.Wrap<T>(id, 0) : null;
        }

        internal static int Push(object o)
        {
            s_Slots.Add(o);
            return s_Slots.Count - 1;
        }

        internal static object Slot(int index) => s_Slots[index];

        internal static void Pin(byte[] bytes, out byte* data)
        {
            GCHandle h = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            s_Pins.Add(h);
            data = (byte*)h.AddrOfPinnedObject();
        }

        internal static string Result(int index) => s_Results[index];
        internal static byte* Zeros => s_Zeros;

        static void Release(int slots, int results, int pins)
        {
            s_Slots.RemoveRange(slots, s_Slots.Count - slots);
            s_Results.RemoveRange(results, s_Results.Count - results);
            for (int i = pins; i < s_Pins.Count; i++)
                s_Pins[i].Free();
            s_Pins.RemoveRange(pins, s_Pins.Count - pins);
        }

        // ------------------------------------------------------------------------------------------- the native side calls

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void StringGet(void* s, DynamicString* type, byte** data, uint* size)
        {
            var slot = (StringSlot*)s;
            *data = slot->Data;
            *size = slot->Size;
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void StringSet(void* s, DynamicString* type, byte* data, uint size)
        {
            try
            {
                var slot = (StringSlot*)s;
                s_Results.Add(Encoding.UTF8.GetString(data, (int)size));
                slot->Result = s_Results.Count - 1;
            }
            catch (Exception e)
            {
                Native.ReportException(e);
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static ulong ReferenceGet(void* r, DynamicReference* type) => *(ulong*)r;

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void ReferenceSet(void* r, DynamicReference* type, ulong id) => *(ulong*)r = id;

        // An object of a Serializable type the native side visits: its fields, to the depth limit.
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void StructTransfer(void* o, DynamicVisitor* v, DynamicStruct* type)
        {
            SerializedType st = s_TypeList[(int)(nint)type->User];
            if (s_Depth >= DepthLimit)
            {
                if (s_DepthWarned.Add(st.Type))
                    ScriptBridge.Warn($"Serialization depth limit {DepthLimit} exceeded at '{st.Type.FullName}'. There may be an object composition cycle in one or more of your serialized classes.");
                return;
            }
            s_Depth++;
            try
            {
                object instance = s_Slots[*(int*)o];
                if ((v->Flags & 4) != 0)
                    BeforeSerialize(instance);
                st.Transfer?.Invoke(instance, (IntPtr)v);
                if ((v->Flags & 1) != 0 && s_Read != null && !s_Read.Exists(x => ReferenceEquals(x, instance)))
                    s_Read.Add(instance);
            }
            catch (Exception e)
            {
                Native.ReportException(e);
            }
            finally
            {
                s_Depth--;
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static uint BufferSize(void* a, DynamicArray* type) => ((ItemBuffer*)a)->Count;

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static void BufferResize(void* a, uint count, DynamicArray* type)
        {
            try
            {
                var b = (ItemBuffer*)a;
                if (count > b->Capacity)
                {
                    uint capacity = Math.Max(count, b->Capacity * 2);
                    b->Data = (byte*)NativeMemory.Realloc(b->Data, (nuint)capacity * b->ItemSize);
                    b->Capacity = capacity;
                }
                ItemOps items = s_ItemList[b->Items];
                for (uint i = b->Count; i < count; i++)
                    items.Grow(b->Data + (long)i * b->ItemSize);
                b->Count = count;
            }
            catch (Exception e)
            {
                Native.ReportException(e);
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static void* BufferItem(void* a, uint index, DynamicArray* type)
        {
            var b = (ItemBuffer*)a;
            return b->Data + (long)index * b->ItemSize;
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static void* BufferPrototype(DynamicArray* type)
        {
            try
            {
                return s_ItemList[(int)(nint)type->User].Prototype();
            }
            catch (Exception e)
            {
                Native.ReportException(e);
                return s_Zeros;
            }
        }

        internal static DynamicString* StringType => s_StringType;
        internal static DynamicReference* ReferenceType => s_ReferenceType;
    }

    /// <summary>The items of a fixed size buffer and its length.</summary>
    internal sealed class FixedItems
    {
        public readonly ItemOps Items;
        public readonly int Length;

        public FixedItems(ItemOps items, int length)
        {
            Items = items;
            Length = length;
        }
    }

    /// <summary>A Serializable class or struct of script code: its native descriptor and its compiled fields.</summary>
    internal sealed unsafe class SerializedType
    {
        public Type Type;
        public int Index;
        public DynamicStruct* Desc;
        // The object (a StrongBox of a struct) and the visitor.
        public Action<object, IntPtr> Transfer;
        public Func<object> New;
        // As a managed reference's data.
        public DynamicStruct* DataDesc;
        // SerializeReference fields of its own or of a Serializable type it holds inline.
        public bool HasReferences;
    }

    /// <summary>The items of an array or list of one type, as the native side reaches them.</summary>
    internal abstract unsafe class ItemOps
    {
        public DynamicArray* Desc;
        public uint ItemSize;
        // Items changed without a read (references the remap changes, struct boxes) are copied back.
        public bool WritesBack;
        // The Serializable type of the items.
        public SerializedType Holds;
        int _index;

        protected ItemOps(uint kind, uint itemSize)
        {
            ItemSize = itemSize;
            Desc = (DynamicArray*)NativeMemory.AllocZeroed((nuint)sizeof(DynamicArray));
            Desc->ItemKind = kind;
            Desc->Size = &SerializedFields.BufferSize;
            Desc->Resize = &SerializedFields.BufferResize;
            Desc->Item = &SerializedFields.BufferItem;
            Desc->Prototype = &SerializedFields.BufferPrototype;
        }

        public void Register(List<ItemOps> list)
        {
            _index = list.Count;
            list.Add(this);
            Desc->User = (void*)_index;
        }

        public ItemBuffer Begin(int count)
        {
            var b = new ItemBuffer { Count = (uint)count, Capacity = (uint)Math.Max(count, 1), ItemSize = ItemSize, Items = _index };
            b.Data = (byte*)NativeMemory.AllocZeroed((nuint)b.Capacity * ItemSize);
            return b;
        }

        // A new item the native side added (zeroed memory).
        public virtual void Grow(byte* at) { }
        // The item the type tree describes.
        public virtual void* Prototype() => SerializedFields.Zeros;
    }

    internal abstract unsafe class ItemOps<T> : ItemOps
    {
        protected ItemOps(uint kind, uint itemSize) : base(kind, itemSize) { }
        public abstract void Store(byte* at, T value);
        public abstract T Load(byte* at, T old);
    }

    // Numbers, bools, enums and Helix.Math value types: their bytes.
    internal sealed unsafe class UnmanagedItems<T> : ItemOps<T> where T : unmanaged
    {
        public UnmanagedItems(uint kind, IntPtr itemStruct) : base(kind, (uint)sizeof(T)) => Desc->ItemStruct = (DynamicStruct*)itemStruct;
        public override void Store(byte* at, T value) => *(T*)at = value;
        public override T Load(byte* at, T old) => *(T*)at;
    }

    internal sealed unsafe class StringItems : ItemOps<string>
    {
        public StringItems() : base((uint)FieldKind.String, (uint)sizeof(StringSlot)) => Desc->ItemString = SerializedFields.StringType;

        public override void Store(byte* at, string value)
        {
            var slot = (StringSlot*)at;
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? "");
            SerializedFields.Pin(bytes, out slot->Data);
            slot->Size = (uint)bytes.Length;
            slot->Result = -1;
        }

        public override void Grow(byte* at) => ((StringSlot*)at)->Result = -1;

        public override string Load(byte* at, string old)
        {
            var slot = (StringSlot*)at;
            return slot->Result >= 0 ? SerializedFields.Result(slot->Result) : old ?? "";
        }
    }

    internal sealed unsafe class ReferenceItems<T> : ItemOps<T> where T : class
    {
        public ReferenceItems() : base((uint)FieldKind.Reference, 8)
        {
            Desc->ItemReference = SerializedFields.ReferenceType;
            WritesBack = true;
        }

        public override void Store(byte* at, T value) => *(ulong*)at = Objects.IdOf(value);

        public override T Load(byte* at, T old)
        {
            ulong id = *(ulong*)at;
            return id == Objects.IdOf(old) ? old : SerializedFields.Resolve<T>(id);
        }
    }

    // Serializable classes and structs: slots of their objects (a struct in a box).
    internal sealed unsafe class ManagedItems<T> : ItemOps<T>
    {
        readonly SerializedType _type;
        readonly int* _prototype;

        public ManagedItems(SerializedType type) : base((uint)FieldKind.Struct, 4)
        {
            _type = type;
            Holds = type;
            Desc->ItemStruct = type.Desc;
            WritesBack = true;
            _prototype = (int*)NativeMemory.AllocZeroed(4);
        }

        public override void Store(byte* at, T value)
        {
            object o = typeof(T).IsValueType ? new StrongBox<T>(value) : (object)value ?? _type.New();
            *(int*)at = SerializedFields.Push(o);
        }

        public override void Grow(byte* at) => *(int*)at = SerializedFields.Push(_type.New());

        public override T Load(byte* at, T old)
        {
            object o = SerializedFields.Slot(*(int*)at);
            return o is StrongBox<T> box ? box.Value : (T)o;
        }

        public override void* Prototype()
        {
            *_prototype = SerializedFields.Push(_type.New());
            return _prototype;
        }
    }
}
