using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;

/// <summary>
/// Folding strategy for Go: collapses curly-brace blocks ({ ... }), multiline block comments (/* ... */),
/// parenthesized declaration groups (import (...), const (...), var (...)), and multiline raw string literals.
/// </summary>
public sealed class GoFoldingStrategy : ILanguageFolding
{
    public IEnumerable<NewFolding> CreateFoldings(TextDocument document, out int firstErrorOffset)
    {
        firstErrorOffset = -1;
        var foldings = new List<NewFolding>();
        var text = document.Text;
        if (string.IsNullOrEmpty(text)) return foldings;

        var braceStack = new Stack<int>();
        var parenStack = new Stack<int>();

        bool inLineComment = false;
        bool inBlockComment = false;
        bool inString = false;
        bool inRawString = false;
        bool inChar = false;
        int blockCommentStart = -1;
        int rawStringStart = -1;

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
                    {
                        foldings.Add(new NewFolding(blockCommentStart, end) { Name = "/* ... */" });
                    }
                }
                continue;
            }

            // Raw string literal `...`
            if (inRawString)
            {
                if (c == '`')
                {
                    inRawString = false;
                    int end = i + 1;
                    int startLine = document.GetLineByOffset(rawStringStart).LineNumber;
                    int endLine = document.GetLineByOffset(end).LineNumber;
                    if (startLine < endLine)
                    {
                        foldings.Add(new NewFolding(rawStringStart, end) { Name = "`...`" });
                    }
                }
                continue;
            }

            // Interpreted string literal "..."
            if (inString)
            {
                if (c == '\\')
                {
                    i++; // skip escaped char
                }
                else if (c == '"')
                {
                    inString = false;
                }
                continue;
            }

            // Rune literal '...'
            if (inChar)
            {
                if (c == '\\')
                {
                    i++;
                }
                else if (c == '\'')
                {
                    inChar = false;
                }
                continue;
            }

            // Start of comment
            if (c == '/' && next == '/')
            {
                inLineComment = true;
                i++;
                continue;
            }
            if (c == '/' && next == '*')
            {
                inBlockComment = true;
                blockCommentStart = i;
                i++;
                continue;
            }

            // Start of string
            if (c == '"')
            {
                inString = true;
                continue;
            }
            if (c == '`')
            {
                inRawString = true;
                rawStringStart = i;
                continue;
            }
            if (c == '\'')
            {
                inChar = true;
                continue;
            }

            // Braces { }
            if (c == '{')
            {
                braceStack.Push(i);
            }
            else if (c == '}' && braceStack.Count > 0)
            {
                int start = braceStack.Pop();
                int end = i + 1;
                int startLine = document.GetLineByOffset(start).LineNumber;
                int endLine = document.GetLineByOffset(end).LineNumber;
                if (startLine < endLine)
                {
                    foldings.Add(new NewFolding(start, end) { Name = "{ ... }" });
                }
            }
            // Parentheses ( ) for import ( ... ), const ( ... ), var ( ... )
            else if (c == '(')
            {
                parenStack.Push(i);
            }
            else if (c == ')' && parenStack.Count > 0)
            {
                int start = parenStack.Pop();
                int end = i + 1;
                int startLine = document.GetLineByOffset(start).LineNumber;
                int endLine = document.GetLineByOffset(end).LineNumber;
                if (startLine < endLine)
                {
                    foldings.Add(new NewFolding(start, end) { Name = "( ... )" });
                }
            }
        }

        foldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        return foldings;
    }
}
