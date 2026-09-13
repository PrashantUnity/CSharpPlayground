using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public enum ExecutionLanguageMode
{
    Statements,
    Program,
    Expression
}

public class RoslynCompilerService
{
    private readonly List<MetadataReference> _defaultReferences = new();
    private readonly List<AssemblyReferenceItem> _referenceItems = new();

    public IReadOnlyList<AssemblyReferenceItem> AvailableReferences => _referenceItems;

    public RoslynCompilerService()
    {
        InitializeDefaultReferences();
    }

    private void InitializeDefaultReferences()
    {
        var coreDir = Path.GetDirectoryName(typeof(object).Assembly.Location) ?? string.Empty;

        var systemAssemblies = new[]
        {
            typeof(object).Assembly.Location,                                  // System.Private.CoreLib
            typeof(Console).Assembly.Location,                                 // System.Console
            typeof(Enumerable).Assembly.Location,                              // System.Linq
            typeof(List<>).Assembly.Location,                                  // System.Collections
            typeof(IDictionary).Assembly.Location,                             // System.Collections.NonGeneric
            typeof(File).Assembly.Location,                                    // System.IO.FileSystem
            typeof(JsonSerializer).Assembly.Location,                          // System.Text.Json
            typeof(Regex).Assembly.Location,                                   // System.Text.RegularExpressions
            typeof(System.Diagnostics.Stopwatch).Assembly.Location,            // System.Diagnostics.Stopwatch
            Path.Combine(coreDir, "System.Runtime.dll"),                       // System.Runtime
            Path.Combine(coreDir, "System.Collections.dll"),                   // System.Collections
            Path.Combine(coreDir, "System.Collections.NonGeneric.dll"),        // System.Collections.NonGeneric
            Path.Combine(coreDir, "System.Linq.dll"),                          // System.Linq
            Path.Combine(coreDir, "System.Text.Json.dll"),                     // System.Text.Json
            Path.Combine(coreDir, "netstandard.dll")                           // netstandard
        };

        foreach (var path in systemAssemblies)
        {
            if (File.Exists(path))
            {
                try
                {
                    var name = Path.GetFileNameWithoutExtension(path);
                    if (!_referenceItems.Any(r => r.Name == name))
                    {
                        _defaultReferences.Add(MetadataReference.CreateFromFile(path));
                        _referenceItems.Add(new AssemblyReferenceItem
                        {
                            Name = name,
                            AssemblyPath = path,
                            Description = "Standard .NET runtime assembly",
                            IsEnabled = true,
                            IsSystem = true
                        });
                    }
                }
                catch
                {
                    // Ignore unresolvable reference
                }
            }
        }
    }

    private const string DumpHelperCode = $$$$""""
#nullable disable
#line 1 "internal_helpers.g.cs"
public static class DumpExtensions
{
    public static T Dump<T>(this T obj, string? label = null)
    {
        if (!string.IsNullOrEmpty(label))
        {
            global::System.Console.WriteLine($"=== {label} ===");
        }

        if (obj == null)
        {
            global::System.Console.WriteLine("<null>");
            return obj;
        }

        if (obj is string s)
        {
            global::System.Console.WriteLine(s);
            return obj;
        }

        if (obj is global::System.Collections.IDictionary dict)
        {
            global::System.Console.WriteLine($"[Dictionary: {dict.Count} entries]");
            foreach (global::System.Collections.DictionaryEntry entry in dict)
            {
                global::System.Console.WriteLine($"  {entry.Key} => {FormatValue(entry.Value)}");
            }
            if (dict.Count == 0) global::System.Console.WriteLine("  (empty dictionary)");
            return obj;
        }

        if (obj is global::System.Collections.IEnumerable enumerable)
        {
            int index = 0;
            var typeName = obj.GetType().Name.Replace("`1", "");
            global::System.Console.WriteLine($"[{typeName}]");
            foreach (var item in enumerable)
            {
                global::System.Console.WriteLine($"  [{index++}] {FormatValue(item)}");
            }
            if (index == 0) global::System.Console.WriteLine("  (empty collection)");
            return obj;
        }

        global::System.Console.WriteLine(FormatValue(obj));
        return obj;
    }

    private static string FormatValue(object? item)
    {
        if (item == null) return "<null>";
        if (item is string or int or long or double or float or decimal or bool or global::System.DateTime or global::System.TimeSpan or global::System.Guid)
        {
            return item.ToString() ?? "";
        }
        if (item is global::System.Exception ex)
        {
            return $"[Exception {ex.GetType().Name}]: {ex.Message}";
        }
        try
        {
            return global::System.Text.Json.JsonSerializer.Serialize(item, new global::System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return item.ToString() ?? "";
        }
    }
}
"""";

