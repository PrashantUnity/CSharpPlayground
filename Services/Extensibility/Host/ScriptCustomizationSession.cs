using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Host;

public class CustomizationExecutionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public IReadOnlyList<DiagnosticItem> Diagnostics { get; set; } = Array.Empty<DiagnosticItem>();
    public TimeSpan CompileDuration { get; set; }
    public TimeSpan ExecutionDuration { get; set; }
}

/// <summary>
/// Manages the lifecycle, compilation, execution, and collectible ALC unloading
/// of active user customization scripts. Supports hot-reloading scripts live.
/// </summary>
public class ScriptCustomizationSession : IDisposable
{
    private readonly CustomizationCompiler _compiler = new();
    private ExtensionLoadContext? _activeContext;
    private LifetimeRegistrationBag? _activeBag;
    private readonly object _lock = new();

    public bool HasActiveCustomization => _activeContext != null;

    public async Task<CustomizationExecutionResult> ApplyScriptAsync(string sourceCode, CancellationToken ct = default)
    {
        var result = new CustomizationExecutionResult();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // 1. Compile in background
        var (compileSuccess, peBytes, pdbBytes, diagnostics) = await Task.Run(() => _compiler.Compile(sourceCode), ct);
        sw.Stop();
        result.CompileDuration = sw.Elapsed;
        result.Diagnostics = diagnostics;

        if (!compileSuccess || peBytes == null)
        {
            result.Success = false;
            result.ErrorMessage = "Compilation failed with errors.";
            return result;
        }

        // 2. Unload previous session & instantiate new collectible context
        lock (_lock)
        {
            UnloadInternal();

            _activeBag = new LifetimeRegistrationBag();
            _activeContext = new ExtensionLoadContext();
        }

        // 3. Execute entrypoint
        sw.Restart();
        try
        {
            await Task.Run(() =>
            {
                using var peStream = new MemoryStream(peBytes);
                using var pdbStream = pdbBytes != null ? new MemoryStream(pdbBytes) : null;

                var assembly = pdbStream != null
                    ? _activeContext.LoadFromStream(peStream, pdbStream)
                    : _activeContext.LoadFromStream(peStream);

                var entryPoint = assembly.EntryPoint;
                if (entryPoint == null)
                {
                    throw new InvalidOperationException("No entry point found in customization script.");
                }

                ct.ThrowIfCancellationRequested();

                var parameters = entryPoint.GetParameters();
                object?[]? args = null;
                if (parameters.Length > 0 && parameters[0].ParameterType == typeof(string[]))
                {
                    args = [Array.Empty<string>()];
                }

                var returnVal = entryPoint.Invoke(null, args);
                if (returnVal is Task task)
                {
                    task.GetAwaiter().GetResult();
                }
            }, ct);

            sw.Stop();
            result.ExecutionDuration = sw.Elapsed;
            result.Success = true;
        }
        catch (TargetInvocationException tie)
        {
            sw.Stop();
            result.ExecutionDuration = sw.Elapsed;
            result.Success = false;
            var inner = tie.InnerException ?? tie;
            result.ErrorMessage = $"{inner.GetType().Name}: {inner.Message}";
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.ExecutionDuration = sw.Elapsed;
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    public void Unload()
    {
        lock (_lock)
        {
            UnloadInternal();
        }
    }

    private void UnloadInternal()
    {
        _activeBag?.Dispose();
        _activeBag = null;

        if (_activeContext != null)
        {
            try
            {
                _activeContext.Unload();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScriptCustomizationSession] Unload error: {ex.Message}");
            }
            _activeContext = null;
        }
    }

    public void Dispose()
    {
        Unload();
    }
}
