using System.Reflection;
using System.Runtime.Loader;
using System.Text;

namespace Helix.ApiInventory;

/// <summary>
/// The public surface of the HelixEngine assemblies against the inventory:
///   HelixApiInventory --assembly HelixEngine.CoreModule.dll [--reference dir|dll ...] --inventory HelixEngine.csv [--write]
/// The inventory has a row per public member (type, member, signature; overloads joined with " / "; operators as one
/// row "operators", constructors as ".ctor"). A member of the assembly without a row, or a row without its member,
/// is listed and fails (exit 1); --write rewrites the inventory from the assembly instead.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        string? assembly = null, inventory = null;
        bool write = false;
        var references = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            string Value() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value");
            switch (args[i])
            {
                case "--assembly": assembly = Value(); break;
                case "--inventory": inventory = Value(); break;
                case "--reference": references.Add(Value()); break;
                case "--write": write = true; break;
                default: throw new ArgumentException($"unknown argument {args[i]}");
            }
        }
        if (assembly == null || inventory == null)
        {
            Console.Error.WriteLine("HelixApiInventory: --assembly and --inventory are needed");
            return 2;
        }

        SortedDictionary<(string Type, string Member), string> surface = Surface(Load(assembly, references));
        if (write)
        {
            var sb = new StringBuilder("type,member,signature\n");
            foreach (var ((type, member), signature) in surface)
                sb.Append(Csv(type)).Append(',').Append(Csv(member)).Append(',').Append(Csv(signature)).Append('\n');
            File.WriteAllText(inventory, sb.ToString(), new UTF8Encoding(false));
            Console.WriteLine($"HelixApiInventory: {surface.Count} rows written to {inventory}");
            return 0;
        }

        var rows = new HashSet<(string, string)>();
        foreach (string line in File.ReadAllLines(inventory).Skip(1).Where(l => l.Trim().Length > 0))
        {
            List<string> cells = ParseCsv(line);
            rows.Add((cells[0], cells.Count > 1 ? cells[1] : ""));
        }
        int failures = 0;
        foreach (var (key, signature) in surface)
            if (!rows.Contains(key))
            {
                Console.WriteLine($"not in the inventory: {key.Type}.{key.Member}: {signature}");
                failures++;
            }
        foreach (var key in rows)
            if (!surface.ContainsKey(key))
            {
                Console.WriteLine($"not in the assembly: {key.Item1}.{key.Item2}");
                failures++;
            }
        Console.WriteLine(failures == 0 ? $"HelixApiInventory: {surface.Count} members match the inventory" : $"HelixApiInventory: {failures} differences");
        return failures == 0 ? 0 : 1;
    }

    // The assembly in a load context of its own; references from the given folders and files.
    static Assembly Load(string path, List<string> references)
    {
        var context = new AssemblyLoadContext("HelixApiInventory");
        var folders = new List<string> { Path.GetDirectoryName(Path.GetFullPath(path))! };
        foreach (string r in references)
            folders.Add(Directory.Exists(r) ? Path.GetFullPath(r) : Path.GetDirectoryName(Path.GetFullPath(r))!);
        context.Resolving += (ctx, name) =>
        {
            foreach (string folder in folders)
            {
                string candidate = Path.Combine(folder, name.Name + ".dll");
                if (File.Exists(candidate))
                    return ctx.LoadFromAssemblyPath(candidate);
            }
            return null;
        };
        return context.LoadFromAssemblyPath(Path.GetFullPath(path));
    }

    static bool InSurface(Type t) =>
        t.Namespace != null && (t.Namespace == "HelixEngine" || t.Namespace.StartsWith("HelixEngine.", StringComparison.Ordinal)) &&
        !t.Namespace.EndsWith(".Internal", StringComparison.Ordinal) && !t.Namespace.Contains(".Internal.", StringComparison.Ordinal);

    static bool Visible(Type t) => t.IsPublic || (t.IsNestedPublic && t.DeclaringType != null && Visible(t.DeclaringType));

    static string TypeName(Type t) => t.IsNested ? TypeName(t.DeclaringType!) + "." + Strip(t.Name) : (t.Namespace!.Length > 11 ? t.Namespace[12..] + "." : "") + Strip(t.Name);

    static string Strip(string name) => name.Contains('`') ? name[..name.IndexOf('`')] : name;

    static SortedDictionary<(string, string), string> Surface(Assembly assembly)
    {
        var surface = new SortedDictionary<(string, string), string>();
        void Add(string type, string member, string signature)
        {
            surface[(type, member)] = surface.TryGetValue((type, member), out string? known) ? known + " / " + signature : signature;
        }
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            types = e.Types.Where(t => t != null).ToArray()!;
        }
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (Type t in types.Where(t => Visible(t) && InSurface(t)).OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            string tn = TypeName(t);
            if (t.IsEnum)
            {
                foreach (string n in Enum.GetNames(t))
                    Add(tn, n, $"{tn}.{n} = {Convert.ToInt64(Enum.Parse(t, n))}");
                continue;
            }
            if (typeof(Delegate).IsAssignableFrom(t))
            {
                MethodInfo invoke = t.GetMethod("Invoke")!;
                Add(tn, "delegate", $"{Name(invoke.ReturnType)} ({Params(invoke)})");
                continue;
            }
            foreach (ConstructorInfo c in t.GetConstructors(flags).Where(c => !c.IsStatic))
                Add(tn, ".ctor", $"{Strip(t.Name)}({Params(c)})");
            foreach (FieldInfo f in t.GetFields(flags).Where(f => !f.IsSpecialName))
                Add(tn, f.Name, $"{(f.IsLiteral ? "const " : f.IsStatic ? "static " : "")}{Name(f.FieldType)} {f.Name}");
            foreach (PropertyInfo p in t.GetProperties(flags))
            {
                MethodInfo? get = p.GetGetMethod(), set = p.GetSetMethod();
                bool isStatic = (get ?? set)!.IsStatic;
                string name = p.GetIndexParameters().Length > 0 ? "this[]" : p.Name;
                string index = p.GetIndexParameters().Length > 0 ? "[" + string.Join(", ", p.GetIndexParameters().Select(x => Name(x.ParameterType))) + "]" : "";
                Add(tn, name, $"{(isStatic ? "static " : "")}{Name(p.PropertyType)} {name}{index} {{{(get != null ? " get;" : "")}{(set != null ? " set;" : "")} }}");
            }
            foreach (EventInfo e in t.GetEvents(flags))
                Add(tn, e.Name, $"{(e.AddMethod!.IsStatic ? "static " : "")}event {Name(e.EventHandlerType!)} {e.Name}");
            foreach (MethodInfo m in t.GetMethods(flags))
            {
                if (m.Name.StartsWith("op_", StringComparison.Ordinal))
                {
                    Add(tn, "operators", $"{m.Name[3..]}({Params(m)})");
                    continue;
                }
                if (m.IsSpecialName || m.Name is "Equals" or "GetHashCode" or "Finalize" or "Deconstruct" || m.Name.StartsWith("<", StringComparison.Ordinal))
                    continue;
                string generic = m.IsGenericMethodDefinition ? "<" + string.Join(", ", m.GetGenericArguments().Select(a => a.Name)) + ">" : "";
                Add(tn, m.Name, $"{(m.IsStatic ? "static " : "")}{Name(m.ReturnType)} {m.Name}{generic}({Params(m)})");
            }
        }
        return surface;
    }

    static string Params(MethodBase m) => string.Join(", ", m.GetParameters().Select(p =>
        (p.IsOut ? "out " : p.ParameterType.IsByRef ? "ref " : p.GetCustomAttributes(typeof(ParamArrayAttribute), false).Length > 0 ? "params " : "") +
        Name(p.ParameterType.IsByRef ? p.ParameterType.GetElementType()! : p.ParameterType) + (p.HasDefaultValue ? " = " + (p.DefaultValue ?? "null") : "")));

    static string Name(Type t)
    {
        if (t.IsGenericParameter)
            return t.Name;
        if (t.IsArray)
            return Name(t.GetElementType()!) + "[]";
        if (t.IsByRef)
            return Name(t.GetElementType()!);
        string name = t.FullName switch
        {
            "System.Void" => "void",
            "System.Boolean" => "bool",
            "System.Int32" => "int",
            "System.UInt32" => "uint",
            "System.Int64" => "long",
            "System.UInt64" => "ulong",
            "System.Int16" => "short",
            "System.UInt16" => "ushort",
            "System.Byte" => "byte",
            "System.SByte" => "sbyte",
            "System.Single" => "float",
            "System.Double" => "double",
            "System.String" => "string",
            "System.Object" => "object",
            _ => t.IsNested ? Name(t.DeclaringType!) + "." + Strip(t.Name) : Strip(t.Name),
        };
        if (t.IsGenericType && !t.IsGenericTypeDefinition)
            name += "<" + string.Join(", ", t.GetGenericArguments().Select(Name)) + ">";
        return name;
    }

    static string Csv(string s) => s.Contains(',') || s.Contains('"') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    static List<string> ParseCsv(string line)
    {
        var cells = new List<string>();
        var sb = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else if (c == '"')
                    quoted = false;
                else
                    sb.Append(c);
            }
            else if (c == '"')
                quoted = true;
            else if (c == ',')
            {
                cells.Add(sb.ToString());
                sb.Clear();
            }
            else
                sb.Append(c);
        }
        cells.Add(sb.ToString());
        return cells;
    }
}
