using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;

/// <summary>
/// C# scripts can't hold a <c>using var x = …;</c> declaration at the top of a cell (it reads as a using directive), but
/// a notebook cell is where people write them. The declaration is made a plain one and what it declared is disposed when
/// the cell's statements have run, in reverse order, which is when a using declaration would have disposed it.
/// Line numbers stay as they were: nothing is added before the end of the cell.
/// </summary>
public static class CellUsingDeclarations
{
    private static readonly Regex Candidate = new(@"(?m)^(?:await\s+)?using\s+(?!\()(?!static\b)", RegexOptions.Compiled);

    public static string Rewrite(string code)
    {
        if (!Candidate.IsMatch(code)) return code;

        // As a program's top-level statements, where using declarations are allowed, to find the cell's own.
        var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(kind: SourceCodeKind.Regular, languageVersion: LanguageVersion.Latest));
        var declarations = tree.GetCompilationUnitRoot().Members
            .OfType<GlobalStatementSyntax>()
            .Select(g => g.Statement)
            .OfType<LocalDeclarationStatementSyntax>()
            .Where(d => d.UsingKeyword != default)
            .ToList();
        if (declarations.Count == 0) return code;

        var text = new StringBuilder(code);
        var owned = new List<(string Name, bool Async)>();
        foreach (var declaration in declarations.AsEnumerable().Reverse())
        {
            // The two keywords become spaces, so nothing after them moves.
            Blank(text, declaration.UsingKeyword);
            if (declaration.AwaitKeyword != default) Blank(text, declaration.AwaitKeyword);
            owned.InsertRange(0, declaration.Declaration.Variables.Select(v => (v.Identifier.ValueText, declaration.AwaitKeyword != default)));
        }

        text.AppendLine();
        foreach (var (name, isAsync) in Enumerable.Reverse(owned))
        {
            text.AppendLine(isAsync
                ? $"if (@{name} is System.IAsyncDisposable __fryAsync) await __fryAsync.DisposeAsync(); else (@{name} as System.IDisposable)?.Dispose();"
                : $"(@{name} as System.IDisposable)?.Dispose();");
        }

        return text.ToString();
    }

    private static void Blank(StringBuilder text, SyntaxToken token)
    {
        for (var i = token.SpanStart; i < token.Span.End; i++) text[i] = ' ';
    }
}
