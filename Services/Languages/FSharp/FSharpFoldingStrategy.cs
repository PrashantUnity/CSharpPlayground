using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;

/// <summary>
/// Folding strategy for F#: collapses indent-based offside blocks (let, type, module, match, if, for, while, etc.),
/// block comments (* ... *), and multiline triple-quoted strings (""" ... """).
/// </summary>
public sealed class FSharpFoldingStrategy : ILanguageFolding
{
    private static readonly string[] BlockKeywords =
    [
        "let ", "let! ", "type ", "module ", "namespace ",
        "if ", "elif ", "else", "match ", "with",
        "for ", "while ", "try", "finally", "begin"
    ];

    public IEnumerable<NewFolding> CreateFoldings(TextDocument document, out int firstErrorOffset)
    {
        firstErrorOffset = -1;
        var foldings = new List<NewFolding>();
        var text = document.Text;
        if (string.IsNullOrEmpty(text)) return foldings;

        var lines = new List<LineInfo>();
        int totalLines = document.LineCount;
        for (int lineNumber = 1; lineNumber <= totalLines; lineNumber++)
        {
            var docLine = document.GetLineByNumber(lineNumber);
            var lineText = document.GetText(docLine);
            var trimmed = lineText.TrimEnd('\r', '\n');
            int indent = 0;
            foreach (char ch in trimmed)
            {
                if (ch == ' ') indent++;
                else if (ch == '\t') indent += 4;
                else break;
            }
            lines.Add(new LineInfo(lineNumber, indent, docLine.Offset, docLine.EndOffset, trimmed.TrimStart()));
        }

        AddDelimitedFoldings(text, document, "(*", "*)", foldings, "(* ... *)");
        AddDelimitedFoldings(text, document, "\"\"\"", "\"\"\"", foldings, "\"\"\"...\"\"\"");

        var stack = new Stack<(int indent, int lineIdx)>();

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];

            while (stack.Count > 0 && line.Indent <= stack.Peek().indent && !string.IsNullOrWhiteSpace(line.Content))
            {
                var (_, startIdx) = stack.Pop();
                int endIdx = i - 1;
                while (endIdx > startIdx && string.IsNullOrWhiteSpace(lines[endIdx].Content))
                    endIdx--;
                if (endIdx > startIdx)
                {
                    int startOffset = document.GetLineByNumber(lines[startIdx].LineNumber).EndOffset;
                    int endOffset = document.GetLineByNumber(lines[endIdx].LineNumber).EndOffset;
                    foldings.Add(new NewFolding(startOffset, endOffset) { Name = "..." });
                }
            }

            if (IsBlockHeader(line.Content))
                stack.Push((line.Indent, i));
        }

        while (stack.Count > 0)
        {
            var (_, startIdx) = stack.Pop();
            int endIdx = lines.Count - 1;
            while (endIdx > startIdx && string.IsNullOrWhiteSpace(lines[endIdx].Content))
                endIdx--;
            if (endIdx > startIdx)
            {
                int startOffset = document.GetLineByNumber(lines[startIdx].LineNumber).EndOffset;
                int endOffset = document.GetLineByNumber(lines[endIdx].LineNumber).EndOffset;
                foldings.Add(new NewFolding(startOffset, endOffset) { Name = "..." });
            }
        }

        foldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        return foldings;
    }

    private static bool IsBlockHeader(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return false;
        foreach (var kw in BlockKeywords)
        {
            if (content.StartsWith(kw, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static void AddDelimitedFoldings(
        string text,
        TextDocument document,
        string startDelim,
        string endDelim,
        List<NewFolding> foldings,
        string foldName)
    {
        int pos = 0;
        while (pos < text.Length)
        {
            int start = text.IndexOf(startDelim, pos, StringComparison.Ordinal);
            if (start < 0) break;
            int end = text.IndexOf(endDelim, start + startDelim.Length, StringComparison.Ordinal);
            if (end < 0) break;
            end += endDelim.Length;

            int startLine = document.GetLineByOffset(start).LineNumber;
            int endLine = document.GetLineByOffset(end - 1).LineNumber;
            if (startLine < endLine)
                foldings.Add(new NewFolding(start, end) { Name = foldName });

            pos = end;
        }
    }

    private readonly record struct LineInfo(int LineNumber, int Indent, int StartOffset, int EndOffset, string Content);
}
