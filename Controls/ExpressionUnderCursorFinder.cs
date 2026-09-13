using System;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls;

public static class ExpressionUnderCursorFinder
{
    public static (string identifier, string dottedPath, int startCol, int endCol) FindExpression(string lineText, int column1Based)
    {
        if (string.IsNullOrEmpty(lineText)) return (string.Empty, string.Empty, 0, 0);

        int col0 = column1Based - 1; // Convert 1-based column to 0-based index
        if (col0 < 0) col0 = 0;
        if (col0 > lineText.Length) col0 = lineText.Length;

        // If pointer is at end of line or on whitespace/delimiters, allow one step back
        if (col0 == lineText.Length || (!char.IsLetterOrDigit(lineText[col0]) && lineText[col0] != '_'))
        {
            if (col0 > 0 && (char.IsLetterOrDigit(lineText[col0 - 1]) || lineText[col0 - 1] == '_'))
            {
                col0--;
            }
            else
            {
                return (string.Empty, string.Empty, 0, 0);
            }
        }

        // 1. Find identifier bounds [idStart, idEnd)
        int idStart = col0;
        while (idStart > 0 && (char.IsLetterOrDigit(lineText[idStart - 1]) || lineText[idStart - 1] == '_'))
        {
            idStart--;
        }

        int idEnd = col0;
        while (idEnd < lineText.Length && (char.IsLetterOrDigit(lineText[idEnd]) || lineText[idEnd] == '_'))
        {
            idEnd++;
        }

        if (idStart >= idEnd)
        {
            return (string.Empty, string.Empty, 0, 0);
        }

        string identifier = lineText.Substring(idStart, idEnd - idStart);

        // 2. Expand backwards to find leading dotted chain (e.g. "environment" in "environment.Application")
        int chainStart = idStart;
        while (chainStart >= 2 && lineText[chainStart - 1] == '.' && (char.IsLetterOrDigit(lineText[chainStart - 2]) || lineText[chainStart - 2] == '_'))
        {
            int prevWordEnd = chainStart - 1;
            int prevWordStart = prevWordEnd - 1;
            while (prevWordStart > 0 && (char.IsLetterOrDigit(lineText[prevWordStart - 1]) || lineText[prevWordStart - 1] == '_'))
            {
                prevWordStart--;
            }
            chainStart = prevWordStart;
        }

        // 3. Expand forwards to find trailing dotted chain (e.g. "Application" in "environment.Application")
        int chainEnd = idEnd;
        while (chainEnd < lineText.Length - 1 && lineText[chainEnd] == '.' && (char.IsLetterOrDigit(lineText[chainEnd + 1]) || lineText[chainEnd + 1] == '_'))
        {
            int nextWordStart = chainEnd + 1;
            int nextWordEnd = nextWordStart;
            while (nextWordEnd < lineText.Length && (char.IsLetterOrDigit(lineText[nextWordEnd]) || lineText[nextWordEnd] == '_'))
            {
                nextWordEnd++;
            }
            chainEnd = nextWordEnd;
        }

        string dottedPath = lineText.Substring(chainStart, chainEnd - chainStart);

        return (identifier, dottedPath, idStart + 1, idEnd + 1);
    }
}
