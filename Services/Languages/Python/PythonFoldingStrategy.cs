using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Python;

/// <summary>
/// Folding strategy for Python: collapses indent-based blocks (def, class, if, for, while, with, try, elif, else,
/// except, finally, async def/for/with) and triple-quoted strings (""" / ''').
/// A block starts at a line ending with ':' and spans all following lines that are strictly more indented.
/// </summary>
public sealed class PythonFoldingStrategy : ILanguageFolding
{
    // Headers that introduce an indented block.
    private static readonly string[] BlockKeywords =
    [
        "def ", "async def ", "class ",
        "if ", "elif ", "else:", "else :",
        "for ", "async for ",
        "while ",
        "with ", "async with ",
        "try:", "try :", "except", "except:",
        "finally:", "finally :"
    ];

    public IEnumerable<NewFolding> CreateFoldings(TextDocument document, out int firstErrorOffset)
    {
        firstErrorOffset = -1;
        var foldings = new List<NewFolding>();
        var text = document.Text;
        if (string.IsNullOrEmpty(text)) return foldings;

        // Build a line list: (lineNumber 1-based, indentLevel, lineStartOffset, lineEndOffset, trimmedContent)
        // Use GetLineByNumber instead of document.Lines to avoid DocumentLineTree.GetEnumerator()
        // which requires the Avalonia UI thread.
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

        // --- Triple-quoted string folding ---
        AddTripleQuotedFoldings(text, document, foldings);

        // --- Indent-based block folding ---
        // Stack of (indentLevel, headerLineIndex)
        var stack = new Stack<(int indent, int lineIdx)>();

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];

            // Pop any blocks that are no longer more indented
            while (stack.Count > 0 && line.Indent <= stack.Peek().indent && !string.IsNullOrWhiteSpace(line.Content))
            {
                var (_, startIdx) = stack.Pop();
                // The fold ends at the last line of the block (i - 1)
                int endIdx = i - 1;
                while (endIdx > startIdx && string.IsNullOrWhiteSpace(lines[endIdx].Content))
                    endIdx--; // skip trailing blank lines
                if (endIdx > startIdx)
                {
                    int startOffset = document.GetLineByNumber(lines[startIdx].LineNumber).EndOffset;
                    int endOffset   = document.GetLineByNumber(lines[endIdx].LineNumber).EndOffset;
                    foldings.Add(new NewFolding(startOffset, endOffset) { Name = "..." });
                }
            }

            // Does this line start a block?
            if (IsBlockHeader(line.Content))
                stack.Push((line.Indent, i));
        }

        // Close any remaining open blocks at EOF
        while (stack.Count > 0)
        {
            var (_, startIdx) = stack.Pop();
            int endIdx = lines.Count - 1;
            while (endIdx > startIdx && string.IsNullOrWhiteSpace(lines[endIdx].Content))
                endIdx--;
            if (endIdx > startIdx)
            {
                int startOffset = document.GetLineByNumber(lines[startIdx].LineNumber).EndOffset;
                int endOffset   = document.GetLineByNumber(lines[endIdx].LineNumber).EndOffset;
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
            if (content.StartsWith(kw, StringComparison.Ordinal))
                return true;
        }
        // Bare "else:" / "try:" / "finally:" / "except:" / "except Exception..."
        if (content == "else:" || content == "try:" || content == "finally:" || content.StartsWith("except", StringComparison.Ordinal))
            return true;
        return false;
    }

    private static void AddTripleQuotedFoldings(string text, TextDocument document, List<NewFolding> foldings)
    {
        // Delimiters to search for
        string[] delimiters = ["\"\"\"", "'''"];
        foreach (var delimiter in delimiters)
        {
            int pos = 0;
            while (pos < text.Length)
            {
                int start = text.IndexOf(delimiter, pos, StringComparison.Ordinal);
                if (start < 0) break;
                int end = text.IndexOf(delimiter, start + delimiter.Length, StringComparison.Ordinal);
                if (end < 0) break;
                end += delimiter.Length;

                int startLine = document.GetLineByOffset(start).LineNumber;
                int endLine   = document.GetLineByOffset(end - 1).LineNumber;
                if (startLine < endLine)
                    foldings.Add(new NewFolding(start, end) { Name = delimiter + "..." + delimiter });

                pos = end;
            }
        }
    }

    private readonly record struct LineInfo(int LineNumber, int Indent, int StartOffset, int EndOffset, string Content);
}
