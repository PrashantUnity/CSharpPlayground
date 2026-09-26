using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

/// <summary>
/// A notebook of many languages, as in Polyglot Notebooks: each code cell runs in its language's kernel (the tab's C#
/// kernel, or one the language's module starts), after the directives at its top: <c>#!share</c> copies a value in
/// from another kernel, and package lines such as <c>%pip install numpy</c> install what the cell needs.
/// </summary>
public partial class NotebookTabViewModel
{
    private static readonly TimeSpan VariablesPatience = TimeSpan.FromSeconds(5);

    private readonly StudioLanguageServices _languages;
    private readonly Func<string?>? _workspaceRoot;
    private readonly NotebookKernelRouter _router;

    public StudioLanguageServices Languages => _languages;

    /// <summary>The kernels this notebook has started, the C# one first.</summary>
    public IReadOnlyList<INotebookKernel> Kernels => _router.Kernels;

    /// <summary>
    /// The folder the notebook's kernels run in (their working directory and first import path): the notebook's own
    /// folder, or the workspace's for one that isn't saved in a folder yet.
    /// </summary>
    public string? WorkingFolder
    {
        get
        {
            if (Path.IsPathRooted(FilePath) && Path.GetDirectoryName(FilePath) is { Length: > 0 } folder) return folder;
            return _workspaceRoot?.Invoke();
        }
    }

    /// <summary>The language new cells get and cells without one of their own run in (the notebook's "kernel").</summary>
    public string DefaultLanguage =>
        _languages.Registry.Get(Notebook.Kernel) is { } language && IsNotebookLanguage(language) ? language.Id : LanguageIds.CSharp;

    public ILanguageDefinition? DefaultLanguageDefinition => _languages.Registry.Get(DefaultLanguage);

    /// <summary>True when more than one language can run in notebooks, so there's a choice to offer.</summary>
    public bool HasLanguageChoices => _languages.Registry.NotebookLanguages.Count > 1;

    /// <summary>The languages the notebook's default can be set to, the current one ticked.</summary>
    public IReadOnlyList<CellLanguageChoice> DefaultLanguageChoices =>
        _languages.Registry.NotebookLanguages.Select(l => new CellLanguageChoice(
            l.Id, l.DisplayName, l.IconKind, l.AccentHex,
            string.Equals(l.Id, DefaultLanguage, StringComparison.OrdinalIgnoreCase),
            new RelayCommand(() => SetDefaultLanguage(l.Id)))).ToList();

    /// <summary>
    /// Makes <paramref name="languageId"/> the notebook's default. Cells keep the language they have: those that
    /// followed the old default get it as their own first.
    /// </summary>
    [RelayCommand]
    public void SetDefaultLanguage(string? languageId)
    {
        if (_languages.Registry.Get(languageId) is not { } language || !IsNotebookLanguage(language)) return;
        if (language.Id == DefaultLanguage) return;

        foreach (var cell in Cells.Where(c => c.IsCodeCell && c.Language == null)) cell.Language = DefaultLanguage;
        Notebook.Kernel = language.Id;
        foreach (var cell in Cells.Where(c => string.Equals(c.Language, language.Id, StringComparison.OrdinalIgnoreCase))) cell.Language = null;
        foreach (var cell in Cells) cell.NotifyLanguageChanged(force: true);

        IsModified = true;
        OnPropertyChanged(nameof(DefaultLanguage));
        OnPropertyChanged(nameof(DefaultLanguageDefinition));
        OnPropertyChanged(nameof(DefaultLanguageChoices));
        RefreshKernelName();
    }

    /// <summary>Ends the kernels running in their own programs (the tab is closing); they start again when needed.</summary>
    public void ShutdownKernels() => _router.ShutdownOthers();

    /// <summary>The kernels' names for the header, e.g. ".NET (C#) · Python 3.14.6": the default language's first, then those the cells use.</summary>
    private void RefreshKernelName()
    {
        var used = new List<string> { DefaultLanguage };
        foreach (var cell in Cells.Where(c => c.IsCodeCell))
        {
            var language = cell.EffectiveLanguage;
            if (!used.Contains(language, StringComparer.OrdinalIgnoreCase)) used.Add(language);
        }

        KernelName = string.Join(" · ", used
            .Select(id => _languages.Registry.Get(id))
            .Where(l => l != null && IsNotebookLanguage(l))
            .Select(l => _router.Find(l!.Id)?.DisplayName ?? l!.DisplayName));
    }

    private static bool IsNotebookLanguage(ILanguageDefinition language) =>
        language.NotebookKernels != null && language.Has(LanguageCapabilities.NotebookCells);

