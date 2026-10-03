using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Extensions;

/// <summary>
/// Compiles multi-file C# extension packages into in-memory assemblies.
/// </summary>
public class ExtensionCompiler
{
    public (bool Success, byte[]? AssemblyBytes, byte[]? PdbBytes, IReadOnlyList<DiagnosticItem> Diagnostics) CompileDirectory(
        string extensionDirectory,
        IEnumerable<string>? specificFiles = null)
    {
        if (!Directory.Exists(extensionDirectory))
        {
            return (false, null, null, [new DiagnosticItem { Id = "EXT001", Message = $"Directory not found: {extensionDirectory}", Severity = DiagnosticSeverity.Error }]);
        }

        var files = specificFiles != null
            ? specificFiles.Select(f => Path.IsPathRooted(f) ? f : Path.Combine(extensionDirectory, f)).Where(File.Exists).ToList()
            : Directory.GetFiles(extensionDirectory, "*.cs", SearchOption.AllDirectories).ToList();

        if (files.Count == 0)
        {
            return (false, null, null, [new DiagnosticItem { Id = "EXT002", Message = "No C# source files found in extension package.", Severity = DiagnosticSeverity.Error }]);
        }

        var syntaxTrees = new List<SyntaxTree>();
        var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp13);

        foreach (var file in files)
        {
            string source = File.ReadAllText(file);
            var sourceText = Microsoft.CodeAnalysis.Text.SourceText.From(source, System.Text.Encoding.UTF8);
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(sourceText, parseOptions, path: file));
        }

        string assemblyName = $"FrySharpExt_{Path.GetFileName(extensionDirectory)}_{Guid.NewGuid():N}";

        var allReferences = new List<MetadataReference>(RoslynCompilerService.SharedDefaultReferences);
        var existingPaths = new HashSet<string>(allReferences.OfType<PortableExecutableReference>().Select(r => r.FilePath).Where(p => p != null)!, StringComparer.OrdinalIgnoreCase);

        // Include any assemblies from the host application directory (e.g. Material.Icons, AvaloniaEdit)
        var hostDir = AppContext.BaseDirectory;
        if (!string.IsNullOrEmpty(hostDir) && Directory.Exists(hostDir))
        {
            foreach (var dll in Directory.GetFiles(hostDir, "*.dll"))
            {
                try
                {
                    if (existingPaths.Add(dll))
                    {
                        allReferences.Add(MetadataReference.CreateFromFile(dll));
                    }
                }
                catch { }
            }
        }

        var libDir = Path.Combine(extensionDirectory, "lib");
        if (Directory.Exists(libDir))
        {
            foreach (var dll in Directory.GetFiles(libDir, "*.dll", SearchOption.AllDirectories))
            {
                try
                {
                    if (existingPaths.Add(dll))
                    {
                        allReferences.Add(MetadataReference.CreateFromFile(dll));
                    }
                }
                catch { }
            }
        }

        var compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees: syntaxTrees,
            references: allReferences,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Debug,
                allowUnsafe: false));

        using var peStream = new MemoryStream();
        using var pdbStream = new MemoryStream();
        var emitResult = compilation.Emit(peStream, pdbStream);

        var diagnostics = MapDiagnostics(emitResult.Diagnostics);

        if (!emitResult.Success)
        {
            return (false, null, null, diagnostics);
        }

        return (true, peStream.ToArray(), pdbStream.ToArray(), diagnostics);
    }

    private static IReadOnlyList<DiagnosticItem> MapDiagnostics(IEnumerable<Diagnostic> diagnostics)
    {
        var results = new List<DiagnosticItem>();

        foreach (var diag in diagnostics)
        {
            if (diag.Severity == DiagnosticSeverity.Hidden) continue;

            var lineSpan = diag.Location.GetLineSpan();
            results.Add(new DiagnosticItem
            {
                Id = diag.Id,
                Message = diag.GetMessage(),
                Severity = diag.Severity,
                Line = lineSpan.StartLinePosition.Line + 1,
                Column = lineSpan.StartLinePosition.Character + 1,
                EndLine = lineSpan.EndLinePosition.Line + 1,
                EndColumn = lineSpan.EndLinePosition.Character + 1
            });
        }

        return results.OrderByDescending(d => d.Severity).ThenBy(d => d.Line).ToList();
    }
}
