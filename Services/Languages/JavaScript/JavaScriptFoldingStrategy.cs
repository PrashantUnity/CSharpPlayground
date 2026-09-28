using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.JavaScript;

/// <summary>
/// Folding strategy for JavaScript/Node.js: collapses curly-brace blocks ({ ... }) and multiline block comments
/// (/* ... */). Handles template literals (backtick strings) and skips detection inside strings and line comments.
/// </summary>
public sealed class JavaScriptFoldingStrategy : ILanguageFolding
{
    public IEnumerable<NewFolding> CreateFoldings(TextDocument document, out int firstErrorOffset)
    {
        firstErrorOffset = -1;
        var foldings = new List<NewFolding>();
        var text = document.Text;
        if (string.IsNullOrEmpty(text)) return foldings;

        var braceStack = new Stack<int>();

        bool inLineComment = false;
        bool inBlockComment = false;
        bool inString = false;      // single or double-quoted
        bool inTemplate = false;    // backtick template literal
        int blockCommentStart = -1;
        char stringDelimiter = '"';

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            char next = (i + 1 < text.Length) ? text[i + 1] : '\0';

            if (c == '\n')
            {
                inLineComment = false;
                // single/double quoted strings don't span lines (template literals do)
                if (inString) inString = false;
                continue;
            }

            if (inLineComment) continue;

            if (inBlockComment)
            {
                if (c == '*' && next == '/')
                {
                    inBlockComment = false;
                    i++;
                    int end = i + 1;
                    int startLine = document.GetLineByOffset(blockCommentStart).LineNumber;
                    int endLine = document.GetLineByOffset(end).LineNumber;
                    if (startLine < endLine)
                        foldings.Add(new NewFolding(blockCommentStart, end) { Name = "/* ... */" });
                }
                continue;
            }

            if (inTemplate)
            {
                if (c == '\\') { i++; continue; }
                if (c == '`') inTemplate = false;
                continue;
            }

            if (inString)
            {
                if (c == '\\') { i++; continue; }
                if (c == stringDelimiter) inString = false;
                continue;
            }

            // Comments
            if (c == '/' && next == '/') { inLineComment = true; i++; continue; }
            if (c == '/' && next == '*') { inBlockComment = true; blockCommentStart = i; i++; continue; }

            // Strings
            if (c == '`') { inTemplate = true; continue; }
            if (c == '"' || c == '\'') { inString = true; stringDelimiter = c; continue; }

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

        foldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        return foldings;
    }
}
