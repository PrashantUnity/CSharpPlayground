using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;

public sealed class CSharpCoreClrCompiler
{
    private readonly RoslynCompilerService _compilerService;

    public CSharpCoreClrCompiler(RoslynCompilerService compilerService)
    {
        _compilerService = compilerService ?? throw new ArgumentNullException(nameof(compilerService));
    }

    public (bool Success, string? DllPath, IReadOnlyList<DiagnosticItem> Diagnostics) CompileToStandaloneBinary(
        string rawSourceCode,
        string outputDirectory,
        string assemblyName = "script",
        ExecutionLanguageMode mode = ExecutionLanguageMode.Statements,
        string? exitCodeFile = null)
    {
        if (string.IsNullOrWhiteSpace(rawSourceCode))
        {
            return (false, null, [new DiagnosticItem { Id = "DBG001", Message = "Source code is empty.", Severity = DiagnosticSeverity.Error }]);
        }

        Directory.CreateDirectory(outputDirectory);

        var wrappedCode = _compilerService.WrapSourceCode(rawSourceCode, mode);
        var sourceText = Microsoft.CodeAnalysis.Text.SourceText.From(wrappedCode, Encoding.UTF8);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText, path: "script.cs");
        var syntaxTrees = new List<SyntaxTree> { syntaxTree };
        if (exitCodeFile != null) syntaxTrees.Add(CSharpSyntaxTree.ParseText(Microsoft.CodeAnalysis.Text.SourceText.From(ExitCodeReporter(exitCodeFile), Encoding.UTF8), path: "exit-code-reporter.cs"));

        var assemblyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in _compilerService.DefaultReferences)
        {
            if (r is PortableExecutableReference peRef && !string.IsNullOrEmpty(peRef.FilePath) && File.Exists(peRef.FilePath))
            {
                var name = Path.GetFileNameWithoutExtension(peRef.FilePath);
                assemblyMap[name] = peRef.FilePath;
            }
        }
        var probeDirs = assemblyMap.Values.Select(Path.GetDirectoryName).Where(d => !string.IsNullOrEmpty(d)).Distinct()!;
        syntaxTrees.Add(CSharpSyntaxTree.ParseText(Microsoft.CodeAnalysis.Text.SourceText.From(AssemblyResolver(assemblyMap, probeDirs!), Encoding.UTF8), path: "assembly-resolver.cs"));

        var compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees: syntaxTrees,
            references: _compilerService.DefaultReferences,
            options: new CSharpCompilationOptions(
                OutputKind.ConsoleApplication,
                optimizationLevel: OptimizationLevel.Debug,
                allowUnsafe: false));

        var dllPath = Path.Combine(outputDirectory, $"{assemblyName}.dll");
        var pdbPath = Path.Combine(outputDirectory, $"{assemblyName}.pdb");
        var runtimeConfigPath = Path.Combine(outputDirectory, $"{assemblyName}.runtimeconfig.json");

        using (var dllStream = File.Create(dllPath))
        using (var pdbStream = File.Create(pdbPath))
        {
            var emitResult = compilation.Emit(
                peStream: dllStream,
                pdbStream: pdbStream,
                options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));

            var diagnostics = emitResult.Diagnostics
                .Where(d => d.Severity != DiagnosticSeverity.Hidden)
                .Where(d =>
                {
                    var mapped = d.Location.GetMappedLineSpan();
                    return !mapped.IsValid || string.IsNullOrEmpty(mapped.Path) || mapped.Path is "script.cs" or "exit-code-reporter.cs" or "assembly-resolver.cs";
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
        }

        // Generate runtimeconfig.json for .NET 10 CoreCLR
        var runtimeConfigJson = """
        {
          "runtimeOptions": {
            "tfm": "net10.0",
            "framework": {
              "name": "Microsoft.NETCore.App",
              "version": "10.0.0"
            }
          }
        }
        """;
        File.WriteAllText(runtimeConfigPath, runtimeConfigJson, Encoding.UTF8);

        return (true, dllPath, Array.Empty<DiagnosticItem>());
    }

    /// <summary>
    /// A second source file for the debug build that writes the program's exit code to <paramref name="exitCodeFile"/> as the process ends:
    /// netcoredbg reports 0 in its <c>exited</c> event whatever the program returned, so the debugger reads the truth from here.
    /// </summary>
    internal static string ExitCodeReporter(string exitCodeFile) => $$"""
        internal static class FryExitCodeReporter
        {
            [System.Runtime.CompilerServices.ModuleInitializer]
            internal static void Register()
            {
                System.AppDomain.CurrentDomain.ProcessExit += (_, _) =>
                {
                    try
                    {
                        System.IO.File.WriteAllText({{ToLiteral(exitCodeFile)}}, System.Environment.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    }
                    catch (System.Exception)
                    {
                    }
                };
            }
        }
        """;

    private static string ToLiteral(string text) => Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(text, quote: true);

    internal static string AssemblyResolver(Dictionary<string, string> assemblyMap, IEnumerable<string> probeDirs)
    {
        var mapEntries = string.Join(",\n", assemblyMap.Select(kvp => $"        [{ToLiteral(kvp.Key)}] = {ToLiteral(kvp.Value)}"));
        var dirEntries = string.Join(",\n", probeDirs.Select(d => $"        {ToLiteral(d)}"));

        return $$"""
internal static class FryAssemblyResolver
{
    private static readonly System.Collections.Generic.Dictionary<string, string> AssemblyMap =
        new(System.StringComparer.OrdinalIgnoreCase)
        {
{{mapEntries}}
        };

    private static readonly string[] ProbeDirs = new string[]
    {
{{dirEntries}}
    };

    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Register()
    {
        System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (context, assemblyName) =>
        {
            var name = assemblyName.Name;
            if (string.IsNullOrEmpty(name)) return null;

            if (AssemblyMap.TryGetValue(name, out var exactPath) && System.IO.File.Exists(exactPath))
            {
                try
                {
                    return context.LoadFromAssemblyPath(exactPath);
                }
                catch
                {
                }
            }

            foreach (var dir in ProbeDirs)
            {
                var candidate = System.IO.Path.Combine(dir, name + ".dll");
                if (System.IO.File.Exists(candidate))
                {
                    try
                    {
                        return context.LoadFromAssemblyPath(candidate);
                    }
                    catch
                    {
                    }
                }
            }

            return null;
        };
    }
}
""";
    }
}


