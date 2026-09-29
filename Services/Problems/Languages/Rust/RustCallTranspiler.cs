using System.Globalization;
using System.Text;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.Rust;

/// <summary>
/// Turns the C# expression a Blind 75 test is written in (<c>sol.TwoSum(new[] { 2, 7, 11, 15 }, 9)</c>,
/// <c>BuildTree(3, 9, null, 15)</c>, <c>Replay(new[] { "Trie", "insert" }, ...)</c>) into the Rust that makes the same call
/// (<c>Solution::two_sum(vec![2, 7, 11, 15], 9)</c>). It reads the small language the catalog uses (literals, arrays, calls) with a
/// parser, not text replacement, so a nested array or an argument that is itself a call comes out right; anything else is refused
/// with a <see cref="NotSupportedException"/> rather than turned into Rust that doesn't build.
/// </summary>
internal static class RustCallTranspiler
{
    /// <param name="call">The C# expression.</param>
    /// <param name="method">The problem's solution method, whose parameter types decide how a bare <c>2</c> is written (<c>2.0</c> for a <c>double</c>); null when unknown.</param>
    /// <param name="problemNumber">Which problem, for the helpers whose meaning depends on it (<c>RoundTrip</c>).</param>
    public static string Translate(string call, RustMethod? method, int problemNumber)
    {
        var tokens = Tokenize(call);
        var parser = new Parser(tokens);
        var node = parser.ParseExpression();
        parser.ExpectEnd();
        return new Emitter(method, problemNumber).Emit(node, null);
    }

    // ── Syntax tree ────────────────────────────────────────────────────────

    private abstract record Node;

    private sealed record IntLiteral(string Text, bool Unsigned) : Node;

    private sealed record DecimalLiteral(string Text) : Node;

    private sealed record StringLiteral(string Value) : Node;

    private sealed record CharLiteral(char Value) : Node;

    private sealed record BoolLiteral(bool Value) : Node;

    private sealed record NullLiteral : Node;

    private sealed record ArrayLiteral(IReadOnlyList<Node> Items) : Node;

    /// <summary><c>new int[0]</c>, <c>new int[0][]</c>, <c>new ListNode[0]</c>, <c>Array.Empty&lt;int&gt;()</c>: an empty array whose element type is written out.</summary>
    private sealed record EmptyArray(string ElementType, int Rank) : Node;

    private sealed record Call(string? Receiver, string Method, IReadOnlyList<Node> Arguments) : Node;

    /// <summary><c>int.MaxValue</c>, <c>int.MinValue</c>: a limit of a number type.</summary>
    private sealed record MemberAccess(string Type, string Member) : Node;

    /// <summary><c>new Codec().Encode(...)</c></summary>
    private sealed record NewObjectCall(string Type, string Method, IReadOnlyList<Node> Arguments) : Node;

    // ── Tokens ─────────────────────────────────────────────────────────────

    private enum Kind
    {
        Identifier,
        Number,
        String,
        Char,
        Symbol,
        End
    }

    private readonly record struct Token(Kind Kind, string Text);