    // A new cell is written in the language of the cell it's added next to (or the notebook's default).
    private NotebookCellItem NewCellItem(NotebookCellViewModel? nextTo, CellType type)
    {
        if (type != CellType.Code)
        {
            return new NotebookCellItem { Type = type, Source = "### Markdown Notes\nWrite documentation here." };
        }

        var languageId = (nextTo is { IsCodeCell: true } ? nextTo.EffectiveLanguage : null) ?? DefaultLanguage;
        var language = _languages.Registry.Get(languageId) ?? _languages.CSharp;
        return new NotebookCellItem
        {
            Type = CellType.Code,
            Source = $"{language.LineCommentPrefix} {language.DisplayName} Code Block\n",
            Language = string.Equals(language.Id, DefaultLanguage, StringComparison.OrdinalIgnoreCase) ? null : language.Id
        };
    }

    // Best-effort static approximation of the kernel's real chained ScriptState: cells are assumed
    // to run top-to-bottom, so completion sees every code cell above the active one regardless of
    // whether it has actually been run yet. That matches the common "write several cells, then run"
    // workflow; if cells are run out of order, completion may suggest a variable that isn't in scope
    // yet at runtime — the same static-analysis tradeoff every notebook IDE completion makes.
    // Only cells of the same language count, with their directive lines blanked (#!share isn't C#).
    private string GetPrecedingCodeContext(NotebookCellViewModel cell)
    {
        var idx = Cells.IndexOf(cell);
        if (idx <= 0) return string.Empty;

        var language = cell.EffectiveLanguage;
        return string.Join(
            "\n",
            Cells.Take(idx)
                .Where(c => c.Type == CellType.Code && !string.IsNullOrWhiteSpace(c.Source) &&
                            string.Equals(c.EffectiveLanguage, language, StringComparison.OrdinalIgnoreCase))
                .Select(c => NotebookCellDirectives.Parse(c.Source, _languages.Registry, c.Language ?? DefaultLanguage).Code));
    }

    /// <summary>
    /// Runs a cell's directives before its code: package commands, then <c>#!share</c>s. False when one failed (the
    /// cell's output says why), so the code doesn't run.
    /// </summary>
    private async Task<bool> RunDirectivesAsync(CellDirectives directives, ILanguageDefinition language, INotebookKernel kernel, CellConsole console, CancellationToken ct)
    {
        foreach (var command in directives.PackageCommands)
        {
            if (!await RunPackageCommandAsync(language, command, kernel, console, ct)) return false;
        }

        foreach (var share in directives.Shares)
        {
            if (!await ShareAsync(share, language, kernel, console, ct)) return false;
        }

        return true;
    }

    private async Task<bool> ShareAsync(ShareDirective share, ILanguageDefinition language, INotebookKernel kernel, CellConsole console, CancellationToken ct)
    {
        var from = _languages.Registry.Get(share.FromLanguage);
        string? problem = null;
        if (from == null || !IsNotebookLanguage(from))
        {
            problem = $"There's no notebook language called \"{share.FromLanguage}\". Use {string.Join(" or ", _languages.Registry.NotebookLanguages.Select(l => l.Id))}.";
        }
        else if (!from.Has(LanguageCapabilities.ValueSharing) || !language.Has(LanguageCapabilities.ValueSharing))
        {
            problem = $"{(from.Has(LanguageCapabilities.ValueSharing) ? language.DisplayName : from.DisplayName)} cells can't share values.";
        }
        else if (ReferenceEquals(from, language))
        {
            problem = $"'{share.Name}' is already a {language.DisplayName} value: #!share copies from another language's cells.";
        }
        else if (_router.Find(from.Id) is not { IsSessionActive: true } source)
        {
            problem = $"No {from.DisplayName} cell has run yet, so there's no '{share.Name}' to copy. Run the {from.DisplayName} cell that sets it first.";
        }
        else
        {
            try
            {
                var json = await source.GetValueJsonAsync(share.Name, ct);
                await kernel.SetValueFromJsonAsync(share.TargetName, json, ct);
                return true;
            }
            catch (KernelValueException ex)
            {
                problem = ex.Message;
            }
        }

        console.Write($"❌ #!share (line {share.LineNumber}): {problem}\n");
        return false;
    }

    private async Task<bool> RunPackageCommandAsync(ILanguageDefinition language, PackageCommand command, INotebookKernel kernel, CellConsole console, CancellationToken ct)
    {
        if (language.Packages is not { } packages || language.Toolchain is not { } toolchains)
        {
            console.Write($"❌ {language.DisplayName} cells can't install packages.\n");
            return false;
        }

        var resolution = await toolchains.ResolveAsync(ToolchainQuery(), ct);
        if (resolution.Toolchain is not { } toolchain)
        {
            console.Write((resolution.Missing?.ToText() ?? $"{toolchains.ToolName} wasn't found.") + "\n");
            return false;
        }

        console.Write($"▶ {command.Text}\n");
        var result = await packages.RunAsync(command, toolchain, console.Write, ct);
        if (result.Message is { Length: > 0 } message) console.Write((result.Success ? "" : "❌ ") + message.TrimEnd() + "\n");

        // A new environment only takes over when the kernel next starts; until then the running one looks there too.
        if (result.Success && result.AddedSearchPath is { } searchPath && kernel is ISearchPathKernel searchable && kernel.IsSessionActive)
        {
            await searchable.AddSearchPathAsync(searchPath, ct);
        }

        return result.Success;
    }

