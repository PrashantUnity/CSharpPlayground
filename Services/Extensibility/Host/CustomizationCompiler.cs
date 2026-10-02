using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Host;

/// <summary>
/// Fast in-memory Roslyn compiler for C# customization scripts and extensions.
/// Injects standard namespaces and global 'App'/'Studio' ambient bindings.
/// </summary>
public class CustomizationCompiler
{
    private static readonly string ScriptHeader = """
        using System;
        using System.Collections;
        using System.Collections.Generic;
        using System.IO;
        using System.Linq;
        using System.Text;
        using System.Threading;
        using System.Threading.Tasks;
        using FrySharp.Sdk;
        using static PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Host.CustomizationScriptGlobals;
        #line 1 "customization.cs"

        """;

    public (bool Success, byte[]? AssemblyBytes, byte[]? PdbBytes, IReadOnlyList<DiagnosticItem> Diagnostics) Compile(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return (false, null, null, [new DiagnosticItem { Id = "CS0001", Message = "Customization script is empty.", Severity = DiagnosticSeverity.Error }]);
        }

        var allReferences = new List<MetadataReference>(RoslynCompilerService.SharedDefaultReferences);
        string effectiveSource = sourceCode;

        if (sourceCode.Contains("#r \"nuget:", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var resolver = new NuGetReferenceResolver();
                var nugetResult = resolver.ProcessDirectives(sourceCode);
                effectiveSource = nugetResult.SanitizedCode;
                allReferences.AddRange(nugetResult.References);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CustomizationCompiler] NuGet resolution warning: {ex.Message}");
            }
        }

        string fullCode = ScriptHeader + effectiveSource;
        var sourceText = Microsoft.CodeAnalysis.Text.SourceText.From(fullCode, Encoding.UTF8);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText, new CSharpParseOptions(LanguageVersion.CSharp13), path: "customization.cs");

        var compilation = CSharpCompilation.Create(
            $"FrySharpCustomization_{Guid.NewGuid():N}",
            syntaxTrees: [syntaxTree],
            references: allReferences,
            options: new CSharpCompilationOptions(
                OutputKind.ConsoleApplication,
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

    public IReadOnlyList<DiagnosticItem> CheckDiagnostics(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode)) return Array.Empty<DiagnosticItem>();

        string fullCode = ScriptHeader + sourceCode;
        var syntaxTree = CSharpSyntaxTree.ParseText(fullCode, new CSharpParseOptions(LanguageVersion.CSharp13));

        var compilation = CSharpCompilation.Create(
            $"FrySharpDiag_{Guid.NewGuid():N}",
            syntaxTrees: [syntaxTree],
            references: RoslynCompilerService.SharedDefaultReferences,
            options: new CSharpCompilationOptions(
                OutputKind.ConsoleApplication,
                optimizationLevel: OptimizationLevel.Debug,
                allowUnsafe: false));

        return MapDiagnostics(compilation.GetDiagnostics());
    }

    private static IReadOnlyList<DiagnosticItem> MapDiagnostics(IEnumerable<Diagnostic> diagnostics)
    {
        var results = new List<DiagnosticItem>();

        foreach (var diag in diagnostics)
        {
            if (diag.Severity == DiagnosticSeverity.Hidden) continue;

            var mappedSpan = diag.Location.GetMappedLineSpan();
            if (mappedSpan.IsValid && !string.IsNullOrEmpty(mappedSpan.Path) && mappedSpan.Path != "customization.cs")
            {
                continue;
            }

            var lineSpan = mappedSpan.IsValid ? mappedSpan : diag.Location.GetLineSpan();

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