    public string WrapSourceCode(string rawCode, ExecutionLanguageMode mode)
    {
        if (string.IsNullOrWhiteSpace(rawCode))
        {
            return string.Empty;
        }

        // 1. Auto-detect if raw code is already a complete Program with an entry point
        var isExplicitProgram = mode == ExecutionLanguageMode.Program ||
            (rawCode.Contains("class ") && (rawCode.Contains("static void Main") || rawCode.Contains("static async Task Main") || rawCode.Contains("static Task Main") || rawCode.Contains("static int Main"))) ||
            rawCode.Contains("namespace ");

        if (isExplicitProgram)
        {
            if (!rawCode.Contains("class DumpExtensions") && rawCode.Contains(".Dump("))
            {
                return $"#line 1 \"script.cs\"\n{rawCode}\n\n{DumpHelperCode}";
            }
            return $"#line 1 \"script.cs\"\n{rawCode}";
        }

        // 2. Parse using directives to hoist them out of statements/expressions
        var (hoistedUsings, remainingCode) = ExtractAndHoistUsings(rawCode);

        var defaultUsings = @"using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Diagnostics;
using System.Threading.Tasks;";

        var allUsings = defaultUsings;
        if (!string.IsNullOrWhiteSpace(hoistedUsings))
        {
            allUsings += "\n" + hoistedUsings;
        }

        // 3. Expression mode
        if (mode == ExecutionLanguageMode.Expression)
        {
            var expr = remainingCode.Trim().TrimEnd(';');
            return $@"{allUsings}

#line 1 ""script.cs""
({expr}).Dump();

{DumpHelperCode}";
        }

        // 4. Statements mode (LINQPad / Notebook Cells)
        // Check if user code is a bare expression without semicolon, e.g. "1 + 1" or "DateTime.Now"
        var trimmed = remainingCode.Trim();
        if (!trimmed.Contains(';') && !trimmed.Contains('\n') && !trimmed.Contains('{') && !trimmed.StartsWith("//"))
        {
            return $@"{allUsings}

#line 1 ""script.cs""
({trimmed}).Dump();

{DumpHelperCode}";
        }

        // In C# top-level statements, types (classes, records, structs) must appear AFTER all statements.
        // We separate any user-defined types from top-level statements so both can be declared in Statements mode!
        var (statements, types) = SeparateStatementsAndTypes(remainingCode);

        var sb = new StringBuilder();
        sb.AppendLine(allUsings);
        sb.AppendLine();
        sb.AppendLine("#line 1 \"script.cs\"");
        sb.AppendLine(statements);
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(types))
        {
            sb.AppendLine("#line default");
            sb.AppendLine("// User declared types:");
            sb.AppendLine(types);
            sb.AppendLine();
        }
        sb.AppendLine(DumpHelperCode);

