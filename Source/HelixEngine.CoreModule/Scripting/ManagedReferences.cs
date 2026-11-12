using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Helix.Interop;

namespace HelixEngine.Internal
{
    // SerializeReference: a field is {rid}, the host's objects are its references section after its fields
    // (references {version: 2, RefIds: [{rid, type: {class, ns, asm}, data}]}).
    internal static unsafe partial class SerializedFields
    {
        // The rid of null, and the first rid given.
        const long NullRid = -2;
        const long FirstRid = 1000;

        static DynamicStruct* s_RidDesc;
        static DynamicStruct* s_RegistryDesc;
        static DynamicStruct* s_EntryDesc;
        static DynamicStruct* s_TypeDesc;
        static DynamicStruct* s_EmptyDataDesc;
        static DynamicVisitor* s_Collector;
        static ItemOps<long> s_RidItems;
        static EntryItems s_EntryItems;
        static ReferenceScope s_Scope;
        // Objects keep their rid across writes.
        static readonly ConditionalWeakTable<object, StrongBox<long>> s_Rids = new();
        static readonly Dictionary<(string, string, string), Type> s_Found = new();
        static readonly HashSet<string> s_Missing = new();

        static void StartReferences()
        {
            s_RidDesc = Desc("managedReference", 0, &RidTransfer);
            s_RegistryDesc = Desc("ManagedReferencesRegistry", 0, &RegistryTransfer);
            s_EntryDesc = Desc("ReferencedObject", 0, &EntryTransfer);
            s_TypeDesc = Desc("ReferencedManagedType", 1, &TypeTransfer);
            s_EmptyDataDesc = Desc("ReferencedObjectData", 2, &EmptyTransfer);
            var table = (VisitorTable*)NativeMemory.AllocZeroed((nuint)sizeof(VisitorTable));
            table->Value = &CollectValue;
            table->Text = &CollectText;
            table->Reference = &CollectReference;
            table->Structure = &CollectStructure;
            table->Array = &CollectArray;
            table->Version = &CollectVersion;
            table->ObjectReference = &CollectObjectReference;
            s_Collector = (DynamicVisitor*)NativeMemory.AllocZeroed((nuint)sizeof(DynamicVisitor));
            s_Collector->Table = table;
            s_RidItems = new UnmanagedItems<long>((uint)FieldKind.Struct, (IntPtr)s_RidDesc);
            s_RidItems.Register(s_ItemList);
            s_EntryItems = new EntryItems(s_EntryDesc);
            s_EntryItems.Register(s_ItemList);
        }

        static DynamicStruct* Desc(string name, uint flags, delegate* unmanaged[Cdecl]<void*, DynamicVisitor*, DynamicStruct*, void> transfer)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(name);
            var d = (DynamicStruct*)NativeMemory.AllocZeroed((nuint)sizeof(DynamicStruct));
            d->TypeName = (byte*)NativeMemory.Alloc((nuint)bytes.Length);
            bytes.AsSpan().CopyTo(new Span<byte>(d->TypeName, bytes.Length));
            d->TypeNameSize = (uint)bytes.Length;
            d->Flags = flags;
            d->Version = 1;
            d->Transfer = transfer;
            return d;
        }

