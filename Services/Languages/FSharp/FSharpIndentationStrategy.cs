using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Indentation;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;

/// <summary>
/// F# indentation strategy honoring the offside rule: keeps previous line's indentation,
/// and increases indentation level after block openers (=, ->, do, then, else, try, with, match ... with, etc.).
/// </summary>
public sealed class FSharpIndentationStrategy(TextEditorOptions options) : IIndentationStrategy
{
    private static readonly string[] IndentAfterWords =
    [
        "do", "then", "else", "try", "with", "begin", "struct", "class"
    ];

    public void IndentLine(TextDocument document, DocumentLine line)
    {
        var previous = line.PreviousLine;
        if (previous == null) return;

        var previousText = document.GetText(previous);
        var indentation = previousText[..(previousText.Length - previousText.TrimStart().Length)];
        var code = WithoutComment(previousText).Trim();

        if (ShouldIndent(code))
        {
            indentation += options.IndentationString;
        }

        var currentText = document.GetText(line);
        var currentIndentationLength = currentText.Length - currentText.TrimStart().Length;
        document.Replace(line.Offset, currentIndentationLength, indentation);
    }

    public void IndentLines(TextDocument document, int beginLine, int endLine)
    {
        // Offside rule: indentation has semantic meaning in F#, so full-block auto-reindent is avoided.
    }

    private static bool ShouldIndent(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;

        if (code.EndsWith('=') || code.EndsWith("->", StringComparison.Ordinal) ||
            code.EndsWith('{') || code.EndsWith('[') || code.EndsWith('(') ||
            code.EndsWith("[|", StringComparison.Ordinal))
        {
            return true;
        }

        if (code.EndsWith(" with", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var word in IndentAfterWords)
        {
            if (code.Equals(word, StringComparison.OrdinalIgnoreCase) ||
                code.EndsWith(" " + word, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string WithoutComment(string text)
    {
        var commentIdx = text.IndexOf("//", StringComparison.Ordinal);
        return commentIdx >= 0 ? text[..commentIdx] : text;
    }
}
