using System.Diagnostics;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;

/// <summary>
/// The kernels of one notebook, one per language, like Polyglot Notebooks' composite kernel: a cell runs in its
/// language's kernel, which is created the first time a cell of that language runs and keeps its own state.
/// </summary>
public sealed class NotebookKernelRouter : IDisposable
{
    private readonly LanguageRegistry _registry;
    private readonly KernelCreationContext _context;
    private readonly INotebookKernel? _primary;
    private readonly object _gate = new();
    private readonly Dictionary<string, INotebookKernel> _kernels = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="primary">A kernel the notebook already has (C#), which stays alive across <see cref="ShutdownOthers"/>.</param>
    public NotebookKernelRouter(LanguageRegistry registry, KernelCreationContext context, INotebookKernel? primary = null)
    {
        _registry = registry;
        _context = context;
        _primary = primary;
        if (primary != null) _kernels[primary.LanguageId] = primary;
    }

    public LanguageRegistry Registry => _registry;

    /// <summary>The kernels created so far.</summary>
    public IReadOnlyList<INotebookKernel> Kernels
    {
        get
        {
            lock (_gate) return _kernels.Values.ToArray();
        }
    }

    /// <summary>The kernel for a language, created on first use; null when the language can't run notebook cells.</summary>
    public INotebookKernel? GetOrCreate(string? languageId)
    {
        var language = _registry.Get(languageId);
        if (language?.NotebookKernels == null || !language.Has(LanguageCapabilities.NotebookCells)) return null;

        lock (_gate)
        {
            if (_kernels.TryGetValue(language.Id, out var existing)) return existing;
            var kernel = language.NotebookKernels.Create(_context);
            _kernels[language.Id] = kernel;
            return kernel;
        }
    }

    /// <summary>The language's kernel if one has been created, without creating it.</summary>
    public INotebookKernel? Find(string? languageId)
    {
        var language = _registry.Get(languageId);
        if (language == null) return null;
        lock (_gate) return _kernels.TryGetValue(language.Id, out var kernel) ? kernel : null;
    }

    /// <summary>Clears every kernel's state (Run All, Restart).</summary>
    public void ResetAll()
    {
        foreach (var kernel in Kernels) kernel.HardReset();
    }

    /// <summary>Ends every kernel but the primary one; they start again when a cell of theirs next runs (tab closed).</summary>
    public void ShutdownOthers()
    {
        INotebookKernel[] others;
        lock (_gate)
        {
            others = _kernels.Values.Where(k => !ReferenceEquals(k, _primary)).ToArray();
            foreach (var kernel in others) _kernels.Remove(kernel.LanguageId);
        }

        foreach (var kernel in others)
        {
            try
            {
                kernel.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CSharpEditorPlugin] Couldn't shut down the {kernel.DisplayName} kernel: {ex.Message}");
            }
        }
    }

    /// <summary>Every kernel's variables, each tagged with its language.</summary>
    public async Task<IReadOnlyList<NotebookVariableInfo>> GetVariablesAsync(CancellationToken ct = default)
    {
        var all = new List<NotebookVariableInfo>();
        foreach (var kernel in Kernels.Where(k => k.IsSessionActive))
        {
            try
            {
                var tag = _registry.Get(kernel.LanguageId)?.DisplayName ?? kernel.LanguageId;
                foreach (var variable in await kernel.GetVariablesAsync(ct))
                {
                    variable.Kernel = tag;
                    all.Add(variable);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Debug.WriteLine($"[CSharpEditorPlugin] Couldn't list the {kernel.DisplayName} kernel's variables: {ex.Message}");
            }
        }

        return all;
    }

    public void Dispose() => ShutdownOthers();
}
