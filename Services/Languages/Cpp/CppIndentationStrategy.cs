using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Indentation;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;

/// <summary>
/// C++ brace and bracket indentation after Enter: preserves indentation from previous line,
/// indents after opening braces '{', '(', '[', and outdents on closing braces '}', ')', ']'.
/// </summary>
public sealed class CppIndentationStrategy(TextEditorOptions options) : IIndentationStrategy
{
    public void IndentLine(TextDocument document, DocumentLine line)
    {
        var previous = line.PreviousLine;
        if (previous == null) return;

        var previousText = document.GetText(previous);
        var indentation = previousText[..(previousText.Length - previousText.TrimStart().Length)];
        var code = WithoutComments(previousText).TrimEnd();

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

    private static string WithoutComments(string text)
    {
        var singleLine = text.IndexOf("//", StringComparison.Ordinal);
        return singleLine >= 0 ? text[..singleLine] : text;
    }
}
