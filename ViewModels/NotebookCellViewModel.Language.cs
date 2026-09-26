using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

/// <summary>An entry of a cell's language menu.</summary>
public sealed record CellLanguageChoice(string LanguageId, string Label, string IconKind, string AccentHex, bool IsSelected, IRelayCommand Command);

/// <summary>
/// The language a code cell runs in, as in Polyglot Notebooks: a <c>#!python</c> (or any language's) first line, else
/// the cell's own choice, else the notebook's default. Also the cell's prompt for <c>input()</c>, and its offer to
/// install a package a run found missing.
/// </summary>
public partial class NotebookCellViewModel
{
    private LanguageRegistry? _languages;
    private Func<string>? _defaultLanguage;
    private string? _notifiedLanguage;
    private TaskCompletionSource<string?>? _pendingInput;

    /// <summary>Connects the cell to the notebook's languages (the tab does this for every cell it makes).</summary>
    public void UseLanguages(LanguageRegistry registry, Func<string> defaultLanguage)
    {
        _languages = registry;
        _defaultLanguage = defaultLanguage;
        NotifyLanguageChanged(force: true);
    }

    /// <summary>The cell's own language id, or null to follow the notebook's default.</summary>
    public string? Language
    {
        get => Model.Language;
        set
        {
            if (Model.Language == value) return;
            Model.Language = value;
            NotifyLanguageChanged(force: true);
            _onModified?.Invoke();
        }
    }

    /// <summary>The language the cell runs in.</summary>
    public string EffectiveLanguage
    {
        get
        {
            var own = Language ?? _defaultLanguage?.Invoke() ?? LanguageIds.CSharp;
            return (_languages != null ? NotebookCellDirectives.LanguageOf(Source, _languages, own) : null) ?? own;
        }
    }

    public ILanguageDefinition? EffectiveLanguageDefinition => _languages?.Get(EffectiveLanguage);

    /// <summary>The languages the cell can be switched to, the current one ticked.</summary>
    public IReadOnlyList<CellLanguageChoice> LanguageChoices =>
        _languages?.NotebookLanguages.Select(l => new CellLanguageChoice(
            l.Id, l.DisplayName, l.IconKind, l.AccentHex,
            string.Equals(l.Id, EffectiveLanguage, StringComparison.OrdinalIgnoreCase),
            new RelayCommand(() => SetLanguage(l.Id)))).ToList()
        ?? (IReadOnlyList<CellLanguageChoice>)Array.Empty<CellLanguageChoice>();

    /// <summary>True when the cell can be switched between languages (more than one can run in notebooks).</summary>
    public bool HasLanguageChoices => IsCodeCell && (_languages?.NotebookLanguages.Count ?? 0) > 1;

    [RelayCommand]
    public void SetLanguage(string? languageId)
    {
        if (string.IsNullOrWhiteSpace(languageId) || _languages?.Get(languageId) is not { } language) return;
        // Following the notebook's default needs no language of its own.
        Language = string.Equals(language.Id, _defaultLanguage?.Invoke(), StringComparison.OrdinalIgnoreCase) ? null : language.Id;
    }

    /// <summary>Raises the language properties; unless <paramref name="force"/>d, only when the effective language changed (typing mostly doesn't).</summary>
    internal void NotifyLanguageChanged(bool force = false)
    {
        var effective = EffectiveLanguage;
        if (!force && effective == _notifiedLanguage) return;
        _notifiedLanguage = effective;
        OnPropertyChanged(nameof(Language));
        OnPropertyChanged(nameof(EffectiveLanguage));
        OnPropertyChanged(nameof(EffectiveLanguageDefinition));
        OnPropertyChanged(nameof(LanguageTag));
        OnPropertyChanged(nameof(LanguageChoices));
        OnPropertyChanged(nameof(HasLanguageChoices));
    }

    // ── input() ─────────────────────────────────────────────────────────────────────────────────────────────────────

    [ObservableProperty]
    private bool _isAwaitingInput;

    [ObservableProperty]
    private string _inputPrompt = string.Empty;

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private bool _isPasswordInput;

    /// <summary>
    /// Asks for a line of input in the cell (a kernel's <c>input()</c>): shows the prompt and an input box, and completes
    /// with what's entered, or null (end of input) when the run stops first.
    /// </summary>
    public Task<string?> AskAsync(string prompt, bool password, CancellationToken ct)
    {
        var answer = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Show()
        {
            _pendingInput?.TrySetResult(null);
            _pendingInput = answer;
            InputPrompt = string.IsNullOrEmpty(prompt) ? "Input:" : prompt;
            IsPasswordInput = password;
            InputText = string.Empty;
            IsAwaitingInput = true;
        }

        OnUiThread(Show);
        ct.Register(() => OnUiThread(() => Answer(answer, null)));
        return answer.Task;
    }

    [RelayCommand]
    public void SubmitInput() => Answer(_pendingInput, InputText);

    /// <summary>Ends the input, as Ctrl+D does in a terminal.</summary>
    [RelayCommand]
    public void EndInput() => Answer(_pendingInput, null);

    private void Answer(TaskCompletionSource<string?>? pending, string? value)
    {
        if (pending == null) return;
        if (ReferenceEquals(_pendingInput, pending))
        {
            _pendingInput = null;
            IsAwaitingInput = false;
            InputText = string.Empty;
        }

        pending.TrySetResult(value);
    }

    // ── missing packages ────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A package the last run needed but isn't installed (e.g. "numpy"), which the cell offers to install.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMissingDependency))]
    [NotifyPropertyChangedFor(nameof(InstallMissingDependencyLabel))]
    [NotifyPropertyChangedFor(nameof(MissingDependencyHintTitle))]
    [NotifyPropertyChangedFor(nameof(MissingDependencyHintDetail))]
    private string? _missingDependency;

    public bool HasMissingDependency => !string.IsNullOrEmpty(MissingDependency);

    public string InstallMissingDependencyLabel => $"Install {MissingDependency}";

    public string MissingDependencyHintTitle => $"The package {MissingDependency} isn't installed";

    public string MissingDependencyHintDetail =>
        $"Install it for the notebook's {EffectiveLanguageDefinition?.DisplayName ?? "kernel"} with {EffectiveLanguageDefinition?.Packages?.ToolName ?? "its package manager"}; the cell runs again after.";

    /// <summary>Installs <see cref="MissingDependency"/> and runs the cell again (set by the tab).</summary>
    public Func<NotebookCellViewModel, Task>? InstallMissingDependencyAction { get; set; }

    [RelayCommand]
    private async Task InstallMissingDependencyAsync()
    {
        if (InstallMissingDependencyAction != null) await InstallMissingDependencyAction(this);
    }

    private static void OnUiThread(Action action)
    {
        if (Avalonia.Application.Current == null || Avalonia.Threading.Dispatcher.UIThread.CheckAccess()) action();
        else Avalonia.Threading.Dispatcher.UIThread.Post(action);
    }
}
