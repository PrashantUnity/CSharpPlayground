using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;

/// <summary>
/// C# scripts can't hold a <c>using var x = …;</c> declaration at the top of a cell (it reads as a using directive), but
/// a notebook cell is where people write them. The declaration is made a plain one, and what it declared is handed to
/// <see cref="CellDisposables"/>, which disposes it when the cell has run (last first, also when the cell fails), as a
/// using declaration would. A last expression without a semicolon stays the cell's result. Line numbers stay as they
/// were: the registration goes on the declaration's own line, after it.
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
        foreach (var declaration in declarations.AsEnumerable().Reverse())
        {
            // After the declaration, on its line: each variable is tracked by the cell's scope.
            var tracking = string.Concat(declaration.Declaration.Variables.Select(v =>
                $" global::PdfEditorApp.Plugins.CSharpEditor.Services.Kernels.CellDisposables.Track(@{v.Identifier.ValueText});"));
            text.Insert(declaration.Span.End, tracking);

            // The two keywords become spaces, so nothing after them on the line moves.
            Blank(text, declaration.UsingKeyword);
            if (declaration.AwaitKeyword != default) Blank(text, declaration.AwaitKeyword);
        }

        return text.ToString();
    }

    private static void Blank(StringBuilder text, SyntaxToken token)
    {
        for (var i = token.SpanStart; i < token.Span.End; i++) text[i] = ' ';
    }
}
