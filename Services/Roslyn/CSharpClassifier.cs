using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;

/// <summary>
/// What a piece of C# is, for colouring: the roles VS Code gives C# with semantic highlighting on (Dark+ and Light+).
/// Each role is a named colour of the C# highlighting definition (<see cref="CSharpSyntaxHighlightingTheme"/>).
/// </summary>
public enum CSharpRole
{
    Comment,
    ExcludedCode,
    String,
    StringEscape,
    Number,
    Keyword,
    ControlKeyword,
    Preprocessor,
    Type,
    Namespace,
    Method,
    Variable,
    EnumMember,
    Punctuation,
}

/// <summary>A run of C# text and its role.</summary>
public readonly record struct CSharpClassifiedSpan(int Start, int Length, CSharpRole Role)
{
    public int End => Start + Length;
}

/// <summary>
/// The names a file declares, by kind. With them an identifier can be told apart without compiling: <c>Items.Count</c>
/// is a property when the file declares <c>Items</c>, while an undeclared <c>Console.WriteLine</c> is a type's member.
/// </summary>
public sealed class CSharpDeclarations
{
    public static readonly CSharpDeclarations Empty = new();

    public HashSet<string> Types { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Variables { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Methods { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Constants { get; } = new(StringComparer.Ordinal);
    public HashSet<string> EnumTypes { get; } = new(StringComparer.Ordinal);

    public static CSharpDeclarations Collect(SyntaxNode root)
    {
        var names = new CSharpDeclarations();
        foreach (var node in root.DescendantNodes()) names.Add(node);
        return names;
    }

    /// <summary>
    /// These names plus the ones declared in the parts of <paramref name="root"/> an edit changed: the work follows the edit,
    /// not the file. A name the edit removed stays until the next full <see cref="Collect"/> (it can only colour a word
    /// that has the same name).
    /// </summary>
    public CSharpDeclarations With(SyntaxNode root, IEnumerable<TextSpan> changed)
    {
        var names = new CSharpDeclarations();
        names.Types.UnionWith(Types);
        names.Variables.UnionWith(Variables);
        names.Methods.UnionWith(Methods);
        names.Constants.UnionWith(Constants);
        names.EnumTypes.UnionWith(EnumTypes);
        foreach (var span in changed)
        {
            foreach (var node in root.DescendantNodes(span)) names.Add(node);
        }

        return names;
    }

    private void Add(SyntaxNode node)
    {
        switch (node)
        {
            case EnumDeclarationSyntax e:
                Types.Add(e.Identifier.ValueText);
                EnumTypes.Add(e.Identifier.ValueText);
                break;
            case BaseTypeDeclarationSyntax t:
                Types.Add(t.Identifier.ValueText);
                break;
            case DelegateDeclarationSyntax d:
                Types.Add(d.Identifier.ValueText);
                break;
            case TypeParameterSyntax p:
                Types.Add(p.Identifier.ValueText);
                break;
            case MethodDeclarationSyntax m:
                Methods.Add(m.Identifier.ValueText);
                break;
            case LocalFunctionStatementSyntax f:
                Methods.Add(f.Identifier.ValueText);
                break;
            case VariableDeclaratorSyntax v:
                if (IsConstant(v)) Constants.Add(v.Identifier.ValueText);
                else Variables.Add(v.Identifier.ValueText);
                break;
            case ParameterSyntax p:
                Variables.Add(p.Identifier.ValueText);
                break;
            case PropertyDeclarationSyntax p:
                Variables.Add(p.Identifier.ValueText);
                break;
            case EventDeclarationSyntax e:
                Variables.Add(e.Identifier.ValueText);
                break;
            case SingleVariableDesignationSyntax s:
                Variables.Add(s.Identifier.ValueText);
                break;
            case ForEachStatementSyntax f:
                Variables.Add(f.Identifier.ValueText);
                break;
            case CatchDeclarationSyntax c when c.Identifier.RawKind != 0:
                Variables.Add(c.Identifier.ValueText);
                break;
            case QueryClauseSyntax or FromClauseSyntax or QueryContinuationSyntax:
                var range = node switch
                {
                    FromClauseSyntax from => from.Identifier,
                    LetClauseSyntax let => let.Identifier,
                    JoinClauseSyntax join => join.Identifier,
                    JoinIntoClauseSyntax into => into.Identifier,
                    QueryContinuationSyntax continuation => continuation.Identifier,
                    _ => default,
                };
                if (range.RawKind != 0) Variables.Add(range.ValueText);
                break;
        }
    }

    internal static bool IsConstant(VariableDeclaratorSyntax declarator) =>
        declarator.Parent?.Parent switch
        {
            FieldDeclarationSyntax field => field.Modifiers.Any(SyntaxKind.ConstKeyword),
            LocalDeclarationStatementSyntax local => local.IsConst,
            _ => false,
        };
}

/// <summary>
/// Colours C# from Roslyn's syntax tree: every token gets the role its place in the code gives it (a type where a type
/// goes, a method where something is called, a property after a dot), the way VS Code colours C# with the C# extension.
/// Works on the syntax alone, so it needs no compilation and costs one walk over the tokens of the lines asked for.
/// </summary>
public static class CSharpClassifier
{
    /// <summary>Scripts and notebooks: top-level statements, a trailing expression, <c>#r</c>, every new feature.</summary>
    public static readonly CSharpParseOptions ParseOptions =
        CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview).WithKind(SourceCodeKind.Script);

    public static SyntaxTree Parse(SourceText text) => CSharpSyntaxTree.ParseText(text, ParseOptions);

    public static SyntaxTree Parse(string text) => Parse(SourceText.From(text));

    /// <summary>The roles in <paramref name="span"/>, in order, clipped to it, never overlapping.</summary>
    public static List<CSharpClassifiedSpan> Classify(SyntaxTree tree, TextSpan span, CSharpDeclarations? names = null)
    {
        var spans = new List<CSharpClassifiedSpan>();
        Classify(tree.GetRoot(), span, names ?? CSharpDeclarations.Empty, spans);
        return spans;
    }

    public static void Classify(SyntaxNode root, TextSpan span, CSharpDeclarations names, List<CSharpClassifiedSpan> into)
    {
        if (span.Length == 0 || root.FullSpan.Length == 0) return;
        var start = Math.Min(span.Start, root.FullSpan.End - 1);
        var token = root.FindToken(Math.Max(0, start));
        while (token.RawKind != 0 && token.FullSpan.Start < span.End)
        {
            if (token.HasLeadingTrivia) ClassifyTrivia(token.LeadingTrivia, span, names, into);
            if (token.Span.Length > 0 && token.Span.End > span.Start) ClassifyToken(token, span, names, into);
            if (token.HasTrailingTrivia) ClassifyTrivia(token.TrailingTrivia, span, names, into);
            // Zero-width tokens too: the end of the file carries the last comments and directives (#endregion).
            token = token.GetNextToken(includeZeroWidth: true, includeSkipped: true);
        }
    }

    private static void Add(List<CSharpClassifiedSpan> into, TextSpan clip, int start, int end, CSharpRole role)
    {
        start = Math.Max(start, clip.Start);
        end = Math.Min(end, clip.End);
        if (end <= start) return;
        if (into.Count > 0 && into[^1].End > start)
        {
            // Never overlap (a directive's own tokens are walked after its trivia span).
            start = into[^1].End;
            if (end <= start) return;
        }

        // One run for touching pieces of the same role (#region, a string and its closing quote).
        if (into.Count > 0 && into[^1].End == start && into[^1].Role == role)
        {
            into[^1] = into[^1] with { Length = end - into[^1].Start };
            return;
        }

        into.Add(new CSharpClassifiedSpan(start, end - start, role));
    }

    private static void ClassifyTrivia(SyntaxTriviaList trivia, TextSpan clip, CSharpDeclarations names, List<CSharpClassifiedSpan> into)
    {
        foreach (var item in trivia)
        {
            if (item.Span.End <= clip.Start) continue;
            if (item.Span.Start >= clip.End) return;
            switch (item.Kind())
            {
                case SyntaxKind.SingleLineCommentTrivia:
                case SyntaxKind.MultiLineCommentTrivia:
                case SyntaxKind.SingleLineDocumentationCommentTrivia:
                case SyntaxKind.MultiLineDocumentationCommentTrivia:
                    Add(into, clip, item.Span.Start, item.Span.End, CSharpRole.Comment);
                    break;
                case SyntaxKind.DisabledTextTrivia:
                    Add(into, clip, item.Span.Start, item.Span.End, CSharpRole.ExcludedCode);
                    break;
                default:
                    if (item.IsDirective && item.GetStructure() is DirectiveTriviaSyntax directive) ClassifyDirective(directive, clip, into);
                    break;
            }
        }
    }

    // #region, #if DEBUG, #r "nuget: X": the directive in the preprocessor colour, its strings and numbers as such, its
    // comment as a comment, and a region's name as plain text.
    private static void ClassifyDirective(DirectiveTriviaSyntax directive, TextSpan clip, List<CSharpClassifiedSpan> into)
    {
        foreach (var token in directive.DescendantTokens(descendIntoTrivia: false))
        {
            foreach (var t in token.LeadingTrivia.Concat(token.TrailingTrivia))
            {
                if (t.IsKind(SyntaxKind.SingleLineCommentTrivia) || t.IsKind(SyntaxKind.MultiLineCommentTrivia))
                {
                    Add(into, clip, t.Span.Start, t.Span.End, CSharpRole.Comment);
                }
            }

            if (token.Span.Length == 0) continue;
            var role = token.Kind() switch
            {
                SyntaxKind.StringLiteralToken => CSharpRole.String,
                SyntaxKind.NumericLiteralToken => CSharpRole.Number,
                SyntaxKind.EndOfDirectiveToken => (CSharpRole?)null,
                _ => CSharpRole.Preprocessor,
            };
            if (role is { } r) Add(into, clip, token.Span.Start, token.Span.End, r);
        }
    }

    private static void ClassifyToken(SyntaxToken token, TextSpan clip, CSharpDeclarations names, List<CSharpClassifiedSpan> into)
    {
        var kind = token.Kind();
        switch (kind)
        {
            case SyntaxKind.IdentifierToken:
                if (ClassifyIdentifier(token, names) is { } role) Add(into, clip, token.Span.Start, token.Span.End, role);
                return;
            case SyntaxKind.NumericLiteralToken:
                Add(into, clip, token.Span.Start, token.Span.End, CSharpRole.Number);
                return;
            case SyntaxKind.StringLiteralToken:
            case SyntaxKind.Utf8StringLiteralToken:
            case SyntaxKind.CharacterLiteralToken:
                AddString(into, clip, token, escapes: token.Text.Length > 0 && token.Text[0] != '@');
                return;
            case SyntaxKind.InterpolatedStringTextToken:
                AddString(into, clip, token, escapes: token.Parent?.Parent is InterpolatedStringExpressionSyntax s
                    && s.StringStartToken.IsKind(SyntaxKind.InterpolatedStringStartToken));
                return;
            case SyntaxKind.SingleLineRawStringLiteralToken:
            case SyntaxKind.MultiLineRawStringLiteralToken:
            case SyntaxKind.Utf8SingleLineRawStringLiteralToken:
            case SyntaxKind.Utf8MultiLineRawStringLiteralToken:
            case SyntaxKind.InterpolatedStringStartToken:
            case SyntaxKind.InterpolatedVerbatimStringStartToken:
            case SyntaxKind.InterpolatedSingleLineRawStringStartToken:
            case SyntaxKind.InterpolatedMultiLineRawStringStartToken:
            case SyntaxKind.InterpolatedStringEndToken:
            case SyntaxKind.InterpolatedRawStringEndToken:
                Add(into, clip, token.Span.Start, token.Span.End, CSharpRole.String);
                return;
        }

        // The braces and format of an interpolation belong to the string ($"{x:N2}"); the expression inside is code.
        if (token.Parent is InterpolationSyntax && (kind is SyntaxKind.OpenBraceToken or SyntaxKind.CloseBraceToken)
            || token.Parent is InterpolationFormatClauseSyntax)
        {
            Add(into, clip, token.Span.Start, token.Span.End, CSharpRole.String);
            return;
        }

        if (SyntaxFacts.IsKeywordKind(kind))
        {
            Add(into, clip, token.Span.Start, token.Span.End, IsControlKeyword(token) ? CSharpRole.ControlKeyword : CSharpRole.Keyword);
            return;
        }

        if (SyntaxFacts.IsPunctuation(kind) || SyntaxFacts.IsAnyOverloadableOperator(kind))
        {
            Add(into, clip, token.Span.Start, token.Span.End, CSharpRole.Punctuation);
        }
    }

    // A string, with its escape sequences (\n, A, and {{ in an interpolated string) in their own colour.
    private static void AddString(List<CSharpClassifiedSpan> into, TextSpan clip, SyntaxToken token, bool escapes)
    {
        var text = token.Text;
        var start = token.Span.Start;
        if (!escapes && !token.IsKind(SyntaxKind.InterpolatedStringTextToken))
        {
            Add(into, clip, start, token.Span.End, CSharpRole.String);
            return;
        }

        var run = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var length = escapes ? EscapeLength(text, i) : 0;
            if (length == 0 && token.IsKind(SyntaxKind.InterpolatedStringTextToken) && i + 1 < text.Length
                && (text[i] == '{' && text[i + 1] == '{' || text[i] == '}' && text[i + 1] == '}'))
            {
                length = 2;
            }

            if (length == 0) continue;
            Add(into, clip, start + run, start + i, CSharpRole.String);
            Add(into, clip, start + i, start + i + length, CSharpRole.StringEscape);
            i += length - 1;
            run = i + 1;
        }

        Add(into, clip, start + run, token.Span.End, CSharpRole.String);
    }

    private static int EscapeLength(string text, int i)
    {
        if (text[i] != '\\' || i + 1 >= text.Length) return 0;
        int Hex(int from, int max)
        {
            var n = 0;
            while (n < max && from + n < text.Length && Uri.IsHexDigit(text[from + n])) n++;
            return n;
        }

        return text[i + 1] switch
        {
            'x' => Hex(i + 2, 4) is > 0 and var n ? 2 + n : 0,
            'u' => Hex(i + 2, 4) == 4 ? 6 : 0,
            'U' => Hex(i + 2, 8) == 8 ? 10 : 0,
            '\\' or '\'' or '"' or '0' or 'a' or 'b' or 'e' or 'f' or 'n' or 'r' or 't' or 'v' => 2,
            _ => 0,
        };
    }

    // Roslyn's own rule: the keywords of statements that change the flow (if, for, return, throw, ...) are "control".
    private static bool IsControlKeyword(SyntaxToken token)
    {
        switch (token.Kind())
        {
            case SyntaxKind.IfKeyword:
            case SyntaxKind.ElseKeyword:
            case SyntaxKind.WhileKeyword:
            case SyntaxKind.ForKeyword:
            case SyntaxKind.ForEachKeyword:
            case SyntaxKind.DoKeyword:
            case SyntaxKind.SwitchKeyword:
            case SyntaxKind.CaseKeyword:
            case SyntaxKind.TryKeyword:
            case SyntaxKind.CatchKeyword:
            case SyntaxKind.FinallyKeyword:
            case SyntaxKind.GotoKeyword:
            case SyntaxKind.BreakKeyword:
            case SyntaxKind.ContinueKeyword:
            case SyntaxKind.ReturnKeyword:
            case SyntaxKind.ThrowKeyword:
            case SyntaxKind.YieldKeyword:
            case SyntaxKind.DefaultKeyword:
                break;
            default:
                return false;
        }

        return token.Parent?.Kind() switch
        {
            SyntaxKind.IfStatement or SyntaxKind.ElseClause or SyntaxKind.WhileStatement or SyntaxKind.DoStatement
                or SyntaxKind.ForStatement or SyntaxKind.ForEachStatement or SyntaxKind.ForEachVariableStatement
                or SyntaxKind.SwitchStatement or SyntaxKind.SwitchExpression or SyntaxKind.CaseSwitchLabel
                or SyntaxKind.CasePatternSwitchLabel or SyntaxKind.DefaultSwitchLabel or SyntaxKind.TryStatement
                or SyntaxKind.CatchClause or SyntaxKind.FinallyClause or SyntaxKind.GotoStatement
                or SyntaxKind.GotoCaseStatement or SyntaxKind.GotoDefaultStatement or SyntaxKind.BreakStatement
                or SyntaxKind.ContinueStatement or SyntaxKind.ReturnStatement or SyntaxKind.YieldReturnStatement
                or SyntaxKind.YieldBreakStatement or SyntaxKind.ThrowStatement or SyntaxKind.ThrowExpression => true,
            _ => false,
        };
    }

    private static CSharpRole? ClassifyIdentifier(SyntaxToken token, CSharpDeclarations names)
    {
        switch (token.Parent)
        {
            case BaseTypeDeclarationSyntax:
            case DelegateDeclarationSyntax:
            case TypeParameterSyntax:
            case ConstructorDeclarationSyntax:
            case DestructorDeclarationSyntax:
                return CSharpRole.Type;
            case MethodDeclarationSyntax:
            case LocalFunctionStatementSyntax:
                return CSharpRole.Method;
            case EnumMemberDeclarationSyntax:
                return CSharpRole.EnumMember;
            case VariableDeclaratorSyntax declarator:
                return CSharpDeclarations.IsConstant(declarator) ? CSharpRole.EnumMember : CSharpRole.Variable;
            case PropertyDeclarationSyntax:
            case EventDeclarationSyntax:
            case ParameterSyntax:
            case SingleVariableDesignationSyntax:
            case ForEachStatementSyntax:
            case CatchDeclarationSyntax:
            case FromClauseSyntax:
            case LetClauseSyntax:
            case JoinClauseSyntax:
            case JoinIntoClauseSyntax:
            case QueryContinuationSyntax:
            case LabeledStatementSyntax:
                return CSharpRole.Variable;
            case ExternAliasDirectiveSyntax:
                return CSharpRole.Namespace;
            case SimpleNameSyntax name:
                return ClassifyName(name, names);
            default:
                return null;
        }
    }

    private static CSharpRole ClassifyName(SimpleNameSyntax name, CSharpDeclarations names)
    {
        var text = name.Identifier.ValueText;

        // Contextual keywords where they act as keywords.
        if (InTypePosition(name, out _))
        {
            if (text is "var" or "dynamic" or "nint" or "nuint" && name is IdentifierNameSyntax && !names.Types.Contains(text)) return CSharpRole.Keyword;
        }

        if (text == "nameof" && name.Parent is InvocationExpressionSyntax nameOf && nameOf.Expression == name) return CSharpRole.Keyword;

        // using System.Collections.Generic; namespace App.Models;
        if (InNamespaceName(name, out var lastOfStaticUsing)) return lastOfStaticUsing ? CSharpRole.Type : CSharpRole.Namespace;

        // Something called: Foo(), x.Foo(), x?.Foo(), Array.Empty<int>(). A local holding a delegate stays a variable.
        ExpressionSyntax called = name.Parent switch
        {
            MemberAccessExpressionSyntax access when access.Name == name => access,
            MemberBindingExpressionSyntax binding when binding.Name == name => binding,
            _ => name,
        };
        if (called.Parent is InvocationExpressionSyntax invocation && invocation.Expression == called)
        {
            return called == name && names.Variables.Contains(text) && !names.Methods.Contains(text) ? CSharpRole.Variable : CSharpRole.Method;
        }

        if (InTypePosition(name, out var qualifierOfType)) return qualifierOfType ? CSharpRole.Namespace : CSharpRole.Type;

        switch (name.Parent)
        {
            // The left of a dot: a variable the file declares, else a type when it is capitalised (Console, Math).
            case MemberAccessExpressionSyntax left when left.Expression == name:
                if (names.Variables.Contains(text)) return CSharpRole.Variable;
                if (names.Types.Contains(text) || char.IsUpper(text[0])) return CSharpRole.Type;
                return CSharpRole.Variable;

            // The right of a dot: a member. An enum's members and constants have their own colour.
            case MemberAccessExpressionSyntax right when right.Name == name:
                if (right.Expression is IdentifierNameSyntax owner && names.EnumTypes.Contains(owner.Identifier.ValueText)) return CSharpRole.EnumMember;
                if (names.Constants.Contains(text)) return CSharpRole.EnumMember;
                if (right.Parent is MemberAccessExpressionSyntax outer && outer.Expression == right && names.Types.Contains(text)) return CSharpRole.Type;
                return CSharpRole.Variable;
        }

        if (names.Constants.Contains(text)) return CSharpRole.EnumMember;
        if (names.Variables.Contains(text)) return CSharpRole.Variable;
        if (names.Types.Contains(text) && name.Parent is not ArgumentSyntax) return CSharpRole.Type;
        if (names.Methods.Contains(text)) return CSharpRole.Method;
        return CSharpRole.Variable;
    }

    // In a using directive's or a namespace declaration's name. The last part of "using static X.Y.Math" is a type.
    private static bool InNamespaceName(SimpleNameSyntax name, out bool typeOfStaticUsing)
    {
        typeOfStaticUsing = false;
        SyntaxNode node = name;
        while (node.Parent is QualifiedNameSyntax or AliasQualifiedNameSyntax) node = node.Parent;
        switch (node.Parent)
        {
            case UsingDirectiveSyntax usingDirective when usingDirective.NamespaceOrType == node:
                typeOfStaticUsing = usingDirective.StaticKeyword.RawKind != 0 && IsRightmost(name);
                return true;
            case BaseNamespaceDeclarationSyntax ns when ns.Name == node:
                return true;
            default:
                return false;
        }
    }

    private static bool IsRightmost(SimpleNameSyntax name) =>
        name.Parent is not QualifiedNameSyntax q || q.Right == name && !(q.Parent is QualifiedNameSyntax outer && outer.Left == q);

    /// <summary>
    /// Whether the name is (part of) a type in a place only a type can go. <paramref name="qualifier"/>: it is the
    /// left of a qualified type name (System in System.Text.StringBuilder), so a namespace or an outer type.
    /// </summary>
    private static bool InTypePosition(SimpleNameSyntax name, out bool qualifier)
    {
        qualifier = name.Parent is QualifiedNameSyntax q && q.Left == name;
        SyntaxNode type = name;
        while (true)
        {
            switch (type.Parent)
            {
                case QualifiedNameSyntax:
                case AliasQualifiedNameSyntax:
                case NullableTypeSyntax:
                case ArrayTypeSyntax:
                case PointerTypeSyntax:
                case RefTypeSyntax:
                case ScopedTypeSyntax:
                case TupleElementSyntax:
                case TupleTypeSyntax:
                    type = type.Parent;
                    continue;
                case TypeArgumentListSyntax:
                    return true;
            }

            break;
        }

        return type.Parent switch
        {
            VariableDeclarationSyntax v => v.Type == type,
            ParameterSyntax p => p.Type == type,
            MethodDeclarationSyntax m => m.ReturnType == type,
            LocalFunctionStatementSyntax f => f.ReturnType == type,
            BasePropertyDeclarationSyntax p => p.Type == type,
            DelegateDeclarationSyntax d => d.ReturnType == type,
            OperatorDeclarationSyntax o => o.ReturnType == type,
            ConversionOperatorDeclarationSyntax c => c.Type == type,
            ObjectCreationExpressionSyntax o => o.Type == type,
            ArrayCreationExpressionSyntax => true,
            StackAllocArrayCreationExpressionSyntax => true,
            TypeOfExpressionSyntax or SizeOfExpressionSyntax or DefaultExpressionSyntax => true,
            CastExpressionSyntax c => c.Type == type,
            BinaryExpressionSyntax b => b.Right == type && (b.IsKind(SyntaxKind.IsExpression) || b.IsKind(SyntaxKind.AsExpression)),
            DeclarationPatternSyntax or TypePatternSyntax or RecursivePatternSyntax => true,
            DeclarationExpressionSyntax d => d.Type == type,
            ForEachStatementSyntax f => f.Type == type,
            CatchDeclarationSyntax => true,
            BaseTypeSyntax => true,
            TypeConstraintSyntax => true,
            AttributeSyntax a => a.Name == type,
            ExplicitInterfaceSpecifierSyntax => true,
            RefValueExpressionSyntax r => r.Type == type,
            FunctionPointerParameterSyntax => true,
            _ => false,
        };
    }
}
