using System.Text;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.Common;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.Rust;

/// <summary>A parameter of a problem's method, in Rust.</summary>
/// <param name="Name">The parameter's name in snake_case.</param>
/// <param name="Type">Its Rust type; an in-place parameter (<c>&amp;mut Vec&lt;Vec&lt;i32&gt;&gt;</c>) starts with <c>&amp;mut</c>.</param>
internal sealed record RustParameter(string Name, string Type);

/// <summary>A problem's solution method as LeetCode writes it in Rust: <c>pub fn two_sum(nums: Vec&lt;i32&gt;, target: i32) -&gt; Vec&lt;i32&gt;</c>.</summary>
/// <param name="ReturnType">The Rust return type, or null when the method changes its argument in place and returns nothing.</param>
internal sealed record RustMethod(string Name, IReadOnlyList<RustParameter> Parameters, string? ReturnType)
{
    public string Signature => $"pub fn {Name}({string.Join(", ", Parameters.Select(p => $"{p.Name}: {p.Type}"))}){(ReturnType == null ? string.Empty : " -> " + ReturnType)}";

    /// <summary>A body that returns something of the right type, so an unsolved problem builds and every case shows ❌ instead of the script failing to compile.</summary>
    public string DefaultBody => ReturnType == null ? string.Empty : RustProblemSignature.DefaultValue(ReturnType);
}

/// <summary>
/// Turns the C# signature a problem's reference solution has into the Rust one LeetCode shows: the types
/// (<c>int[]</c> is <c>Vec&lt;i32&gt;</c>, <c>ListNode</c> is <c>Option&lt;Box&lt;ListNode&gt;&gt;</c>), the snake_case names, and the value an
/// unsolved method returns.
/// </summary>
internal static class RustProblemSignature
{
    /// <param name="problemNumber">Linked List Cycle (141) needs nodes that can be shared, so a tail can point back into the list: its <c>ListNode</c> is an <c>Rc&lt;RefCell&lt;ListNode&gt;&gt;</c> list, not a <c>Box</c> one.</param>
    public static RustMethod For(string csharpSolution, string problemTitle, int problemNumber = 0)
    {
        var info = ProblemCodeTranspiler.ExtractMethodInfo(csharpSolution, problemTitle);
        var inPlace = info.ReturnType == "void";

        var parameters = new List<RustParameter>();
        for (var i = 0; i < info.Parameters.Count; i++)
        {
            var type = MapType(info.Parameters[i].Type);
            parameters.Add(new RustParameter(Identifier(SnakeCase(info.Parameters[i].Name)), inPlace && i == 0 ? "&mut " + type : type));
        }

        var method = new RustMethod(Identifier(SnakeCase(info.Name)), parameters, inPlace ? null : MapType(info.ReturnType));
        return problemNumber == SharedNodeProblem ? SharedNodes(method) : method;
    }

    /// <summary>Linked List Cycle.</summary>
    public const int SharedNodeProblem = 141;

    private static RustMethod SharedNodes(RustMethod method)
    {
        static string Shared(string type) => type.Replace("Option<Box<ListNode>>", "Option<Rc<RefCell<ListNode>>>");
        return new RustMethod(method.Name, method.Parameters.Select(p => new RustParameter(p.Name, Shared(p.Type))).ToList(), method.ReturnType == null ? null : Shared(method.ReturnType));
    }

    /// <summary>The Rust type for a C# type in a problem's signature.</summary>
    public static string MapType(string csharpType)
    {
        var type = csharpType.Trim().Replace(" ", string.Empty);

        // T[] and IList<T> / List<T> are a Vec<T> of whatever T is, however deep.
        if (type.EndsWith("[]", StringComparison.Ordinal)) return $"Vec<{MapType(type[..^2])}>";
        foreach (var prefix in new[] { "IList<", "List<", "IEnumerable<", "ICollection<" })
        {
            if (type.StartsWith(prefix, StringComparison.Ordinal) && type.EndsWith('>')) return $"Vec<{MapType(type[prefix.Length..^1])}>";
        }

        return type switch
        {
            "int" or "Int32" => "i32",
            "uint" or "UInt32" => "u32",
            "long" or "Int64" => "i64",
            "ulong" or "UInt64" => "u64",
            "short" => "i16",
            "byte" => "u8",
            "double" or "float" => "f64",
            "bool" or "Boolean" => "bool",
            "string" or "String" => "String",
            "char" => "char",
            "ListNode" => "Option<Box<ListNode>>",
            "TreeNode" => "Option<Rc<RefCell<TreeNode>>>",
            "Node" => "Option<Rc<RefCell<Node>>>",
            _ => type
        };
    }

    /// <summary>An expression of <paramref name="rustType"/> that means "nothing yet".</summary>
    public static string DefaultValue(string rustType) => rustType switch
    {
        "bool" => "false",
        "String" => "String::new()",
        "f64" or "f32" => "0.0",
        "char" => "' '",
        _ when rustType.StartsWith("Vec<", StringComparison.Ordinal) => "Vec::new()",
        _ when rustType.StartsWith("Option<", StringComparison.Ordinal) => "None",
        _ when rustType.StartsWith('i') || rustType.StartsWith('u') => "0",
        _ => "Default::default()"
    };

    /// <summary><c>IsValidBST</c> is <c>is_valid_bst</c>, <c>LengthOfLIS</c> is <c>length_of_lis</c>, <c>reverseBits</c> is <c>reverse_bits</c>, <c>list1</c> stays.</summary>
    public static string SnakeCase(string name)
    {
        var text = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                var startsWord = i > 0 && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1]));
                var endsAcronym = i > 0 && char.IsUpper(name[i - 1]) && i + 1 < name.Length && char.IsLower(name[i + 1]);
                if (startsWord || endsAcronym) text.Append('_');
                text.Append(char.ToLowerInvariant(c));
            }
            else
            {
                text.Append(c);
            }
        }

        return text.ToString();
    }

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "as", "break", "const", "continue", "else", "enum", "extern", "false", "fn", "for", "if", "impl", "in", "let", "loop",
        "match", "mod", "move", "mut", "pub", "ref", "return", "static", "struct", "trait", "true", "type", "unsafe", "use",
        "where", "while", "async", "await", "dyn", "abstract", "become", "box", "do", "final", "macro", "override", "priv",
        "typeof", "unsized", "virtual", "yield", "try"
    };

    // A C# name that is a Rust keyword (a parameter called `type` or `match`) becomes a raw identifier.
    private static string Identifier(string name) => Keywords.Contains(name) ? "r#" + name : name;
}
