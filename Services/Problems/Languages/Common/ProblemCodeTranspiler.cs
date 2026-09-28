using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.Common;

/// <summary>
/// Utility service that parses C# problem signatures and transpiles test call expressions
/// into idiomatic Python, JavaScript, and Java syntax.
/// </summary>
public static partial class ProblemCodeTranspiler
{
    [GeneratedRegex(@"public\s+(?:(?:static|async|override|virtual)\s+)*(?<ret>[a-zA-Z0-9_<>[\]?]+)\s+(?<name>[a-zA-Z0-9_]+)\s*\((?<params>[^)]*)\)", RegexOptions.Multiline)]
    private static partial Regex MethodSignatureRegex();

    public record MethodParam(string Type, string Name);
    public record MethodInfo(string Name, string ReturnType, List<MethodParam> Parameters);

    /// <summary>
    /// Parses the primary solution method from C# code.
    /// </summary>
    public static MethodInfo ExtractMethodInfo(string csharpCode, string problemTitle)
    {
        var match = MethodSignatureRegex().Match(csharpCode);
        if (match.Success)
        {
            var rawName = match.Groups["name"].Value;
            var rawRet = match.Groups["ret"].Value;
            var rawParams = match.Groups["params"].Value;

            var paramList = new List<MethodParam>();
            if (!string.IsNullOrWhiteSpace(rawParams))
            {
                var parts = rawParams.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                foreach (var p in parts)
                {
                    var tokens = p.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (tokens.Length >= 2)
                    {
                        paramList.Add(new MethodParam(tokens[^2], tokens[^1]));
                    }
                }
            }

            return new MethodInfo(rawName, rawRet, paramList);
        }

        var fallbackName = ToCamelCase(problemTitle);
        return new MethodInfo(char.ToUpperInvariant(fallbackName[0]) + fallbackName[1..], "void", new List<MethodParam>());
    }

    public static string ToCamelCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "solve";
        var words = text.Split(new[] { ' ', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return "solve";

        var sb = new StringBuilder();
        sb.Append(char.ToLowerInvariant(words[0][0]));
        if (words[0].Length > 1) sb.Append(words[0][1..]);

        for (int i = 1; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                sb.Append(char.ToUpperInvariant(words[i][0]));
                if (words[i].Length > 1) sb.Append(words[i][1..]);
            }
        }
        return sb.ToString();
    }

    public static string ToPascalCase(string text)
    {
        var camel = ToCamelCase(text);
        if (camel.Length == 0) return "Solve";
        return char.ToUpperInvariant(camel[0]) + camel[1..];
    }

    /// <summary>
    /// Transpiles a C# test call (e.g. `sol.TwoSum(new[] { 2, 7, 11, 15 }, 9)`) into Python syntax.
    /// </summary>
    public static string TranspileCallToPython(string csharpCall)
    {
        if (string.IsNullOrWhiteSpace(csharpCall)) return "None";

        string res = csharpCall.Trim();
        res = FixMethodInvocation(res, isPython: true);

        // Arrays: new[] { 1, 2 } or new int[] { 1, 2 } -> [1, 2]
        res = Regex.Replace(res, @"new\s*(?:[a-zA-Z0-9_]+)?\s*\[\s*\]\s*\[\s*\]\s*\{", "[");
        res = Regex.Replace(res, @"new\s*(?:[a-zA-Z0-9_]+)?\s*\[\s*\]\s*\{", "[");
        res = res.Replace("{", "[").Replace("}", "]");

        // Booleans and null
        res = Regex.Replace(res, @"\btrue\b", "True");
        res = Regex.Replace(res, @"\bfalse\b", "False");
        res = Regex.Replace(res, @"\bnull\b", "None");

        // Helpers
        res = res.Replace("BuildList(", "buildList(");
        res = res.Replace("BuildTree(", "buildTree(");
        res = res.Replace("LcaValue(", "lcaValue(");

        return res;
    }

