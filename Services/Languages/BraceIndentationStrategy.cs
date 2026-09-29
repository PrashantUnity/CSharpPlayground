using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Indentation;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

/// <summary>
/// Indentation after Enter for languages that nest with brackets: keeps the previous line's indentation, adds a level after a
/// line that ends in <c>{</c>, <c>(</c> or <c>[</c> (ignoring a trailing <c>//</c> comment), and takes one away from a line
/// that starts with <c>}</c>, <c>)</c> or <c>]</c>.
/// </summary>
public sealed class BraceIndentationStrategy(TextEditorOptions options) : IIndentationStrategy
{
    public void IndentLine(TextDocument document, DocumentLine line)
    {
        var previous = line.PreviousLine;
        if (previous == null) return;

        var previousText = document.GetText(previous);
        var indentation = previousText[..(previousText.Length - previousText.TrimStart().Length)];
        var code = WithoutLineComment(previousText).TrimEnd();

        if (code.EndsWith('{') || code.EndsWith('(') || code.EndsWith('['))
        {
            indentation += options.IndentationString;
        }

        var currentText = document.GetText(line);
        var currentTrimmed = currentText.TrimStart();
        if (currentTrimmed.StartsWith('}') || currentTrimmed.StartsWith(')') || currentTrimmed.StartsWith(']'))
        {
            indentation = Outdent(indentation);
        }

        var currentIndentationLength = currentText.Length - currentTrimmed.Length;
        document.Replace(line.Offset, currentIndentationLength, indentation);
    }

    public void IndentLines(TextDocument document, int beginLine, int endLine)
    {
    }

    private string Outdent(string indentation)
    {
        if (indentation.EndsWith('\t')) return indentation[..^1];
        var remove = Math.Min(options.IndentationSize, indentation.Length - indentation.TrimEnd(' ').Length);
        return indentation[..^remove];
    }

    // A // inside a string ("http://…") isn't a comment: only cut where the number of quotes before it is even.
    private static string WithoutLineComment(string text)
    {
        var quotes = 0;
        for (var i = 0; i + 1 < text.Length; i++)
        {
            if (text[i] == '\\') { i++; continue; }
            if (text[i] == '"') quotes++;
            else if (text[i] == '/' && text[i + 1] == '/' && quotes % 2 == 0) return text[..i];
        }

        return text;
    }
}
