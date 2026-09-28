using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using PdfEditorApp.Plugins.CSharpEditor.Models;

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
        ExecutionLanguageMode mode = ExecutionLanguageMode.Statements)
    {
        if (string.IsNullOrWhiteSpace(rawSourceCode))
        {
            return (false, null, [new DiagnosticItem { Id = "DBG001", Message = "Source code is empty.", Severity = DiagnosticSeverity.Error }]);
        }

        Directory.CreateDirectory(outputDirectory);

        var wrappedCode = _compilerService.WrapSourceCode(rawSourceCode, mode);
        var sourceText = Microsoft.CodeAnalysis.Text.SourceText.From(wrappedCode, Encoding.UTF8);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText, path: "script.cs");

        var compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees: [syntaxTree],
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
}