        return sb.ToString();
    }

    private static (string HoistedUsings, string RemainingCode) ExtractAndHoistUsings(string rawCode)
    {
        var usingLines = new List<string>();
        var remainingLines = new List<string>();

        using var reader = new StringReader(rawCode);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("using ") && trimmed.EndsWith(';') && !trimmed.Contains('('))
            {
                usingLines.Add(trimmed);
                remainingLines.Add("// " + line); // Preserve line position so mapped lines match 1:1 with user editor
            }
            else
            {
                remainingLines.Add(line);
            }
        }

        var hoisted = string.Join("\n", usingLines.Distinct());
        var remaining = string.Join("\n", remainingLines);
        return (hoisted, remaining);
    }

    private static (string Statements, string Types) SeparateStatementsAndTypes(string code)
    {
        try
        {
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetCompilationUnitRoot();

            var types = new List<string>();
            var statements = new List<string>();

            foreach (var member in root.Members)
            {
                if (member is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax or EnumDeclarationSyntax)
                {
                    types.Add(member.ToFullString());
                }
                else
                {
                    statements.Add(member.ToFullString());
                }
            }

            if (types.Count > 0)
            {
                return (string.Join("\n", statements), string.Join("\n", types));
            }
        }
        catch
        {
            // Fallback to raw code
        }

        return (code, string.Empty);
    }

    public IReadOnlyList<DiagnosticItem> CheckDiagnostics(string sourceCode, ExecutionLanguageMode mode = ExecutionLanguageMode.Statements)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return Array.Empty<DiagnosticItem>();
        }

        var codeToAnalyze = WrapSourceCode(sourceCode, mode);
        var syntaxTree = CSharpSyntaxTree.ParseText(codeToAnalyze);
        var compilation = CSharpCompilation.Create(
            $"ScriptAnalysis_{Guid.NewGuid():N}",
            syntaxTrees: [syntaxTree],
            references: _defaultReferences,
            options: new CSharpCompilationOptions(
                OutputKind.ConsoleApplication,
                optimizationLevel: OptimizationLevel.Debug,
                allowUnsafe: false));

        var diagnostics = compilation.GetDiagnostics();
        var results = new List<DiagnosticItem>();

        foreach (var diag in diagnostics)
        {
            if (diag.Severity == DiagnosticSeverity.Hidden)
            {
                continue;
            }

            var mappedSpan = diag.Location.GetMappedLineSpan();
            // If the diagnostic originated from an internal helper or synthetic file outside user code, ignore it
            if (mappedSpan.IsValid && !string.IsNullOrEmpty(mappedSpan.Path) && mappedSpan.Path != "script.cs")
            {
                continue;
            }

            var lineSpan = mappedSpan.IsValid ? mappedSpan : diag.Location.GetLineSpan();
            var startLine = lineSpan.StartLinePosition.Line + 1;
            var startCol = lineSpan.StartLinePosition.Character + 1;
            var endLine = lineSpan.EndLinePosition.Line + 1;
            var endCol = lineSpan.EndLinePosition.Character + 1;

            results.Add(new DiagnosticItem
            {
                Id = diag.Id,
                Message = diag.GetMessage(),
                Severity = diag.Severity,
                Line = startLine,
                Column = startCol,
                EndLine = endLine,
                EndColumn = endCol
            });
        }

        return results.OrderByDescending(d => d.Severity).ThenBy(d => d.Line).ToList();
    }

    public (bool Success, byte[]? AssemblyBytes, IReadOnlyList<DiagnosticItem> Diagnostics) CompileToAssembly(
        string sourceCode,
        ExecutionLanguageMode mode = ExecutionLanguageMode.Statements)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return (false, null, [new DiagnosticItem { Id = "CS0001", Message = "Source code is empty.", Severity = DiagnosticSeverity.Error }]);
        }

        var wrappedCode = WrapSourceCode(sourceCode, mode);
        var syntaxTree = CSharpSyntaxTree.ParseText(wrappedCode);
        var compilation = CSharpCompilation.Create(
            $"ScriptExec_{Guid.NewGuid():N}",
            syntaxTrees: [syntaxTree],
            references: _defaultReferences,
            options: new CSharpCompilationOptions(
                OutputKind.ConsoleApplication,
                optimizationLevel: OptimizationLevel.Release,
                allowUnsafe: false));

        using var peStream = new MemoryStream();
        var emitResult = compilation.Emit(peStream);

        var diagnostics = emitResult.Diagnostics
            .Where(d => d.Severity != DiagnosticSeverity.Hidden)
            .Where(d =>
            {
                var mapped = d.Location.GetMappedLineSpan();
                return !mapped.IsValid || string.IsNullOrEmpty(mapped.Path) || mapped.Path == "script.cs";
            })
            .Select(d =>
            {
                var mapped = d.Location.GetMappedLineSpan();
                var span = mapped.IsValid ? mapped : d.Location.GetLineSpan();
                return new DiagnosticItem
                {
                    Id = d.Id,
                    Message = d.GetMessage(),
                    Severity = d.Severity,
                    Line = span.StartLinePosition.Line + 1,
                    Column = span.StartLinePosition.Character + 1,
                    EndLine = span.EndLinePosition.Line + 1,
                    EndColumn = span.EndLinePosition.Character + 1
                };
            })
            .OrderByDescending(d => d.Severity)
            .ThenBy(d => d.Line)
            .ToList();

        if (!emitResult.Success)
        {
            return (false, null, diagnostics);
        }

        peStream.Seek(0, SeekOrigin.Begin);
        return (true, peStream.ToArray(), diagnostics);
    }
}
