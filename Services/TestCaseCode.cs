using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

/// <summary>
/// A script's test cases are its <c>Check("name", answer, "expected")</c> lines: each prints a ✅/❌ line the Test Cases
/// panel reads (see <see cref="Judge"/>). This finds, adds and removes those lines in the script's code.
/// </summary>
public static class TestCaseCode
{
    public static string CheckLine(string name, string call, string expected, bool anyOrder = false) =>
        $"Check({Literal(name)}, {call.Trim()}, {Literal(expected)}{(anyOrder ? ", anyOrder: true" : string.Empty)});";

    /// <summary>True when the code already has a Check line for a case called <paramref name="name"/>.</summary>
    public static bool HasCheck(string code, string name) => FindCheck(SplitLines(code), name) >= 0;

    /// <summary>
    /// Adds <paramref name="checkLine"/> after the script's last Check line, indented like it, so it runs where the other
    /// cases do (for a Blind 75 script: after <c>var sol = new Solution();</c>). A script with no Check lines gets it at the
    /// end, under a comment.
    /// </summary>
    public static string AddCheck(string code, string checkLine)
    {
        string newline = code.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = SplitLines(code);
        int last = lines.FindLastIndex(IsCheckLine);
        if (last >= 0)
        {
            string indent = lines[last][..(lines[last].Length - lines[last].TrimStart().Length)];
            lines.Insert(last + 1, indent + checkLine);
            return string.Join(newline, lines);
        }

        string trimmed = code.TrimEnd();
        string separator = trimmed.Length == 0 ? string.Empty : newline + newline;
        return trimmed + separator + "// ── Test cases: each Check prints ✅ when the answer matches, ❌ when it doesn't ──" + newline + checkLine + newline;
    }

    /// <summary>Removes the Check line for <paramref name="name"/>, if the code still has one.</summary>
    public static string RemoveCheck(string code, string name)
    {
        string newline = code.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = SplitLines(code);
        int index = FindCheck(lines, name);
        if (index < 0) return code;
        lines.RemoveAt(index);
        return string.Join(newline, lines);
    }

    private static List<string> SplitLines(string code) =>
        code.Split('\n').Select(line => line.TrimEnd('\r')).ToList();

    private static bool IsCheckLine(string line) => line.TrimStart().StartsWith("Check(", StringComparison.Ordinal);

    private static int FindCheck(List<string> lines, string name)
    {
        string start = $"Check({Literal(name)},";
        return lines.FindIndex(line => line.TrimStart().StartsWith(start, StringComparison.Ordinal));
    }

    private static string Literal(string text) =>
        Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(text, quote: true);
}
