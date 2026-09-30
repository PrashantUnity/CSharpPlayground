using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;

/// <summary>
/// SQL folding strategy: collapses multiline block comments (/* ... */), parenthesized statements
/// (CREATE TABLE (...), INSERT INTO (...)), and BEGIN ... END blocks.
/// </summary>
public sealed class SqlFoldingStrategy : ILanguageFolding
{
    public IEnumerable<NewFolding> CreateFoldings(TextDocument document, out int firstErrorOffset)
    {
        firstErrorOffset = -1;
        var foldings = new List<NewFolding>();
        var text = document.Text;
        if (string.IsNullOrEmpty(text)) return foldings;

        AddBlockCommentFoldings(text, document, foldings);
        AddParenthesizedFoldings(text, document, foldings);
        AddBeginEndFoldings(text, document, foldings);

        foldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        return foldings;
    }

    private static void AddBlockCommentFoldings(string text, TextDocument doc, List<NewFolding> foldings)
    {
        var start = 0;
        while ((start = text.IndexOf("/*", start, StringComparison.Ordinal)) >= 0)
        {
            var end = text.IndexOf("*/", start + 2, StringComparison.Ordinal);
            if (end < 0) break;

            var startLine = doc.GetLineByOffset(start).LineNumber;
            var endLine = doc.GetLineByOffset(end).LineNumber;
            if (endLine > startLine)
            {
                foldings.Add(new NewFolding(start, end + 2) { Name = "/* ... */" });
            }
            start = end + 2;
        }
    }

    private static void AddParenthesizedFoldings(string text, TextDocument doc, List<NewFolding> foldings)
    {
        var parenStack = new Stack<int>();
        var inString = false;
        var stringChar = '\0';

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (inString)
            {
                if (c == stringChar)
                {
                    if (stringChar == '\'' && i + 1 < text.Length && text[i + 1] == '\'')
                    {
                        i++; // skip escaped quote ''
                    }
                    else
                    {
                        inString = false;
                    }
                }
                continue;
            }

            if (c == '\'' || c == '"')
            {
                inString = true;
                stringChar = c;
                continue;
            }

            if (c == '-' && i + 1 < text.Length && text[i + 1] == '-')
            {
                // skip to end of line
                var eol = text.IndexOf('\n', i + 2);
                if (eol < 0) break;
                i = eol;
                continue;
            }

            if (c == '(')
            {
                parenStack.Push(i);
            }
            else if (c == ')' && parenStack.Count > 0)
            {
                var open = parenStack.Pop();
                var startLine = doc.GetLineByOffset(open).LineNumber;
                var endLine = doc.GetLineByOffset(i).LineNumber;
                if (endLine > startLine)
                {
                    foldings.Add(new NewFolding(open, i + 1) { Name = "(...)" });
                }
            }
        }
    }

    private static void AddBeginEndFoldings(string text, TextDocument doc, List<NewFolding> foldings)
    {
        var words = System.Text.RegularExpressions.Regex.Matches(text, @"\b(?:BEGIN|END)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var beginStack = new Stack<int>();

        foreach (System.Text.RegularExpressions.Match match in words)
        {
            if (match.Value.Equals("BEGIN", StringComparison.OrdinalIgnoreCase))
            {
                beginStack.Push(match.Index);
            }
            else if (match.Value.Equals("END", StringComparison.OrdinalIgnoreCase) && beginStack.Count > 0)
            {
                var start = beginStack.Pop();
                var startLine = doc.GetLineByOffset(start).LineNumber;
                var endLine = doc.GetLineByOffset(match.Index).LineNumber;
                if (endLine > startLine)
                {
                    foldings.Add(new NewFolding(start, match.Index + match.Length) { Name = "BEGIN ... END" });
                }
            }
        }
    }
}
