using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Scripting;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public class ScriptDebuggerService
{
    private readonly RoslynCompilerService _compilerService;
    private readonly ScriptExecutionEngine _executionEngine;

    public ScriptDebuggerService(RoslynCompilerService compilerService, ScriptExecutionEngine executionEngine)
    {
        _compilerService = compilerService;
        _executionEngine = executionEngine;
    }

    public (bool Success, byte[]? AssemblyBytes, IReadOnlyList<DiagnosticItem> Diagnostics) CompileForDebugging(
        string rawSourceCode,
        ExecutionLanguageMode mode)
    {
        if (string.IsNullOrWhiteSpace(rawSourceCode))
        {
            return (false, null, [new DiagnosticItem { Id = "DBG001", Message = "Source code is empty.", Severity = DiagnosticSeverity.Error }]);
        }

        // 1. Wrap source code (with line mappings to "script.cs")
        var wrappedCode = _compilerService.WrapSourceCode(rawSourceCode, mode);
        var syntaxTree = CSharpSyntaxTree.ParseText(wrappedCode);

        // 2. Check for compile errors before instrumentation
        var preCompilation = CSharpCompilation.Create(
            $"PreCheck_{Guid.NewGuid():N}",
            syntaxTrees: [syntaxTree],
            references: _compilerService.DefaultReferences,
            options: new CSharpCompilationOptions(OutputKind.ConsoleApplication, optimizationLevel: OptimizationLevel.Debug));

        var preDiags = preCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        if (preDiags.Count > 0)
        {
            var diagnosticItems = preDiags.Select(d =>
            {
                var mapped = d.Location.GetMappedLineSpan();
                var span = mapped.IsValid ? mapped : d.Location.GetLineSpan();
                return new DiagnosticItem
                {
                    Id = d.Id,
                    Message = d.GetMessage(),
                    Severity = d.Severity,
                    Line = span.StartLinePosition.Line + 1,
                    Column = span.StartLinePosition.Character + 1
                };
            }).ToList();

            return (false, null, diagnosticItems);
        }

        // 3. Instrument the syntax tree with debug probes using semantic model for symbol verification
        var semanticModel = preCompilation.GetSemanticModel(syntaxTree);
        var instrumentedTree = DebugInstrumentationRewriter.Instrument(syntaxTree, semanticModel);

        // 4. Compile instrumented code with Debug optimizations
        var compilation = CSharpCompilation.Create(
            $"ScriptDebug_{Guid.NewGuid():N}",
            syntaxTrees: [instrumentedTree],
            references: _compilerService.DefaultReferences,
            options: new CSharpCompilationOptions(
                OutputKind.ConsoleApplication,
                optimizationLevel: OptimizationLevel.Debug,
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
                    Column = span.StartLinePosition.Character + 1
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

    public async Task<ExecutionResult> StartDebuggingSessionAsync(
        byte[] assemblyBytes,
        IEnumerable<BreakpointItem> breakpoints,
        CancellationTokenSource cts,
        Action<string>? onLiveOutput = null,
        string? scriptId = null)
    {
        var session = ScriptDebugSession.BeginSession(breakpoints, cts, scriptId);

        try
        {
            var result = await _executionEngine.ExecuteAsync(
                assemblyBytes,
                onLiveOutput,
                cts.Token);

            return result;
        }
        finally
        {
            ScriptDebugSession.EndSession();
        }
    }

    public async Task<(bool Success, string Result, string TypeName)> EvaluateExpressionAsync(
        string expression,
        IReadOnlyList<DebugVariableItem> locals,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return (false, "Expression cannot be empty", "");
        }

        try
        {
            var options = ScriptOptions.Default
                .WithReferences(_compilerService.DefaultReferences)
                .WithImports(
                    "System",
                    "System.IO",
                    "System.Linq",
                    "System.Collections",
                    "System.Collections.Generic",
                    "System.Text",
                    "System.Text.Json",
                    "System.Text.RegularExpressions",
                    "System.Threading.Tasks",
                    "PdfEditorApp.Plugins.CSharpEditor.Services");

            // Synthesize local variable declarations if any are in scope
            var prefix = new StringBuilder();
            foreach (var local in locals)
            {
                if (local.RawValue != null)
                {
                    // Store in a global lookup dictionary for safe evaluation
                    GlobalVariableCache.Set(local.Name, local.RawValue);
                    prefix.AppendLine($"var {local.Name} = (dynamic)PdfEditorApp.Plugins.CSharpEditor.Services.GlobalVariableCache.Get(\"{local.Name}\");");
                }
            }

            var codeToEval = prefix.Length > 0
                ? $"{prefix}\n({expression})"
                : expression;

            var evalResult = await CSharpScript.EvaluateAsync(codeToEval, options, cancellationToken: ct);

            if (evalResult == null)
            {
                return (true, "null", "null");
            }

            var typeName = evalResult.GetType().Name;
            var display = evalResult is string s ? $"\"{s}\"" : evalResult.ToString() ?? typeName;
            return (true, display, typeName);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, "");
        }
    }
}

public static class GlobalVariableCache
{
    private static readonly Dictionary<string, object> _cache = new();

    public static void Set(string name, object value)
    {
        lock (_cache) { _cache[name] = value; }
    }

    public static object? Get(string name)
    {
        lock (_cache) { return _cache.TryGetValue(name, out var val) ? val : null; }
    }

    public static void Clear()
    {
        lock (_cache) { _cache.Clear(); }
    }
}