    /// <summary>
    /// Transpiles a C# test call into JavaScript syntax.
    /// </summary>
    public static string TranspileCallToJavaScript(string csharpCall)
    {
        if (string.IsNullOrWhiteSpace(csharpCall)) return "null";

        string res = csharpCall.Trim();
        res = FixMethodInvocation(res, isPython: false);

        // Arrays
        res = Regex.Replace(res, @"new\s*(?:[a-zA-Z0-9_]+)?\s*\[\s*\]\s*\[\s*\]\s*\{", "[");
        res = Regex.Replace(res, @"new\s*(?:[a-zA-Z0-9_]+)?\s*\[\s*\]\s*\{", "[");
        res = res.Replace("{", "[").Replace("}", "]");

        // Helpers
        res = res.Replace("BuildList(", "buildList(");
        res = res.Replace("BuildTree(", "buildTree(");
        res = res.Replace("LcaValue(", "lcaValue(");

        return res;
    }

    /// <summary>
    /// Transpiles a C# test call into Java syntax.
    /// </summary>
    public static string TranspileCallToJava(string csharpCall)
    {
        if (string.IsNullOrWhiteSpace(csharpCall)) return "null";

        string res = csharpCall.Trim();
        res = FixMethodInvocation(res, isPython: false);

        // Convert untyped new[] { ... } to new int[] { ... } or new String[] { ... }
        res = Regex.Replace(res, @"new\s*\[\s*\]\s*\{(?=[^""]*""[^""]*"")", "new String[] {");
        res = Regex.Replace(res, @"new\s*\[\s*\]\s*\{", "new int[] {");

        // Convert nested arrays: new int[][] { new[] { ... } }
        res = Regex.Replace(res, @"new\s+(?:[a-zA-Z0-9_]+)?\s*\[\s*\]\s*\[\s*\]\s*\{", "new int[][] {");

        // Helpers
        res = res.Replace("BuildList(", "buildList(");
        res = res.Replace("BuildTree(", "buildTree(");
        res = res.Replace("LcaValue(", "lcaValue(");

        return res;
    }

    /// <summary>
    /// Transpiles a C# test call into C++ syntax.
    /// </summary>
    public static string TranspileCallToCpp(string csharpCall)
    {
        if (string.IsNullOrWhiteSpace(csharpCall)) return "nullptr";

        string res = csharpCall.Trim();
        res = FixMethodInvocation(res, isPython: false);

        // Convert untyped new[] { ... } or new int[] { ... } to C++ initializer lists { ... }
        res = Regex.Replace(res, @"new\s*(?:[a-zA-Z0-9_<>[\]]+)?\s*\[\s*\]\s*\[\s*\]\s*\{", "{");
        res = Regex.Replace(res, @"new\s*(?:[a-zA-Z0-9_<>[\]]+)?\s*\[\s*\]\s*\{", "{");

        // Helpers
        res = res.Replace("BuildList(", "ListNode::buildList(");
        res = res.Replace("BuildTree(", "TreeNode::buildTree(");
        res = res.Replace("LcaValue(", "TreeNode::lcaValue(");
        res = Regex.Replace(res, @"\bnull\b", "nullptr");

        return res;
    }

    private static string FixMethodInvocation(string call, bool isPython)
    {
        // sol.TwoSum(...) -> sol.twoSum(...)
        return Regex.Replace(call, @"sol\.([A-Z][a-zA-Z0-9_]*)", m =>
        {
            var methodName = m.Groups[1].Value;
            var camel = char.ToLowerInvariant(methodName[0]) + methodName[1..];
            return $"sol.{camel}";
        });
    }

    /// <summary>
    /// Formats an expected output value for the specified language.
    /// </summary>
    public static string FormatExpected(string expected, string languageId)
    {
        if (string.IsNullOrWhiteSpace(expected)) return "null";
        var trimmed = expected.Trim();

        return languageId switch
        {
            "python" => trimmed switch
            {
                "true" => "True",
                "false" => "False",
                "null" => "None",
                _ => trimmed
            },
            "javascript" => trimmed switch
            {
                "true" => "true",
                "false" => "false",
                "null" => "null",
                _ => trimmed
            },
            _ => trimmed
        };
    }
}