    private static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
            }
            else if (char.IsLetter(c) || c == '_')
            {
                var start = i;
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) i++;
                tokens.Add(new Token(Kind.Identifier, text[start..i]));
            }
            else if (char.IsAsciiDigit(c))
            {
                var start = i;
                while (i < text.Length && (char.IsAsciiDigit(text[i]) || text[i] == '.' && i + 1 < text.Length && char.IsAsciiDigit(text[i + 1]))) i++;
                if (i < text.Length && text[i] is 'u' or 'U' or 'L' or 'l' or 'd' or 'D' or 'f' or 'F' or 'm' or 'M') i++;
                tokens.Add(new Token(Kind.Number, text[start..i]));
            }
            else if (c == '"')
            {
                var value = new StringBuilder();
                i++;
                while (i < text.Length && text[i] != '"')
                {
                    if (text[i] == '\\' && i + 1 < text.Length)
                    {
                        i++;
                        value.Append(text[i] switch { 'n' => '\n', 't' => '\t', 'r' => '\r', '0' => '\0', var other => other });
                    }
                    else
                    {
                        value.Append(text[i]);
                    }

                    i++;
                }

                if (i >= text.Length) throw new NotSupportedException("A string in the call never ends.");
                i++;
                tokens.Add(new Token(Kind.String, value.ToString()));
            }
            else if (c == '\'')
            {
                i++;
                char value;
                if (i < text.Length && text[i] == '\\' && i + 1 < text.Length)
                {
                    i++;
                    value = text[i] switch { 'n' => '\n', 't' => '\t', 'r' => '\r', '0' => '\0', var other => other };
                }
                else if (i < text.Length)
                {
                    value = text[i];
                }
                else
                {
                    throw new NotSupportedException("A character in the call never ends.");
                }

                i++;
                if (i >= text.Length || text[i] != '\'') throw new NotSupportedException("A character in the call isn't closed.");
                i++;
                tokens.Add(new Token(Kind.Char, value.ToString()));
            }
            else if ("(){}[],.<>-?".Contains(c))
            {
                tokens.Add(new Token(Kind.Symbol, c.ToString()));
                i++;
            }
            else
            {
                throw new NotSupportedException($"The call has '{c}', which the Rust translation doesn't read.");
            }
        }

        tokens.Add(new Token(Kind.End, string.Empty));
        return tokens;
    }

    // ── Parser ─────────────────────────────────────────────────────────────

    private sealed class Parser(List<Token> tokens)
    {
        private int _at;

        private Token Peek(int ahead = 0) => tokens[Math.Min(_at + ahead, tokens.Count - 1)];

        private Token Next() => tokens[_at++];

        private bool IsSymbol(string text, int ahead = 0) => Peek(ahead) is { Kind: Kind.Symbol } t && t.Text == text;

        private bool IsWord(string text, int ahead = 0) => Peek(ahead) is { Kind: Kind.Identifier } t && t.Text == text;

        private void Expect(string symbol)
        {
            if (!IsSymbol(symbol)) throw new NotSupportedException($"Expected '{symbol}' in the call but found '{Peek().Text}'.");
            _at++;
        }

        public void ExpectEnd()
        {
            if (Peek().Kind != Kind.End) throw new NotSupportedException($"Unexpected '{Peek().Text}' after the call.");
        }

        public Node ParseExpression()
        {
            var t = Peek();
            switch (t.Kind)
            {
                case Kind.Number:
                    _at++;
                    return Number(t.Text, negative: false);
                case Kind.Symbol when t.Text == "-" && Peek(1).Kind == Kind.Number:
                    _at += 2;
                    return Number(Peek(-1).Text, negative: true);
                case Kind.String:
                    _at++;
                    return new StringLiteral(t.Text);
                case Kind.Char:
                    _at++;
                    return new CharLiteral(t.Text[0]);
                case Kind.Identifier when t.Text == "true" || t.Text == "false":
                    _at++;
                    return new BoolLiteral(t.Text == "true");
                case Kind.Identifier when t.Text == "null":
                    _at++;
                    return new NullLiteral();
                case Kind.Identifier when t.Text == "new":
                    return ParseNew();
                case Kind.Identifier:
                    return ParseCall();
                default:
                    throw new NotSupportedException($"Unexpected '{t.Text}' in the call.");
            }
        }

        private static Node Number(string text, bool negative)
        {
            var sign = negative ? "-" : string.Empty;
            var suffix = char.ToLowerInvariant(text[^1]);
            if (suffix is 'u' or 'l')
            {
                return new IntLiteral(sign + text[..^1], Unsigned: suffix == 'u');
            }

            if (suffix is 'd' or 'f' or 'm') return new DecimalLiteral(sign + text[..^1]);
            return text.Contains('.') ? new DecimalLiteral(sign + text) : new IntLiteral(sign + text, Unsigned: false);
        }

        // new[] { … }   new int[] { … }   new int[0]   new int[0][]   new ListNode[0]   new Codec().Encode(…)
        private Node ParseNew()
        {
            _at++; // new
            if (IsSymbol("["))
            {
                SkipRankSpecifiers();
                return ParseArrayBody();
            }

            var type = Next();
            if (type.Kind != Kind.Identifier) throw new NotSupportedException("Expected a type after 'new'.");

            if (IsSymbol("("))
            {
                Expect("(");
                Expect(")");
                Expect(".");
                var method = Next();
                if (method.Kind != Kind.Identifier) throw new NotSupportedException("Expected a method name.");
                return new NewObjectCall(type.Text, method.Text, ParseArguments());
            }

            if (!IsSymbol("[")) throw new NotSupportedException($"'new {type.Text}' isn't an array or a call.");

            // new T[0], new T[0][]: a size in the first brackets, then more empty ones for a jagged array.
            if (Peek(1).Kind == Kind.Number)
            {
                Expect("[");
                var size = Next();
                if (size.Text != "0") throw new NotSupportedException("Only an empty array can be sized.");
                Expect("]");
                var rank = 1;
                while (IsSymbol("[") && IsSymbol("]", 1))
                {
                    _at += 2;
                    rank++;
                }

                return new EmptyArray(type.Text, rank);
            }

            SkipRankSpecifiers();
            return ParseArrayBody();
        }

        private void SkipRankSpecifiers()
        {
            // [] or [][] before the braces
            while (IsSymbol("[") && IsSymbol("]", 1)) _at += 2;
        }

        private Node ParseArrayBody()
        {
            Expect("{");
            var items = new List<Node>();
            while (!IsSymbol("}"))
            {
                items.Add(ParseExpression());
                if (IsSymbol(",")) _at++;
                else if (!IsSymbol("}")) throw new NotSupportedException("Expected ',' or '}' in an array.");
            }

            Expect("}");
            return new ArrayLiteral(items);
        }

        // sol.Method(…)   Helper(…)   Array.Empty<int>()   a.b.c(…)
        private Node ParseCall()
        {
            var parts = new List<string> { Next().Text };
            while (IsSymbol(".") && Peek(1).Kind == Kind.Identifier)
            {
                _at++;
                parts.Add(Next().Text);
            }

            if (IsSymbol("<"))
            {
                // Array.Empty<int>()
                _at++;
                var element = Next().Text;
                var extra = 0;
                while (IsSymbol("[") && IsSymbol("]", 1))
                {
                    _at += 2;
                    extra++;
                }

                Expect(">");
                Expect("(");
                Expect(")");
                if (parts is ["Array", "Empty"]) return new EmptyArray(element, 1 + extra);
                throw new NotSupportedException($"'{string.Join('.', parts)}<{element}>' isn't a call the translation reads.");
            }

            if (parts is [var type, "MaxValue" or "MinValue"] && !IsSymbol("(")) return new MemberAccess(type, parts[1]);
            if (!IsSymbol("(")) throw new NotSupportedException($"'{string.Join('.', parts)}' isn't a call.");
            var arguments = ParseArguments();
            return parts.Count == 1
                ? new Call(null, parts[0], arguments)
                : new Call(string.Join('.', parts.Take(parts.Count - 1)), parts[^1], arguments);
        }

        private List<Node> ParseArguments()
        {
            Expect("(");
            var arguments = new List<Node>();
            while (!IsSymbol(")"))
            {
                arguments.Add(ParseExpression());
                if (IsSymbol(",")) _at++;
                else if (!IsSymbol(")")) throw new NotSupportedException("Expected ',' or ')' in the arguments.");
            }

            Expect(")");
            return arguments;
        }
    }

    // ── Emitter ────────────────────────────────────────────────────────────

    private sealed class Emitter(RustMethod? method, int problemNumber)
    {
        /// <param name="expected">The Rust type the value is used as, when the context knows it.</param>
        public string Emit(Node node, string? expected)
        {
            switch (node)
            {
                case IntLiteral integer:
                    if (expected is "f64" or "f32") return integer.Text + ".0";
                    return integer.Unsigned ? integer.Text + (expected == "u64" ? "u64" : "u32") : integer.Text;
                case DecimalLiteral number:
                    return number.Text.Contains('.') ? number.Text : number.Text + ".0";
                case StringLiteral text:
                    return RustValueLiterals.StringLiteral(text.Value) + ".to_string()";
                case CharLiteral character:
                    return "'" + (character.Value switch { '\'' => "\\'", '\\' => "\\\\", '\n' => "\\n", '\t' => "\\t", '\r' => "\\r", '\0' => "\\0", var c => c.ToString() }) + "'";
                case BoolLiteral boolean:
                    return boolean.Value ? "true" : "false";
                case NullLiteral:
                    return "None";
                case ArrayLiteral array:
                    return "vec![" + string.Join(", ", array.Items.Select(item => Emit(item, ElementOf(expected)))) + "]";
                case EmptyArray empty:
                    return $"Vec::<{ElementType(empty)}>::new()";
                case MemberAccess limit:
                    return Limit(limit);
                case NewObjectCall creation:
                    return $"{creation.Type}::new().{RustProblemSignature.SnakeCase(creation.Method)}({string.Join(", ", creation.Arguments.Select(a => Emit(a, null)))})";
                case Call call:
                    return EmitCall(call);
                default:
                    throw new NotSupportedException("The call has something the Rust translation doesn't read.");
            }
        }

        // int.MaxValue is i32::MAX; only the number types have limits.
        private static string Limit(MemberAccess limit)
        {
            var type = RustProblemSignature.MapType(limit.Type);
            if (type is not ("i8" or "i16" or "i32" or "i64" or "u8" or "u16" or "u32" or "u64" or "f64"))
            {
                throw new NotSupportedException($"'{limit.Type}.{limit.Member}' isn't a limit the Rust translation knows.");
            }

            return $"{type}::{(limit.Member == "MaxValue" ? "MAX" : "MIN")}";
        }

        private static string? ElementOf(string? type)
        {
            if (type == null) return null;
            type = type.Replace("&mut ", string.Empty);
            return type.StartsWith("Vec<", StringComparison.Ordinal) && type.EndsWith('>') ? type[4..^1] : null;
        }

        private static string ElementType(EmptyArray empty)
        {
            var type = RustProblemSignature.MapType(empty.ElementType);
            for (var i = 1; i < empty.Rank; i++) type = $"Vec<{type}>";
            return type;
        }

        private string EmitCall(Call call)
        {
            // sol.TwoSum(…) is the problem's own method; codec.serialize(…) a method of the design class the test set up.
            if (call.Receiver == "sol")
            {
                var name = RustProblemSignature.SnakeCase(call.Method);
                var arguments = call.Arguments.Select((a, i) => Emit(a, ParameterType(i)));
                return $"Solution::{name}({string.Join(", ", arguments)})";
            }

            if (call.Receiver == "codec")
            {
                return $"codec.{RustProblemSignature.SnakeCase(call.Method)}({string.Join(", ", call.Arguments.Select(a => Emit(a, null)))})";
            }

            if (call.Receiver != null) throw new NotSupportedException($"'{call.Receiver}.{call.Method}' isn't a call the Rust translation reads.");

            switch (call.Method)
            {
                case "BuildList":
                    return "build_list(&[" + string.Join(", ", call.Arguments.Select(a => Emit(a, "i32"))) + "])";
                case "BuildCycle":
                    // BuildCycle([3, 2, 0, -4], 1): a list whose tail points back to the node at index 1.
                    return "build_cycle(&[" + string.Join(", ", ((ArrayLiteral)call.Arguments[0]).Items.Select(a => Emit(a, "i32"))) + "], " + Emit(call.Arguments[1], "i32") + ")";
                case "BuildTree":
                    // BuildTree(3, 9, null, 15): each value is Some(n) and each null is None.
                    return "build_tree(&[" + string.Join(", ", call.Arguments.Select(a => a is NullLiteral ? "None" : $"Some({Emit(a, "i32")})")) + "])";
                case "Board" or "Grid":
                    return "board(&[" + string.Join(", ", call.Arguments.Select(RowLiteral)) + "])";
                case "Reordered":
                    return "reordered(&[" + string.Join(", ", call.Arguments.Select(a => Emit(a, "i32"))) + "])";
                case "BuildGraph":
                    return "build_graph(" + string.Join(", ", call.Arguments.Select(a => Emit(a, "Vec<Vec<i32>>"))) + ")";
                case "LcaValue":
                    return "lca_value(" + string.Join(", ", call.Arguments.Select(a => Emit(a, "i32"))) + ")";
                case "Rotated" or "Zeroed":
                    return $"{call.Method.ToLowerInvariant()}(" + string.Join(", ", call.Arguments.Select(a => Emit(a, "Vec<Vec<i32>>"))) + ")";
                case "IsDeepCopy" or "IsConsistent" or "Replay":
                    return $"{RustProblemSignature.SnakeCase(call.Method)}(" + string.Join(", ", call.Arguments.Select(a => Emit(a, null))) + ")";
                case "RoundTrip":
                    // Encode and Decode Strings takes its strings as parameters; Serialize and Deserialize takes a tree.
                    return problemNumber == 271
                        ? "round_trip(vec![" + string.Join(", ", call.Arguments.Select(a => Emit(a, null))) + "])"
                        : "round_trip(" + string.Join(", ", call.Arguments.Select(a => Emit(a, null))) + ")";
                default:
                    throw new NotSupportedException($"'{call.Method}' isn't a helper the Rust translation knows.");
            }
        }

        // A row of a Board or Grid is a bare &str.
        private string RowLiteral(Node node) => node is StringLiteral text
            ? RustValueLiterals.StringLiteral(text.Value)
            : throw new NotSupportedException("A board row has to be a string.");

        private string? ParameterType(int index)
        {
            if (method == null || index >= method.Parameters.Count) return null;
            return method.Parameters[index].Type.Replace("&mut ", string.Empty);
        }
    }
}
