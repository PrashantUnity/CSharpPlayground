using System;
using System.Collections.Generic;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

/// <summary>
/// High-performance folding strategy for C# source code.
/// Detects and collapses multiline curly brace blocks ({ ... }), #region directives,
/// and multiline comments (/* ... */).
/// </summary>
public class CSharpFoldingStrategy
{
    public void UpdateFoldings(FoldingManager manager, TextDocument document)
    {
        if (manager == null || document == null) return;
        var newFoldings = CreateNewFoldings(document, out int firstErrorOffset);
        manager.UpdateFoldings(newFoldings, firstErrorOffset);
    }

    public IEnumerable<NewFolding> CreateNewFoldings(TextDocument document, out int firstErrorOffset)
    {
        firstErrorOffset = -1;
        var foldings = new List<NewFolding>();
        var text = document.Text;
        if (string.IsNullOrEmpty(text)) return foldings;

        var braceStack = new Stack<int>();
        var regionStack = new Stack<int>();

        bool inLineComment = false;
        bool inBlockComment = false;
        bool inString = false;
        bool inVerbatimString = false;
        bool inChar = false;
        int blockCommentStart = -1;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            char next = (i + 1 < text.Length) ? text[i + 1] : '\0';

            // Newline resets line-level states
            if (c == '\n')
            {
                inLineComment = false;
                if (inString && !inVerbatimString) inString = false;
                inChar = false;
                continue;
            }

            if (inLineComment) continue;

            // Multiline block comment
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

            // String literals
            if (inString)
            {
                if (inVerbatimString)
                {
                    if (c == '"')
                    {
                        if (next == '"') { i++; continue; } // Escaped quote ""
                        inString = false;
                        inVerbatimString = false;
                    }
                }
                else
                {
                    if (c == '\\') { i++; continue; } // Escaped character
                    if (c == '"') inString = false;
                }
                continue;
            }

            if (inChar)
            {
                if (c == '\\') { i++; continue; }
                if (c == '\'') inChar = false;
                continue;
            }

            // Start of comments
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

            // Start of strings
            if (c == '@' && next == '"')
            {
                inString = true;
                inVerbatimString = true;
                i++;
                continue;
            }

            if (c == '$' && next == '"')
            {
                inString = true;
                inVerbatimString = false;
                i++;
                continue;
            }

            if (c == '"')
            {
                inString = true;
                inVerbatimString = false;
                continue;
            }

            if (c == '\'')
            {
                inChar = true;
                continue;
            }

            // #region / #endregion
            if (c == '#')
            {
                int lineStart = i;
                while (lineStart > 0 && text[lineStart - 1] != '\n') lineStart--;
                string prefix = text.Substring(lineStart, i - lineStart).Trim();
                if (string.IsNullOrEmpty(prefix))
                {
                    int lineEnd = text.IndexOf('\n', i);
                    if (lineEnd == -1) lineEnd = text.Length;
                    string line = text.Substring(i, lineEnd - i).Trim();

                    if (line.StartsWith("#region"))
                    {
                        regionStack.Push(i);
                    }
                    else if (line.StartsWith("#endregion") && regionStack.Count > 0)
                    {
                        int rStart = regionStack.Pop();
                        foldings.Add(new NewFolding(rStart, lineEnd) { Name = "#region" });
                    }
                }
            }

            // Curly braces { ... }
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
                {
                    foldings.Add(new NewFolding(start, i + 1) { Name = "{...}" });
                }
            }
        }

        foldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        return foldings;
    }
}
