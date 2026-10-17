using System.Collections;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;

public enum ExecutionLanguageMode
{
    Statements,
    Program,
    Expression
}

public partial class RoslynCompilerService
{
    /// <summary>The namespaces every script, program and statement cell compiles with (see <see cref="ScriptImports"/>).</summary>
    public static string DefaultScriptUsings => ScriptImports.GlobalUsingDirectives;

    private static readonly Lazy<(List<MetadataReference> References, List<AssemblyReferenceItem> Items)> CachedDefaultReferences =
        new(LoadDefaultReferencesInternal);

    public static IReadOnlyList<MetadataReference> SharedDefaultReferences => CachedDefaultReferences.Value.References;
    public static IReadOnlyList<AssemblyReferenceItem> SharedReferenceItems => CachedDefaultReferences.Value.Items;

    public static void Warmup()
    {
        _ = CachedDefaultReferences.Value;
    }

    private readonly List<MetadataReference> _defaultReferences = new();
    private readonly List<AssemblyReferenceItem> _referenceItems = new();

    public IReadOnlyList<AssemblyReferenceItem> AvailableReferences => _referenceItems;
    public IReadOnlyList<MetadataReference> DefaultReferences => _defaultReferences;

    public RoslynCompilerService()
    {
        var cached = CachedDefaultReferences.Value;
        _defaultReferences.AddRange(cached.References);
        _referenceItems.AddRange(cached.Items);
    }

