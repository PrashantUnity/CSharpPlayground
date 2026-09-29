using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Indentation;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;

/// <summary>
/// SQL indentation strategy: preserves previous indentation, indents after open parentheses,
/// block statements (CREATE TABLE, SELECT, CASE, BEGIN), and outdents before closing parentheses and END.
/// </summary>
public sealed class SqlIndentationStrategy(TextEditorOptions options) : IIndentationStrategy
{
    private static readonly string[] IndentAfterWords =
    [
        "select", "from", "where", "group by", "order by", "having",
        "set", "values", "begin", "case", "when"
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
        var currentTrimmed = currentText.TrimStart();
        if (ShouldOutdent(currentTrimmed) && indentation.Length >= options.IndentationString.Length)
        {
            indentation = indentation[..^options.IndentationString.Length];
        }

        var currentIndentationLength = currentText.Length - currentTrimmed.Length;
        document.Replace(line.Offset, currentIndentationLength, indentation);
    }

    public void IndentLines(TextDocument document, int beginLine, int endLine)
    {
        for (var i = beginLine; i <= endLine; i++)
        {
            IndentLine(document, document.GetLineByNumber(i));
        }
    }

    private static bool ShouldIndent(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;

        if (code.EndsWith('(') || code.EndsWith('{') || code.EndsWith('['))
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

    private static bool ShouldOutdent(string currentLine)
    {
        if (string.IsNullOrWhiteSpace(currentLine)) return false;
        return currentLine.StartsWith(')') ||
               currentLine.StartsWith('}') ||
               currentLine.StartsWith(']') ||
               currentLine.StartsWith("END", StringComparison.OrdinalIgnoreCase);
    }

    private static string WithoutComment(string line)
    {
        var dash = line.IndexOf("--", StringComparison.Ordinal);
        return dash >= 0 ? line[..dash] : line;
    }
}
