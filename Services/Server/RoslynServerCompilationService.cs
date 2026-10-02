using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Models.Server;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Server;

public record CellCompilationResult(bool Success, IReadOnlyList<DiagnosticItem> Diagnostics, string? ErrorMessage = null);

public class RoslynServerCompilationService
{
    private record CachedCompilation(string Source, ScriptRunner<object?> Runner);
    private readonly ConcurrentDictionary<string, CachedCompilation> _compiledDelegates = new();
    private readonly ScriptOptions _scriptOptions;

    public RoslynServerCompilationService()
    {
        var references = RoslynCompilerService.SharedDefaultReferences;

        _scriptOptions = ScriptOptions.Default
            .WithReferences(references)
            .WithImports(ScriptImports.Namespaces)
            .AddImports("PdfEditorApp.Plugins.CSharpEditor.Services.Server")
            .WithAllowUnsafe(true);
    }

    /// <summary>
    /// Compiles a cell's source code and caches the resulting executable delegate.
    /// </summary>
    public async Task<CellCompilationResult> CompileCellAsync(FryServerCellItem cell, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cell.Source))
        {
            _compiledDelegates.TryRemove(cell.Id, out _);
            return new CellCompilationResult(true, Array.Empty<DiagnosticItem>());
        }

        try
        {
            var script = CSharpScript.Create<object?>(
                cell.Source,
                _scriptOptions,
                typeof(FryServerScriptGlobals));

            var diagnostics = script.Compile(cancellationToken);
            var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

            if (errors.Count > 0)
            {
                var diagItems = errors.Select(d => new DiagnosticItem
                {
                    Id = d.Id,
                    Message = d.GetMessage(),
                    Severity = DiagnosticSeverity.Error,
                    Line = d.Location.GetLineSpan().StartLinePosition.Line + 1,
                    Column = d.Location.GetLineSpan().StartLinePosition.Character + 1
                }).ToList();

                return new CellCompilationResult(false, diagItems, string.Join("; ", errors.Select(e => e.GetMessage())));
            }

            var runner = script.CreateDelegate();
            _compiledDelegates[cell.Id] = new CachedCompilation(cell.Source ?? string.Empty, runner);
            return new CellCompilationResult(true, Array.Empty<DiagnosticItem>());
        }
        catch (CompilationErrorException ex)
        {
            var items = ex.Diagnostics.Select(d => new DiagnosticItem
            {
                Id = d.Id,
                Message = d.GetMessage(),
                Severity = d.Severity,
                Line = d.Location.GetLineSpan().StartLinePosition.Line + 1,
                Column = d.Location.GetLineSpan().StartLinePosition.Character + 1
            }).ToList();

            return new CellCompilationResult(false, items, ex.Message);
        }
        catch (Exception ex)
        {
            return new CellCompilationResult(false, Array.Empty<DiagnosticItem>(), ex.Message);
        }
    }

    /// <summary>
    /// Invalidates cached compilation for a specific cell.
    /// </summary>
    public void InvalidateCell(string cellId)
    {
        _compiledDelegates.TryRemove(cellId, out _);
    }

    /// <summary>
    /// Clears all cached compiled cell delegates.
    /// </summary>
    public void ClearCache()
    {
        _compiledDelegates.Clear();
    }

    /// <summary>
    /// Executes a compiled cell with the provided execution context.
    /// </summary>
    public async Task<IServerResult> ExecuteCellAsync(FryServerCellItem cell, FryServerContext context, CancellationToken cancellationToken = default)
    {
        var currentSource = cell.Source ?? string.Empty;
        if (!_compiledDelegates.TryGetValue(cell.Id, out var cached) ||
            !string.Equals(cached.Source, currentSource, StringComparison.Ordinal))
        {
            var compileRes = await CompileCellAsync(cell, cancellationToken).ConfigureAwait(false);
            if (!compileRes.Success || !_compiledDelegates.TryGetValue(cell.Id, out cached))
            {
                return new StatusCodeResult(500, new
                {
                    error = "Compilation Error",
                    details = compileRes.ErrorMessage,
                    diagnostics = compileRes.Diagnostics
                });
            }
        }

        var runner = cached.Runner;
        var globals = new FryServerScriptGlobals { Context = context };

        try
        {
            var rawResult = await runner(globals, cancellationToken).ConfigureAwait(false);

            if (rawResult is IServerResult serverResult)
            {
                return serverResult;
            }

            if (rawResult == null)
            {
                return new StatusCodeResult(204); // No content
            }

            if (rawResult is string str)
            {
                return new TextResult(str, cell.DefaultStatusCode);
            }

            if (rawResult is byte[] bytes)
            {
                return new FileResult(bytes, cell.ResponseContentType);
            }

            // Default: serialize as JSON
            return new JsonResult(rawResult, cell.DefaultStatusCode);
        }
        catch (Exception ex)
        {
            var inner = ex is TargetInvocationException tie && tie.InnerException != null
                ? tie.InnerException
                : ex;

            return new StatusCodeResult(500, new
            {
                error = "Internal Server Error",
                message = inner.Message,
                type = inner.GetType().Name
            });
        }
    }

    /// <summary>
    /// Executes a startup cell to populate shared state.
    /// </summary>
    public async Task ExecuteStartupCellAsync(FryServerCellItem cell, IDictionary<string, object> state, CancellationToken cancellationToken = default)
    {
        var dummyContext = new FryServerContext("STARTUP", "/", null, null, null, string.Empty, null, state);
        _ = await ExecuteCellAsync(cell, dummyContext, cancellationToken).ConfigureAwait(false);
    }
}