        // A field with SerializeReference: a class, an abstract class, an interface or object (not an Object), or an
        // array or list of them.
        static Call ReferenceCall(Type t)
        {
            static bool Referable(Type x) => !x.IsValueType && !x.IsPointer && x != typeof(string) && !typeof(Object).IsAssignableFrom(x) && !x.IsArray &&
                                             !(x.IsGenericType && x.GetGenericTypeDefinition() == typeof(List<>));
            if (t.IsArray && t.GetArrayRank() == 1 && Referable(t.GetElementType()))
                return new Call { Method = Helper(nameof(ManagedReferences)).MakeGenericMethod(t.GetElementType()), Reference = true };
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>) && Referable(t.GetGenericArguments()[0]))
                return new Call { Method = Helper(nameof(ManagedReferenceList)).MakeGenericMethod(t.GetGenericArguments()[0]), Reference = true };
            if (Referable(t))
                return new Call { Method = Helper(nameof(ManagedReference)).MakeGenericMethod(t), Reference = true };
            return default;
        }

        // The host's fields, then its references: the objects first found (a pass that only follows structs and
        // arrays), so the section knows its entries; the fields of class hosts set once the entries are read.
        static void TransferWithReferences(object script, Action<object, IntPtr> fields, IntPtr visitor)
        {
            ReferenceScope saved = s_Scope;
            var scope = new ReferenceScope { Collecting = true };
            s_Scope = scope;
            try
            {
                fields(script, (IntPtr)s_Collector);
                while (scope.Processed < scope.Order.Count)
                {
                    object o = scope.Order[scope.Processed++];
                    TypeOf(o.GetType())?.Transfer?.Invoke(o, (IntPtr)s_Collector);
                }
                scope.Collecting = false;
                fields(script, visitor);
                int slot = Push(scope);
                (IntPtr name, uint size) = Name("references");
                V(visitor)->Table->Structure(V(visitor)->Self, (byte*)name, size, &slot, s_RegistryDesc);
                if (Reading(visitor))
                    foreach (Action fix in scope.Fixups)
                        fix();
            }
            finally
            {
                s_Scope = saved;
            }
        }

        public static void ManagedReference<T>(IntPtr v, IntPtr name, uint size, Action<object, object> set, object owner, ref T field) where T : class
        {
            ReferenceScope scope = s_Scope;
            if (scope == null)
                return;
            long rid = scope.IdOf(field), before = rid;
            if (scope.Collecting)
                return;
            V(v)->Table->Structure(V(v)->Self, (byte*)name, size, &rid, s_RidDesc);
            if (Reading(v) && rid != before)
            {
                long read = rid;
                scope.Fixups.Add(() => set(owner, scope.Resolve(read, typeof(T))));
            }
        }

        public static void ManagedReferences<T>(IntPtr v, IntPtr name, uint size, Action<object, object> set, object owner, ref T[] field) where T : class
        {
            T[] items = field ?? Array.Empty<T>();
            T[] result = References(v, name, size, items, items.Length);
            if (result != null)
                field = result;
        }

        public static void ManagedReferenceList<T>(IntPtr v, IntPtr name, uint size, Action<object, object> set, object owner, ref List<T> field) where T : class
        {
            List<T> items = field ?? new List<T>();
            T[] result = References(v, name, size, items, items.Count);
            if (result == null)
                return;
            field ??= new List<T>();
            List<T> list = field;
            list.Clear();
            list.AddRange(result);
            // Again once the items read are set.
            s_Scope.Fixups.Add(() =>
            {
                list.Clear();
                list.AddRange(result);
            });
        }

        // The rids of the items; when reading, the items as read (unchanged rids keep their objects, the others set
        // once the entries are read).
        static T[] References<T>(IntPtr v, IntPtr name, uint size, IList<T> items, int count) where T : class
        {
            ReferenceScope scope = s_Scope;
            if (scope == null)
                return null;
            var rids = new long[count];
            for (int i = 0; i < count; i++)
                rids[i] = scope.IdOf(items[i]);
            if (scope.Collecting)
                return null;
            ItemBuffer b = s_RidItems.Begin(count);
            for (int i = 0; i < count; i++)
                s_RidItems.Store(b.Data + (long)i * b.ItemSize, rids[i]);
            try
            {
                V(v)->Table->Array(V(v)->Self, (byte*)name, size, &b, s_RidItems.Desc);
                if (!Reading(v))
                    return null;
                var result = new T[b.Count];
                for (int i = 0; i < result.Length; i++)
                {
                    long rid = s_RidItems.Load(b.Data + (long)i * b.ItemSize, 0);
                    if (i < count && rid == rids[i])
                        result[i] = items[i];
                    else
                    {
                        int at = i;
                        scope.Fixups.Add(() => result[at] = scope.Resolve(rid, typeof(T)) as T);
                    }
                }
                return result;
            }
            finally
            {
                NativeMemory.Free(b.Data);
            }
        }

        // The type a managed reference's entry names, null when no loaded assembly has it.
        static Type FindType(string assembly, string ns, string name)
        {
            if (s_Found.TryGetValue((assembly, ns, name), out Type known))
                return known;
            string full = (ns.Length > 0 ? ns + "." : "") + name.Replace('/', '+');
            Type found = null;
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                if (a.GetName().Name == assembly && a.GetType(full) is Type t)
                {
                    found = t;
                    break;
                }
            s_Found[(assembly, ns, name)] = found;
            return found;
        }

        internal static (string Class, string Namespace, string Assembly) NameOf(Type t)
        {
            string name = t.Name;
            for (Type outer = t.DeclaringType; outer != null; outer = outer.DeclaringType)
                name = outer.Name + "/" + name;
            return (name, t.Namespace ?? "", t.Assembly.GetName().Name ?? "");
        }

        // ------------------------------------------------------------------------------------------- the native side calls

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void RidTransfer(void* o, DynamicVisitor* v, DynamicStruct* type)
        {
            try
            {
                (IntPtr name, uint size) = Name("rid");
                Number((IntPtr)v, name, size, (uint)FieldKind.Int64, ref *(long*)o);
            }
            catch (Exception e)
            {
                Native.ReportException(e);
            }
        }

        // The section: its version and its entries (the found objects, then a null entry when a null was written).
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void RegistryTransfer(void* o, DynamicVisitor* v, DynamicStruct* type)
        {
            try
            {
                var scope = (ReferenceScope)Slot(*(int*)o);
                int version = 2;
                (IntPtr versionName, uint versionSize) = Name("version");
                Number((IntPtr)v, versionName, versionSize, (uint)FieldKind.Int32, ref version);
                var entries = new List<RefEntry>();
                foreach (object found in scope.Order)
                    if (scope.Ids.TryGetValue(found, out long rid))
                        entries.Add(RefEntry.Of(rid, found));
                if (scope.SawNull)
                    entries.Add(new RefEntry { Rid = NullRid });
                ItemBuffer b = s_EntryItems.Begin(entries.Count);
                for (int i = 0; i < entries.Count; i++)
                    *(int*)(b.Data + (long)i * b.ItemSize) = Push(entries[i]);
                try
                {
                    (IntPtr name, uint size) = Name("RefIds");
                    v->Table->Array(v->Self, (byte*)name, size, &b, s_EntryItems.Desc);
                }
                finally
                {
                    NativeMemory.Free(b.Data);
                }
            }
            catch (Exception e)
            {
                Native.ReportException(e);
            }
        }

        // An entry: its rid, its type, its data (the object of the type, made when reading).
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void EntryTransfer(void* o, DynamicVisitor* v, DynamicStruct* type)
        {
            try
            {
                var e = (RefEntry)Slot(*(int*)o);
                ReferenceScope scope = s_Scope;
                (IntPtr ridName, uint ridSize) = Name("rid");
                Number((IntPtr)v, ridName, ridSize, (uint)FieldKind.Int64, ref e.Rid);
                int typeSlot = Push(e);
                (IntPtr typeName, uint typeSize) = Name("type");
                v->Table->Structure(v->Self, (byte*)typeName, typeSize, &typeSlot, s_TypeDesc);
                if ((v->Flags & 1) != 0 && scope != null && e.Rid != NullRid)
                {
                    Type t = FindType(e.Assembly, e.Namespace, e.Class);
                    if (t == null || TypeOf(t) == null)
                    {
                        if (s_Missing.Add(e.Assembly + ":" + e.Namespace + "." + e.Class))
                            ScriptBridge.Warn($"The managed reference type {e.Namespace}.{e.Class} of {e.Assembly} is missing; its references read as null");
                        e.Object = null;
                    }
                    else
                    {
                        e.Object = scope.Objects.TryGetValue(e.Rid, out object existing) && existing.GetType() == t ? existing : TypeOf(t).New();
                        scope.Objects[e.Rid] = e.Object;
                        s_Rids.AddOrUpdate(e.Object, new StrongBox<long>(e.Rid));
                    }
                }
                SerializedType st = e.Object != null ? TypeOf(e.Object.GetType()) : null;
                int dataSlot = Push(e.Object);
                (IntPtr dataName, uint dataSize) = Name("data");
                v->Table->Structure(v->Self, (byte*)dataName, dataSize, &dataSlot, st != null ? st.DataDesc : s_EmptyDataDesc);
            }
            catch (Exception e)
            {
                Native.ReportException(e);
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void TypeTransfer(void* o, DynamicVisitor* v, DynamicStruct* type)
        {
            try
            {
                var e = (RefEntry)Slot(*(int*)o);
                (IntPtr c, uint cs) = Name("class");
                (IntPtr n, uint ns) = Name("ns");
                (IntPtr a, uint asz) = Name("asm");
                Text((IntPtr)v, c, cs, ref e.Class);
                Text((IntPtr)v, n, ns, ref e.Namespace);
                Text((IntPtr)v, a, asz, ref e.Assembly);
            }
            catch (Exception e)
            {
                Native.ReportException(e);
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void EmptyTransfer(void* o, DynamicVisitor* v, DynamicStruct* type) { }

        // The pass that finds the objects: it only follows structs and arrays of structs.
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void CollectValue(void* self, byte* name, uint size, uint kind, void* value) { }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void CollectText(void* self, byte* name, uint size, void* text, DynamicString* type) { }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void CollectReference(void* self, byte* name, uint size, ulong* id) { }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void CollectStructure(void* self, byte* name, uint size, void* o, DynamicStruct* type) => type->Transfer(o, s_Collector, type);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void CollectArray(void* self, byte* name, uint size, void* array, DynamicArray* type)
        {
            if (type->ItemKind != (uint)FieldKind.Struct)
                return;
            uint count = type->Size(array, type);
            for (uint i = 0; i < count; i++)
                type->ItemStruct->Transfer(type->Item(array, i, type), s_Collector, type->ItemStruct);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static int CollectVersion(void* self) => 1;

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void CollectObjectReference(void* self, ulong* id) { }

        // The rids of one host's objects.
        sealed class ReferenceScope
        {
            public bool Collecting;
            public bool SawNull;
            public int Processed;
            public long Next = FirstRid;
            public readonly Dictionary<object, long> Ids = new(ReferenceEqualityComparer.Instance);
            public readonly Dictionary<long, object> Objects = new();
            public readonly List<object> Order = new();
            public readonly List<Action> Fixups = new();

            // Null, and objects of types that are not Serializable, are -2.
            public long IdOf(object o)
            {
                if (o == null || TypeOf(o.GetType()) == null)
                {
                    SawNull = true;
                    return NullRid;
                }
                if (Ids.TryGetValue(o, out long id))
                    return id;
                if (!s_Rids.TryGetValue(o, out StrongBox<long> kept) || kept.Value < 0 || Objects.ContainsKey(kept.Value))
                {
                    while (Objects.ContainsKey(Next))
                        Next++;
                    id = Next++;
                    s_Rids.AddOrUpdate(o, new StrongBox<long>(id));
                }
                else
                    id = kept.Value;
                Ids[o] = id;
                Objects[id] = o;
                Order.Add(o);
                return id;
            }

            public object Resolve(long rid, Type type) => rid != NullRid && Objects.TryGetValue(rid, out object o) && type.IsInstanceOfType(o) ? o : null;
        }

        sealed class RefEntry
        {
            public long Rid;
            public string Class = "", Namespace = "", Assembly = "";
            public object Object;

            public static RefEntry Of(long rid, object o)
            {
                (string c, string n, string a) = NameOf(o.GetType());
                return new RefEntry { Rid = rid, Class = c, Namespace = n, Assembly = a, Object = o };
            }
        }

        // The entries of the section: slots of RefEntry objects.
        sealed class EntryItems : ItemOps
        {
            readonly int* _prototype;

            public EntryItems(DynamicStruct* entry) : base((uint)FieldKind.Struct, 4)
            {
                Desc->ItemStruct = entry;
                _prototype = (int*)NativeMemory.AllocZeroed(4);
            }

            public override void Grow(byte* at) => *(int*)at = Push(new RefEntry());

            public override void* Prototype()
            {
                *_prototype = Push(new RefEntry());
                return _prototype;
            }
        }
    }
}