    /// <summary>Installs the package a cell's last run was missing, then runs the cell again.</summary>
    public async Task InstallMissingDependencyAsync(NotebookCellViewModel cell)
    {
        if (IsExecuting || cell.MissingDependency is not { } package) return;
        var language = _languages.Registry.Get(cell.EffectiveLanguage);
        if (language?.Packages is not { } packages || _router.GetOrCreate(language.Id) is not { } kernel) return;

        IsExecuting = true;
        cell.IsExecuting = true;
        cell.ClearOutput();
        KernelStatusText = $"Installing {package}...";
        _executionCts?.Cancel();
        _executionCts?.Dispose();
        _executionCts = new CancellationTokenSource();
        var runId = ++_executionRunId;
        var console = new CellConsole(cell, () => runId == _executionRunId, rich => ApplyRichOutput(cell, rich, runId), terminal: true);
        bool installed;
        try
        {
            installed = await RunPackageCommandAsync(language, packages.InstallCommand(package), kernel, console, _executionCts.Token);
        }
        catch (OperationCanceledException)
        {
            installed = false;
        }
        finally
        {
            console.Flush();
            cell.IsExecuting = false;
            IsExecuting = false;
        }

        if (installed)
        {
            await RunSingleCellAsync(cell);
        }
        else
        {
            cell.HasError = true;
            KernelStatusText = $"Couldn't install {package}";
        }
    }

    private ToolchainQuery ToolchainQuery() =>
        new(WorkingFolder is { } folder && Directory.Exists(folder) ? folder : _languages.Host.HomeDirectory, _workspaceRoot?.Invoke());

    /// <summary>Every kernel's variables for the Variables panel.</summary>
    public async Task UpdateVariablesAsync()
    {
        IReadOnlyList<NotebookVariableInfo> active;
        try
        {
            using var patience = new CancellationTokenSource(VariablesPatience);
            active = await _router.GetVariablesAsync(patience.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        void ApplyVars()
        {
            Variables.Clear();
            foreach (var v in active)
            {
                Variables.Add(v);
            }
            OnPropertyChanged(nameof(Variables));
        }

        if (Avalonia.Application.Current == null || Dispatcher.UIThread.CheckAccess())
        {
            ApplyVars();
        }
        else
        {
            Dispatcher.UIThread.Post(ApplyVars);
        }
    }

    /// <summary>
    /// A run's output into its cell. Console text from a kernel in its own program shows as a terminal would (progress
    /// bars redraw in place, colors are dropped, very long output keeps its end); the C# kernel's is appended as always.
    /// Output arrives on other threads and is shown on the UI thread; <see cref="Flush"/> shows the rest as the run
    /// ends, before the next cell starts (otherwise its last table or image could arrive "late" and be dropped). Output
    /// from a run the cell has moved on from is dropped.
    /// </summary>
    private sealed class CellConsole(NotebookCellViewModel cell, Func<bool> isCurrent, Action<RichCellOutput> showRich, bool terminal)
    {
        private readonly TerminalTextBuffer? _buffer = terminal ? new TerminalTextBuffer() : null;
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _appends = new();
        private readonly System.Collections.Concurrent.ConcurrentQueue<RichCellOutput> _rich = new();
        private int _refreshQueued;

        public void Write(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (_buffer != null)
            {
                lock (_buffer) _buffer.Append(text);
            }
            else
            {
                _appends.Enqueue(text);
            }

            Refresh();
        }

        public void Show(RichCellOutput output)
        {
            _rich.Enqueue(output);
            Refresh();
        }

        /// <summary>Shows everything written so far, now (the run has finished). Call on the UI thread.</summary>
        public void Flush()
        {
            if (!isCurrent())
            {
                _appends.Clear();
                _rich.Clear();
                return;
            }

            if (_buffer != null)
            {
                lock (_buffer) cell.OutputText = _buffer.Text;
            }
            else
            {
                while (_appends.TryDequeue(out var text)) cell.OutputText += text;
            }

            while (_rich.TryDequeue(out var output)) showRich(output);
        }

        private void Refresh()
        {
            if (Interlocked.Exchange(ref _refreshQueued, 1) == 1) return;
            OnUiThread(() =>
            {
                Interlocked.Exchange(ref _refreshQueued, 0);
                Flush();
            });
        }
    }
}
