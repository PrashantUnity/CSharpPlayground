using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Indentation;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Python;

/// <summary>
/// Python's indentation after Enter: the new line keeps the previous one's indentation, goes one level in after a line
/// ending with <c>:</c> (<c>def</c>, <c>if</c>, <c>for</c>…), and one level out after <c>return</c>, <c>pass</c>,
/// <c>raise</c>, <c>break</c> or <c>continue</c>, which end a block.
/// </summary>
public sealed class PythonIndentationStrategy(TextEditorOptions options) : IIndentationStrategy
{
    private static readonly string[] BlockEnders = ["return", "pass", "raise", "break", "continue"];

    public void IndentLine(TextDocument document, DocumentLine line)
    {
        var previous = line.PreviousLine;
        if (previous == null) return;

        var previousText = document.GetText(previous);
        var indentation = previousText[..(previousText.Length - previousText.TrimStart().Length)];
        var code = WithoutComment(previousText).Trim();

        if (code.EndsWith(':'))
        {
            indentation += options.IndentationString;
        }
        else if (BlockEnders.Any(word => code == word || code.StartsWith(word + " ", StringComparison.Ordinal) || code.StartsWith(word + "(", StringComparison.Ordinal)))
        {
            indentation = Outdent(indentation);
        }

        var currentText = document.GetText(line);
        var currentIndentationLength = currentText.Length - currentText.TrimStart().Length;
        document.Replace(line.Offset, currentIndentationLength, indentation);
    }

    // Reindenting a selection (IndentLines) is left alone: in Python, indentation is the code's meaning.
    public void IndentLines(TextDocument document, int beginLine, int endLine)
    {
    }

    private string Outdent(string indentation)
    {
        if (indentation.EndsWith('\t')) return indentation[..^1];
        var remove = Math.Min(options.IndentationSize, indentation.Length - indentation.TrimEnd(' ').Length);
        return indentation[..^remove];
    }

    // The code part of a line: a '#' outside quotes starts a comment.
    private static string WithoutComment(string text)
    {
        char? quote = null;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quote != null)
            {
                if (c == '\\') i++;
                else if (c == quote) quote = null;
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c == '#')
            {
                return text[..i];
            }
        }

        return text;
    }
}
