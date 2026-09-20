using Microsoft.CodeAnalysis.Scripting.Hosting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public class KernelExecutionResult
{
    public bool Success { get; set; }
    public string ConsoleOutput { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public TimeSpan Elapsed { get; set; }
    public bool WasCancelled { get; set; }
    public IReadOnlyList<DiagnosticItem> Diagnostics { get; set; } = Array.Empty<DiagnosticItem>();
}

public class NotebookExecutionKernel
{
    private ScriptState<object>? _currentState;
    private ScriptOptions _scriptOptions;
    private InteractiveAssemblyLoader _assemblyLoader = new();
    private readonly NuGetReferenceResolver _nuGetResolver;
    private readonly List<MetadataReference> _additionalReferences = new();

    // Per-instance: protects this kernel's own _currentState/_scriptOptions/_additionalReferences from
    // concurrent mutation (e.g. two Run clicks racing in the same tab). Each notebook tab owns its own
    // kernel, so this intentionally no longer serializes execution *across* tabs — that used to happen
    // by accident because this was `static`. Console redirection is handled separately by
    // ConsoleRoutingContext, which is AsyncLocal-scoped per execution and needs no lock at all.
    private readonly SemaphoreSlim _executionLock = new(1, 1);

    public bool IsSessionActive => _currentState != null;

    private static readonly Lazy<ScriptOptions> CachedDefaultScriptOptions = new(CreateDefaultScriptOptionsInternal);

    public static ScriptOptions SharedDefaultScriptOptions => CachedDefaultScriptOptions.Value;

    public static void Warmup()
    {
        _ = CachedDefaultScriptOptions.Value;
        ConsoleRoutingContext.EnsureInstalled();
    }

    public NotebookExecutionKernel()
    {
        _nuGetResolver = new NuGetReferenceResolver();
        _assemblyLoader = new InteractiveAssemblyLoader();
        RegisterCoreDependencies(_assemblyLoader);
        _scriptOptions = CachedDefaultScriptOptions.Value;
    }

    private static void RegisterCoreDependencies(InteractiveAssemblyLoader loader)
    {
        loader.RegisterDependency(typeof(Display).Assembly);
        loader.RegisterDependency(typeof(Control).Assembly);
        loader.RegisterDependency(typeof(Bitmap).Assembly);
        loader.RegisterDependency(typeof(System.Data.DataTable).Assembly);
    }

    private static ScriptOptions CreateDefaultScriptOptionsInternal()
    {
        var references = new List<MetadataReference>(RoslynCompilerService.SharedDefaultReferences);

        var defaultImports = new List<string>
        {
            "System",
            "System.IO",
            "System.Data",
            "System.Linq",
            "System.Collections",
            "System.Collections.Generic",
            "System.Text",
            "System.Text.Json",
            "System.Text.RegularExpressions",
            "System.Threading.Tasks",
            "Avalonia.Controls",
            "Avalonia.Media",
            "Avalonia.Media.Imaging",
            "Avalonia.Threading",
            "Avalonia.Animation",
            "PdfEditorApp.Plugins.CSharpEditor.Services",
            "PdfEditorApp.Plugins.CSharpEditor.Controls"
        };

        return ScriptOptions.Default
            .WithReferences(references)
            .WithImports(defaultImports);
    }

    public async Task<KernelExecutionResult> ExecuteCellAsync(
        string code,
        Action<string>? onLiveConsole = null,
        Action<RichCellOutput>? onRichOutput = null,
        CancellationToken ct = default)
    {
        var result = new KernelExecutionResult();
        var sw = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(code))
        {
            result.Success = true;
            return result;
        }

        // Everything below can throw OperationCanceledException before the innermost try/catch even
        // starts (e.g. an already-cancelled token failing the very first WaitAsync) — that must come
        // back as a normal WasCancelled result, not an unhandled exception out of this method.
        try
        {
            await ExecuteCellCoreAsync(code, result, sw, onLiveConsole, onRichOutput, ct);
        }
        catch (OperationCanceledException)
        {
            result.WasCancelled = true;
            result.ErrorMessage = "Execution was cancelled.";
            sw.Stop();
            result.Elapsed = sw.Elapsed;
        }

        return result;
    }

    private async Task ExecuteCellCoreAsync(
        string code,
        KernelExecutionResult result,
        Stopwatch sw,
        Action<string>? onLiveConsole,
        Action<RichCellOutput>? onRichOutput,
        CancellationToken ct)
    {
        // 1. Process #r "nuget: ..." directives
        var nugetResult = await _nuGetResolver.ProcessDirectivesAsync(code, ct);

        foreach (var msg in nugetResult.Messages)
        {
            onLiveConsole?.Invoke(msg + Environment.NewLine);
        }

        if (nugetResult.References.Count > 0)
        {
            _additionalReferences.AddRange(nugetResult.References);
            _scriptOptions = _scriptOptions.AddReferences(nugetResult.References);
            foreach (var r in nugetResult.References)
            {
                if (r is PortableExecutableReference per && !string.IsNullOrEmpty(per.FilePath) && File.Exists(per.FilePath))
                {
                    try
                    {
                        var asm = Assembly.LoadFrom(per.FilePath);
                        _assemblyLoader.RegisterDependency(asm);
                        _nuGetResolver.EnsureNativeAssetsResolved(asm, nugetResult);
                    }
                    catch { }
                }
            }
        }

        var cleanCode = nugetResult.SanitizedCode;

        await _executionLock.WaitAsync(ct);
        try
        {
            // 2. Intercept Console.Out and live writers. ConsoleRoutingContext routes Console.Out/
            // Error per-execution via an AsyncLocal scope (same pattern as InteractiveDisplayContext
            // just below) instead of a raw global swap — so unlike the old ConsoleRedirectionGate, this
            // never needs to hold a process-wide lock for the duration of the script run: a concurrent
            // Code Studio "Program" run or another tab's cell keeps its own output correctly isolated.
            var liveWriter = new KernelLiveStringWriter(text =>
            {
                onLiveConsole?.Invoke(text);
            });

            using (ConsoleRoutingContext.EnterScope(liveWriter))
            using (InteractiveCancellationContext.EnterScope(ct))
            {
                try
                {
                    using (InteractiveDisplayContext.EnterScope(richOutput =>
                    {
                        onRichOutput?.Invoke(richOutput);
                    }))
                    {
                        ScriptState<object> newState;

                        if (_currentState == null)
                        {
                            var script = CSharpScript.Create<object>(
                                cleanCode,
                                _scriptOptions,
                                assemblyLoader: _assemblyLoader);
                            newState = await script.RunAsync(cancellationToken: ct);
                        }
                        else
                        {
                            newState = await _currentState.ContinueWithAsync(
                                cleanCode,
                                _scriptOptions,
                                cancellationToken: ct);
                        }

                        _currentState = newState;
                        result.Success = true;

                        // Inspect return value for rich media or expression output
                        if (newState.ReturnValue != null)
                        {
                            InspectAndEmitReturnValue(newState.ReturnValue, onLiveConsole, onRichOutput);
                        }
                    }
                }
                catch (CompilationErrorException cee)
                {
                    result.Success = false;
                    var diags = new List<DiagnosticItem>();

                    foreach (var diag in cee.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
                    {
                        var lineSpan = diag.Location.GetLineSpan();
                        diags.Add(new DiagnosticItem
                        {
                            Id = diag.Id,
                            Message = diag.GetMessage(),
                            Severity = diag.Severity,
                            Line = lineSpan.StartLinePosition.Line + 1,
                            Column = lineSpan.StartLinePosition.Character + 1
                        });
                    }

                    result.Diagnostics = diags;
                    result.ErrorMessage = string.Join("\n", diags.Select(d => $"Line {d.Line}: {d.Message}"));
                    liveWriter.WriteLine($"\n❌ Compilation Error:\n{result.ErrorMessage}");
                }
                catch (OperationCanceledException)
                {
                    result.WasCancelled = true;
                    result.ErrorMessage = "Execution was cancelled.";
                    liveWriter.WriteLine("\n⚠️ Execution cancelled.");
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    var root = ex;
                    while (root.InnerException != null &&
                           (root is TargetInvocationException || root is TypeInitializationException || root is AggregateException))
                    {
                        root = root.InnerException;
                    }

                    result.ErrorMessage = root.Message;
                    liveWriter.WriteLine($"\n❌ Runtime Error: {root.GetType().Name}: {root.Message}\n{root.StackTrace}");
                    if (root != ex && ex.InnerException != null && ex.InnerException != root)
                    {
                        liveWriter.WriteLine($"\n(Root cause of {ex.GetType().Name}: {ex.Message})");
                    }
                }
                finally
                {
                    sw.Stop();
                    result.Elapsed = sw.Elapsed;
                    result.ConsoleOutput = liveWriter.ToString();
                }
            }
        }
        finally
        {
            _executionLock.Release();
        }
    }

    private void InspectAndEmitReturnValue(
        object returnValue,
        Action<string>? onLiveConsole,
        Action<RichCellOutput>? onRichOutput)
    {
        if (returnValue is Control control)
        {
            onRichOutput?.Invoke(new RichCellOutput
            {
                Kind = CellOutputKind.Control,
                InteractiveControl = control
            });
            return;
        }

        if (returnValue is Bitmap bitmap)
        {
            using var ms = new MemoryStream();
#pragma warning disable CS0618
            bitmap.Save(ms);
#pragma warning restore CS0618
            var bmpBytes = ms.ToArray();
            onRichOutput?.Invoke(new RichCellOutput
            {
                Kind = CellOutputKind.Image,
                ImageBytes = bmpBytes,
                ImageFormat = "PNG",
                ImageWidth = (int)bitmap.Size.Width,
                ImageHeight = (int)bitmap.Size.Height
            });
            return;
        }

        var typeName = returnValue.GetType().FullName ?? string.Empty;

        // SkiaSharp SKBitmap / SKImage / SKSurface
        if (typeName.Contains("SkiaSharp"))
        {
            try
            {
                var encodeMethod = returnValue.GetType().GetMethod("Encode", Type.EmptyTypes);
                if (encodeMethod != null)
                {
                    var data = encodeMethod.Invoke(returnValue, null);
                    if (data != null)
                    {
                        var toArray = data.GetType().GetMethod("ToArray");
                        if (toArray?.Invoke(data, null) is byte[] skBytes)
                        {
                            onRichOutput?.Invoke(new RichCellOutput
                            {
                                Kind = CellOutputKind.Image,
                                ImageBytes = skBytes,
                                ImageFormat = "PNG"
                            });
                            return;
                        }
                    }
                }
            }
            catch { }
        }

        if (returnValue is byte[] bytes && bytes.Length > 8 &&
            (bytes[0] == 0x89 && bytes[1] == 0x50 || bytes[0] == 0xFF && bytes[1] == 0xD8))
        {
            onRichOutput?.Invoke(new RichCellOutput
            {
                Kind = CellOutputKind.Image,
                ImageBytes = bytes,
                ImageFormat = "PNG"
            });
            return;
        }

        // Bare string that looks like an image base64 data URI
        if (returnValue is string str && str.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
        {
            onRichOutput?.Invoke(new RichCellOutput
            {
                Kind = CellOutputKind.Html,
                HtmlContent = str
            });
            return;
        }

        // Direct DumpTableResult
        if (returnValue is DumpTableResult dumpTable)
        {
            onRichOutput?.Invoke(new RichCellOutput
            {
                Kind = CellOutputKind.Table,
                TableResult = dumpTable
            });
            return;
        }

        // Dedicated Tabular Data (e.g. DataFrame, DataTable, DataView)
        if (DumpTableBuilder.IsTabularObject(returnValue, out var tabularTable))
        {
            onRichOutput?.Invoke(new RichCellOutput
            {
                Kind = CellOutputKind.Table,
                TableResult = tabularTable
            });
            return;
        }

        // 1. If it's a 1D collection of scalars (e.g. List<int> -> [ 1, 3, 4, 5, 6, 7, 8, 10 ])
        if (ObjectInspectorBuilder.IsCollectionOfScalars(returnValue, out var inlineFormatted))
        {
            onLiveConsole?.Invoke(inlineFormatted + Environment.NewLine);
            return;
        }

        // 2. If it's a scalar primitive / string
        if (ObjectInspectorBuilder.IsScalarType(returnValue.GetType()))
        {
            var formattedScalar = FormatValue(returnValue);
            onLiveConsole?.Invoke(formattedScalar + Environment.NewLine);
            return;
        }

        // 3. If it's a complex object (e.g. var people = new People(); people)
        try
        {
            var inspectorNode = ObjectInspectorBuilder.Build(returnValue);
            onRichOutput?.Invoke(new RichCellOutput
            {
                Kind = CellOutputKind.ObjectInspector,
                InspectorNode = inspectorNode
            });
            return;
        }
        catch
        {
            // Fallback to plain string representation
            var formatted = FormatValue(returnValue);
            onLiveConsole?.Invoke(formatted + Environment.NewLine);
        }
    }

    public IReadOnlyList<NotebookVariableInfo> GetActiveVariables()
    {
        if (_currentState == null)
        {
            return Array.Empty<NotebookVariableInfo>();
        }

        var variables = new List<NotebookVariableInfo>();

        foreach (var v in _currentState.Variables)
        {
            try
            {
                variables.Add(new NotebookVariableInfo
                {
                    Name = v.Name,
                    TypeName = v.Type?.Name ?? "var",
                    ValueDisplay = FormatValue(v.Value),
                    Kind = DetermineKind(v.Type, v.Value)
                });
            }
            catch
            {
                // Ignore evaluation errors
            }
        }

        return variables;
    }

    public void ResetSession()
    {
        _currentState = null;
        _additionalReferences.Clear();
        try { _assemblyLoader.Dispose(); } catch { }
        _assemblyLoader = new InteractiveAssemblyLoader();
        RegisterCoreDependencies(_assemblyLoader);
        _scriptOptions = CachedDefaultScriptOptions.Value;
    }

    private static string FormatValue(object? val)
    {
        if (val == null) return "null";
        if (val is string s) return $"\"{s}\"";
        if (val is bool or int or long or double or float or decimal or DateTime or TimeSpan or Guid)
        {
            return val.ToString() ?? "";
        }
        if (val is Control c) return $"<Avalonia.{c.GetType().Name}>";
        if (val is Bitmap b) return $"<Bitmap {b.Size.Width}x{b.Size.Height}>";

        var type = val.GetType();
        if (type.FullName?.Contains("SkiaSharp") == true)
        {
            return $"<{type.Name}>";
        }

        if (val is ICollection col)
        {
            return $"Count = {col.Count}";
        }

        return val.ToString() ?? type.Name;
    }

    private static string DetermineKind(Type? type, object? val)
    {
        if (type == null || val == null) return "Value";
        if (val is Control) return "UI Control";
        if (val is Bitmap || type.FullName?.Contains("SkiaSharp") == true) return "Image";
        if (val is IEnumerable and not string) return "Collection";
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime)) return "Primitive";
        return "Object";
    }

    private sealed class KernelLiveStringWriter : StringWriter
    {
        private readonly Action<string> _onWrite;
        [ThreadStatic] private static bool _isWriting;

        public KernelLiveStringWriter(Action<string> onWrite) => _onWrite = onWrite;

        public override void Write(char value)
        {
            base.Write(value);
            if (!_isWriting)
            {
                _isWriting = true;
                try { _onWrite(value.ToString()); }
                finally { _isWriting = false; }
            }
        }

        public override void Write(string? value)
        {
            base.Write(value);
            if (value != null && !_isWriting)
            {
                _isWriting = true;
                try { _onWrite(value); }
                finally { _isWriting = false; }
            }
        }

        public override void Write(char[] buffer, int index, int count)
        {
            base.Write(buffer, index, count);
            if (buffer != null && count > 0 && !_isWriting)
            {
                _isWriting = true;
                try { _onWrite(new string(buffer, index, count)); }
                finally { _isWriting = false; }
            }
        }
    }
}
