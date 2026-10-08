using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Execution;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Documentation;

/// <summary>
/// Runs a documentation sample where it is read, in the notebook kernel of its language. The kernels are made the first time
/// a sample of their language runs and are kept, so the next one starts at once. Samples run in a scratch folder, not in the
/// reader's workspace. Nothing here touches the UI thread: callers get their output on whatever thread the kernel uses.
/// </summary>
public sealed class SnippetRunService : IDisposable
{
    private readonly LanguageRegistry _registry;
    private readonly Lazy<NotebookKernelRouter> _router;
    private readonly string _scratchFolder = Path.Combine(Path.GetTempPath(), "frysharp-docs-run-" + Guid.NewGuid().ToString("N")[..8]);

    public SnippetRunService(LanguageRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _router = new Lazy<NotebookKernelRouter>(() =>
        {
            Directory.CreateDirectory(_scratchFolder);
            return new NotebookKernelRouter(_registry, new KernelCreationContext(() => _scratchFolder));
        });
    }

    /// <summary>True when a notebook cell can be written in <paramref name="languageId"/>, so a sample in it can run here.</summary>
    public bool CanRun(string? languageId)
    {
        var language = _registry.Get(languageId);
        return language?.NotebookKernels != null && language.Has(LanguageCapabilities.NotebookCells);
    }

    /// <summary>
    /// Runs <paramref name="code"/> and returns when it ends, is stopped, or (for C#, which runs inside the studio and can't
    /// be forced to stop) is given up on a few seconds after <paramref name="ct"/> was cancelled. Every language starts from
    /// a clean session each time, so a sample never depends on the one run before it.
    /// </summary>
    public async Task<KernelExecutionResult> RunAsync(string? languageId, string code, Action<string> onConsole, Action<RichCellOutput> onRichOutput, CancellationToken ct)
    {
        var language = _registry.Get(languageId);
        var kernel = CanRun(languageId) ? _router.Value.GetOrCreate(languageId) : null;
        if (language == null || kernel == null)
        {
            return new KernelExecutionResult { ErrorMessage = $"{language?.DisplayName ?? languageId} samples can't run here: open it in a studio." };
        }

        if (kernel is NotebookExecutionKernel csharp) return await RunCSharpAsync(csharp, code, onConsole, onRichOutput, ct);

        // Another language's kernel keeps what a cell declared (a second run of "const { Display } = ..." is an error there):
        // the kernel a sample ran in before is forgotten, so a sample, like a C# one, never depends on the one run before it.
        bool used;
        lock (_used) used = !_used.Add(language.Id);
        if (used) kernel.HardReset();

        return await kernel.ExecuteAsync(new KernelExecutionRequest { Code = code, OnConsole = onConsole, OnRichOutput = onRichOutput }, ct);
    }

    private readonly HashSet<string> _used = new(StringComparer.OrdinalIgnoreCase);

    private static async Task<KernelExecutionResult> RunCSharpAsync(NotebookExecutionKernel kernel, string code, Action<string> onConsole, Action<RichCellOutput> onRichOutput, CancellationToken ct)
    {
        kernel.ResetSession();

        // Off the caller's thread: a script runs inline until its first await, and one that blocks must not block the UI.
        var running = Task.Run(() => kernel.ExecuteCellAsync(code, onConsole, onRichOutput, ct));
        if (await ExecutionAbandonment.WaitWithGraceAsync(running, ct)) return await running;

        // Stop didn't get through (a statement blocked mid-call): leave it running, and give the kernel back to the next sample.
        _ = running.ContinueWith(t => _ = t.Exception, TaskScheduler.Default);
        kernel.HardReset();
        return new KernelExecutionResult { WasCancelled = true, ErrorMessage = "The sample did not respond to Stop and was abandoned." };
    }

    public void Dispose()
    {
        if (_router.IsValueCreated) _router.Value.Dispose();
        try
        {
            if (Directory.Exists(_scratchFolder)) Directory.Delete(_scratchFolder, recursive: true);
        }
        catch
        {
        }
    }
}
