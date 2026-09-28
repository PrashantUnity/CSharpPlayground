using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;

/// <summary>
/// Folding strategy for C++: collapses curly-brace blocks ({ ... }), multiline block comments (/* ... */),
/// and #pragma region ... #pragma endregion blocks.
/// Skips brace and comment detection inside string literals and line comments.
/// </summary>
public sealed class CppFoldingStrategy : ILanguageFolding
{
    public IEnumerable<NewFolding> CreateFoldings(TextDocument document, out int firstErrorOffset)
    {
        firstErrorOffset = -1;
        var foldings = new List<NewFolding>();
        var text = document.Text;
        if (string.IsNullOrEmpty(text)) return foldings;

        var braceStack = new Stack<int>();
        var regionStack = new Stack<(int Offset, string Name)>();

        bool inLineComment = false;
        bool inBlockComment = false;
        bool inString = false;
        bool inChar = false;
        int blockCommentStart = -1;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            char next = (i + 1 < text.Length) ? text[i + 1] : '\0';

            if (c == '\n')
            {
                inLineComment = false;
                inString = false;
                inChar = false;
                continue;
            }

            if (inLineComment) continue;

            // Block comment
            if (inBlockComment)
            {
                if (c == '*' && next == '/')
                {
                    inBlockComment = false;
                    i++; // skip '/'
                    int end = i + 1;
                    int startLine = document.GetLineByOffset(blockCommentStart).LineNumber;
                    int endLine = document.GetLineByOffset(end).LineNumber;
                    if (startLine < endLine)
                        foldings.Add(new NewFolding(blockCommentStart, end) { Name = "/* ... */" });
                }
                continue;
            }

            // String literals
            if (inString)
            {
                if (c == '\\') { i++; continue; }
                if (c == '"') inString = false;
                continue;
            }

            if (inChar)
            {
                if (c == '\\') { i++; continue; }
                if (c == '\'') inChar = false;
                continue;
            }

            // Comments
            if (c == '/' && next == '/') { inLineComment = true; i++; continue; }
            if (c == '/' && next == '*') { inBlockComment = true; blockCommentStart = i; i++; continue; }

            // Strings and chars
            if (c == '"') { inString = true; continue; }
            if (c == '\'') { inChar = true; continue; }

            // Curly braces
            if (c == '{')
            {
                braceStack.Push(i);
            }
            else if (c == '}' && braceStack.Count > 0)
            {
                int start = braceStack.Pop();
                int startLine = document.GetLineByOffset(start).LineNumber;
                int endLine = document.GetLineByOffset(i).LineNumber;
                if (startLine < endLine)
                    foldings.Add(new NewFolding(start, i + 1) { Name = "{...}" });
            }
        }

        // Fold #pragma region ... #pragma endregion
        CheckRegions(document, foldings);

        foldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        return foldings;
    }

    private static void CheckRegions(TextDocument document, List<NewFolding> foldings)
    {
        var stack = new Stack<(int LineNumber, int Offset, string Name)>();
        foreach (var line in document.Lines)
        {
            var text = document.GetText(line).Trim();
            if (text.StartsWith("#pragma region", StringComparison.OrdinalIgnoreCase))
            {
                var name = text.Length > 14 ? text[14..].Trim() : "region";
                if (string.IsNullOrEmpty(name)) name = "region";
                stack.Push((line.LineNumber, line.Offset, name));
            }
            else if (text.StartsWith("#pragma endregion", StringComparison.OrdinalIgnoreCase) && stack.Count > 0)
            {
                var (startLine, startOffset, name) = stack.Pop();
                if (line.LineNumber > startLine)
                {
                    foldings.Add(new NewFolding(startOffset, line.EndOffset) { Name = name });
                }
            }
        }
    }
}