    private static (List<MetadataReference> References, List<AssemblyReferenceItem> Items) LoadDefaultReferencesInternal()
    {
        var references = new List<MetadataReference>();
        var items = new List<AssemblyReferenceItem>();

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
            typeof(Task).Assembly.Location,                                    // System.Threading.Tasks
            typeof(HttpClient).Assembly.Location,                              // System.Net.Http
            typeof(Uri).Assembly.Location,                                     // System.Private.Uri (required transitively by System.Net.Http types)
            typeof(System.Net.HttpStatusCode).Assembly.Location,               // System.Net.Primitives (required transitively by System.Net.Http types)
            typeof(RoslynCompilerService).Assembly.Location,                   // FrySharp.Core
            typeof(System.Data.DataTable).Assembly.Location,                   // System.Data.Common
            typeof(System.Threading.SendOrPostCallback).Assembly.Location,     // System.Threading (CS0012 SendOrPostCallback)
            typeof(System.ComponentModel.INotifyPropertyChanged).Assembly.Location, // System.ObjectModel (CS0012 INotifyPropertyChanged)
            typeof(System.ComponentModel.Component).Assembly.Location,         // System.ComponentModel.Primitives
            Path.Combine(coreDir, "System.Runtime.dll"),                       // System.Runtime
            Path.Combine(coreDir, "System.Collections.dll"),                   // System.Collections
            Path.Combine(coreDir, "System.Collections.NonGeneric.dll"),        // System.Collections.NonGeneric
            Path.Combine(coreDir, "System.Data.Common.dll"),                   // System.Data.Common
            Path.Combine(coreDir, "System.Linq.dll"),                          // System.Linq
            Path.Combine(coreDir, "System.Text.Json.dll"),                     // System.Text.Json
            Path.Combine(coreDir, "System.Threading.dll"),                     // System.Threading
            Path.Combine(coreDir, "System.Threading.Thread.dll"),              // System.Threading.Thread
            Path.Combine(coreDir, "System.ObjectModel.dll"),                   // System.ObjectModel
            Path.Combine(coreDir, "System.ComponentModel.dll"),                // System.ComponentModel
            Path.Combine(coreDir, "System.ComponentModel.Primitives.dll"),     // System.ComponentModel.Primitives
            Path.Combine(coreDir, "System.ComponentModel.TypeConverter.dll"),  // System.ComponentModel.TypeConverter
            Path.Combine(coreDir, "System.Threading.Tasks.Parallel.dll"),      // Parallel.For / ForEach (only there when something had loaded it)
            Path.Combine(coreDir, "System.Collections.Concurrent.dll"),        // ConcurrentBag, ConcurrentDictionary
            Path.Combine(coreDir, "System.Linq.Parallel.dll"),                 // AsParallel
            Path.Combine(coreDir, "netstandard.dll")                           // netstandard
        };

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string path, string description, bool isSystem, bool listed)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            var name = Path.GetFileNameWithoutExtension(path);
            if (!seen.Add(name)) return;
            try
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
            catch (Exception ex) when (ex is IOException or BadImageFormatException or UnauthorizedAccessException)
            {
                seen.Remove(name);
                return;
            }

            if (listed)
            {
                items.Add(new AssemblyReferenceItem
                {
                    Name = name,
                    AssemblyPath = path,
                    Description = description,
                    IsEnabled = true,
                    IsSystem = isSystem
                });
            }
        }

        foreach (var path in systemAssemblies)
        {
            Add(path, "Standard .NET runtime assembly", isSystem: true, listed: true);
        }

        // The host's own assemblies (the desktop plugin and Avalonia, or Core alone), listed in the References panel.
        foreach (var assembly in ScriptHost.Profile.Assemblies)
        {
            Add(assembly.Location, "Host assembly", isSystem: false, listed: true);
        }

        // Everything else the host process can load by default: the rest of .NET and the app's dependencies. Taken from
        // the trusted platform assemblies, so a script compiles the same however many assemblies happen to be loaded yet.
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trusted)
        {
            foreach (var path in trusted.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                Add(path, "Host process assembly", isSystem: true, listed: false);
            }
        }

        return (references, items);
    }

    /// <summary>True when the code is a whole program (a class with a Main, or a namespace) rather than statements.</summary>
    public static bool IsExplicitProgram(string code) =>
        (code.Contains("class ") && (code.Contains("static void Main") || code.Contains("static async Task Main") || code.Contains("static Task Main") || code.Contains("static int Main"))) ||
        code.Contains("namespace ");

    public string WrapSourceCode(string rawCode, ExecutionLanguageMode mode)
    {
        if (string.IsNullOrWhiteSpace(rawCode))
        {
            return string.Empty;
        }

        // 1. Auto-detect if raw code is already a complete Program with an entry point
        var isExplicitProgram = mode == ExecutionLanguageMode.Program || IsExplicitProgram(rawCode);

        if (isExplicitProgram)
        {
            return $"{DefaultScriptUsings}\n#line 1 \"script.cs\"\n{rawCode}";
        }

        // 2. Parse using directives to hoist them out of statements/expressions
        var (hoistedUsings, remainingCode) = ExtractAndHoistUsings(rawCode);

        var allUsings = DefaultScriptUsings;
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
";
        }

        // 4. Statements mode (Script / Notebook Cells)
        // Check if user code is a bare expression without semicolon, e.g. "1 + 1" or "DateTime.Now"
        var trimmed = remainingCode.Trim();
        if (!trimmed.Contains(';') && !trimmed.Contains('\n') && !trimmed.Contains('{') && !trimmed.StartsWith("//"))
        {
            return $@"{allUsings}

#line 1 ""script.cs""
({trimmed}).Dump();
";
        }

        // In C# top-level statements, types (classes, records, structs) must appear AFTER all statements.
        // We ensure statements precede types while strictly preserving 1:1 line mappings to script.cs!
        var (_, formattedCode) = FormatStatementsAndTypes(remainingCode);

        var sb = new StringBuilder();
        sb.AppendLine(allUsings);
        sb.AppendLine();
        sb.AppendLine(formattedCode);

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
            else if (trimmed.StartsWith("#r ") || trimmed.StartsWith("#load ") || trimmed.StartsWith("#!"))
            {
                remainingLines.Add("// " + line); // Directives handled by NuGet resolver / kernel
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

    private static (bool Reordered, string FormattedCode) FormatStatementsAndTypes(string code)
    {
        try
        {
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetCompilationUnitRoot();

            var types = new List<MemberDeclarationSyntax>();
            var statements = new List<MemberDeclarationSyntax>();

            bool sawType = false;
            bool reorderingNeeded = false;

            foreach (var member in root.Members)
            {
                if (member is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax or EnumDeclarationSyntax)
                {
                    types.Add(member);
                    sawType = true;
                }
                else
                {
                    statements.Add(member);
                    if (sawType)
                    {
                        // A top-level statement appeared AFTER a type declaration!
                        // In C#, top-level statements must precede type declarations.
                        reorderingNeeded = true;
                    }
                }
            }

            if (!reorderingNeeded)
            {
                // Statements already precede types (or file contains only statements or only types).
                // Preserve 1:1 original source code lines directly.
                return (false, "#line 1 \"script.cs\"\n" + code);
            }

            // Reordering needed: statements first, then types.
            // Explicitly preserve original line numbers for every member using #line directives!
            var sb = new StringBuilder();

            foreach (var stmt in statements)
            {
                var line = stmt.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                sb.AppendLine($"#line {line} \"script.cs\"");
                sb.AppendLine(stmt.WithoutLeadingTrivia().ToFullString());
            }

            foreach (var type in types)
            {
                var line = type.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                sb.AppendLine($"#line {line} \"script.cs\"");
                sb.AppendLine(type.WithoutLeadingTrivia().ToFullString());
            }

            return (true, sb.ToString());
        }
        catch
        {
            return (false, "#line 1 \"script.cs\"\n" + code);
        }
    }

    public IReadOnlyList<DiagnosticItem> CheckDiagnostics(string sourceCode, ExecutionLanguageMode mode = ExecutionLanguageMode.Statements)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return Array.Empty<DiagnosticItem>();
        }

        var compilation = mode == ExecutionLanguageMode.Statements && !IsExplicitProgram(sourceCode)
            ? ScriptAnalysisCompilation(sourceCode)
            : ProgramAnalysisCompilation(sourceCode, mode);

        var diagnostics = compilation.GetDiagnostics();
        var results = new List<DiagnosticItem>();

        foreach (var diag in diagnostics)
        {
            if (diag.Severity == DiagnosticSeverity.Hidden || diag.Id == "CS0105" || diag.Id == "CS8933")
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

    private static readonly Dictionary<string, ReportDiagnostic> AnalysisDiagnosticOptions = new()
    {
        ["CS0105"] = ReportDiagnostic.Suppress,
        ["CS8933"] = ReportDiagnostic.Suppress
    };

    // A program (or an expression) as it is compiled to run: wrapped, with the imports as global usings.
    private CSharpCompilation ProgramAnalysisCompilation(string sourceCode, ExecutionLanguageMode mode) =>
        CSharpCompilation.Create(
            $"ScriptAnalysis_{Guid.NewGuid():N}",
            syntaxTrees: [CSharpSyntaxTree.ParseText(WrapSourceCode(sourceCode, mode))],
            references: _defaultReferences,
            options: new CSharpCompilationOptions(
                OutputKind.ConsoleApplication,
                optimizationLevel: OptimizationLevel.Debug,
                allowUnsafe: false,
                specificDiagnosticOptions: AnalysisDiagnosticOptions));

    // Statements as the kernel runs them: a C# script, where a last expression without a semicolon is the result and
    // types may sit anywhere. Prepared the same way (#r lines out, using declarations rewritten); lines don't move.
    private CSharpCompilation ScriptAnalysisCompilation(string sourceCode)
    {
        var prepared = Kernels.CellUsingDeclarations.Rewrite(ReferenceDirective().Replace(sourceCode, string.Empty));
        var tree = CSharpSyntaxTree.ParseText(prepared,
            new CSharpParseOptions(LanguageVersion.Latest, kind: SourceCodeKind.Script), path: "script.cs");
        return CSharpCompilation.CreateScriptCompilation(
            $"ScriptAnalysis_{Guid.NewGuid():N}",
            tree,
            _defaultReferences,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                usings: ScriptImports.ScriptingImports,
                optimizationLevel: OptimizationLevel.Debug,
                allowUnsafe: false,
                specificDiagnosticOptions: AnalysisDiagnosticOptions));
    }

    // #r "nuget: …" and #r "path" lines: the kernel resolves them before compiling; analysis leaves the line empty.
    [GeneratedRegex(@"(?m)^[ \t]*#r[ \t]+""[^""\r\n]*""[ \t]*$")]
    private static partial Regex ReferenceDirective();

    public (bool Success, byte[]? AssemblyBytes, IReadOnlyList<DiagnosticItem> Diagnostics) CompileToAssembly(
        string sourceCode,
        ExecutionLanguageMode mode = ExecutionLanguageMode.Statements)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return (false, null, [new DiagnosticItem { Id = "CS0001", Message = "Source code is empty.", Severity = DiagnosticSeverity.Error }]);
        }

        var references = new List<MetadataReference>(_defaultReferences);
        if (sourceCode.Contains("#r ") && sourceCode.Contains("nuget:"))
        {
            try
            {
                var resolver = new NuGetReferenceResolver();
                var nugetResult = resolver.ProcessDirectives(sourceCode);
                if (nugetResult.References.Count > 0)
                {
                    references.AddRange(nugetResult.References);
                }
            }
            catch
            {
                // Non-fatal if NuGet resolution encounters an issue
            }
        }

        var wrappedCode = WrapSourceCode(sourceCode, mode);
        var syntaxTree = CSharpSyntaxTree.ParseText(wrappedCode);
        var compilation = CSharpCompilation.Create(
            $"ScriptExec_{Guid.NewGuid():N}",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(
                OutputKind.ConsoleApplication,
                optimizationLevel: OptimizationLevel.Release,
                allowUnsafe: false,
                specificDiagnosticOptions: new Dictionary<string, ReportDiagnostic>
                {
                    ["CS0105"] = ReportDiagnostic.Suppress,
                    ["CS8933"] = ReportDiagnostic.Suppress
                }));

        using var peStream = new MemoryStream();
        var emitResult = compilation.Emit(peStream);

        var diagnostics = emitResult.Diagnostics
            .Where(d => d.Severity != DiagnosticSeverity.Hidden && d.Id != "CS0105" && d.Id != "CS8933")
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
