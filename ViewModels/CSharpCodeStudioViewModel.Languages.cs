using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

/// <summary>An entry of the status bar's toolchain picker: an installed Python, or "Automatic".</summary>
public sealed record ToolchainChoice(string Label, string Detail, bool IsSelected, IRelayCommand Command);

/// <summary>A command of the toolchain picker besides choosing one, e.g. "Create studio environment".</summary>
public sealed record ToolchainActionChoice(string Label, string Description, IAsyncRelayCommand Command);

/// <summary>
/// What the active document's language can do, for the toolbar, panels, breadcrumbs and status bar; and the picker
/// of the toolchain it runs with. Everything here follows <see cref="ActiveLanguage"/>'s capabilities, so a language
/// added to the registry gets the right UI without changes here.
/// </summary>
public partial class CSharpCodeStudioViewModel
{
    private CancellationTokenSource? _toolchainCts;

    public bool SupportsExecutionModes => ActiveLanguage.Has(LanguageCapabilities.ExecutionModes);
    public bool SupportsDebugging => ActiveLanguage.Has(LanguageCapabilities.Debugging);
    public bool SupportsFormatting => ActiveLanguage.Has(LanguageCapabilities.Formatting);
    public bool SupportsTestCases => ActiveLanguage.Has(LanguageCapabilities.TestCases);
    public bool SupportsBreakpoints => ActiveLanguage.Has(LanguageCapabilities.Breakpoints);
    public bool SupportsStandardInput => ActiveLanguage.Has(LanguageCapabilities.StandardInput);

    /// <summary>The toolbar's Debug button: while nothing runs, for a language with a debugger.</summary>
    public bool ShowDebugButton => !IsExecuting && SupportsDebugging;

    /// <summary>True when the document runs with an installed toolchain (a Python), which the status bar lets you pick.</summary>
    public bool HasToolchain => ActiveLanguage.Toolchain != null;

    /// <summary>"Python 3.14.6 (Homebrew)" once found, "Python not found", or the language's own description (C#).</summary>
    [ObservableProperty]
    private string? _toolchainLabel;

    [ObservableProperty]
    private bool _isToolchainMissing;

    /// <summary>The breadcrumbs' runtime: the toolchain in use, or how the language runs (C#: "C# (.NET 10 Roslyn)").</summary>
    public string RuntimeLabel => HasToolchain ? ToolchainLabel ?? ActiveLanguage.DisplayName : ActiveLanguage.RuntimeDescription;

    /// <summary>The status bar's language item: C#'s execution mode, or the toolchain in use.</summary>
    public string LanguageStatusText => SupportsExecutionModes ? LanguageModeStatusText : ToolchainLabel ?? ActiveLanguage.DisplayName;

    public ObservableCollection<ToolchainChoice> ToolchainChoices { get; } = new();

    public ObservableCollection<ToolchainActionChoice> ToolchainActions { get; } = new();

    partial void OnToolchainLabelChanged(string? value)
    {
        OnPropertyChanged(nameof(RuntimeLabel));
        OnPropertyChanged(nameof(LanguageStatusText));
    }

    // The document changed: everything that depends on its language, and the toolchain it would run with.
    private void OnActiveLanguageChanged()
    {
        OnPropertyChanged(nameof(ActiveLanguage));
        OnPropertyChanged(nameof(SupportsExecutionModes));
        OnPropertyChanged(nameof(SupportsDebugging));
        OnPropertyChanged(nameof(ShowDebugButton));
        OnPropertyChanged(nameof(SupportsFormatting));
        OnPropertyChanged(nameof(SupportsTestCases));
        OnPropertyChanged(nameof(SupportsBreakpoints));
        OnPropertyChanged(nameof(SupportsStandardInput));
        OnPropertyChanged(nameof(HasToolchain));
        OnPropertyChanged(nameof(RuntimeLabel));
        OnPropertyChanged(nameof(LanguageStatusText));

        ToolchainLabel = null;
        IsToolchainMissing = false;
        if (HasToolchain) _ = ResolveToolchainLabelAsync();
    }

    private ToolchainQuery CurrentToolchainQuery() => new(
        Script.SourceFilePath is { } path ? Path.GetDirectoryName(path) : null,
        _storageService.ActiveWorkspaceRootPath);

    // Looks up (off the UI thread) which toolchain the document would run with, for the status bar.
    private async Task ResolveToolchainLabelAsync()
    {
        var provider = ActiveLanguage.Toolchain;
        if (provider == null) return;

        _toolchainCts?.Cancel();
        var cts = _toolchainCts = new CancellationTokenSource();
        var document = Script;
        var query = CurrentToolchainQuery();
        try
        {
            var resolution = await Task.Run(() => provider.ResolveAsync(query, cts.Token), cts.Token);
            if (cts.IsCancellationRequested || !ReferenceEquals(document, Script)) return;
            _postToUiThread(() =>
            {
                if (!ReferenceEquals(document, Script)) return;
                ToolchainLabel = resolution.Toolchain?.Label ?? $"{provider.ToolName} not found";
                IsToolchainMissing = !resolution.IsFound;
            });
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>Fills the picker: every toolchain found (the one in use ticked), "Automatic", and the language's actions.</summary>
    [RelayCommand]
    public async Task RefreshToolchainsAsync()
    {
        var provider = ActiveLanguage.Toolchain;
        ToolchainChoices.Clear();
        ToolchainActions.Clear();
        if (provider == null) return;

        provider.Refresh();
        var query = CurrentToolchainQuery();
        var found = await Task.Run(() => provider.ListAsync(query));
        var resolved = await Task.Run(() => provider.ResolveAsync(query));
        var selected = provider.SelectedPath;

        ToolchainChoices.Add(new ToolchainChoice(
            "Automatic",
            resolved.Toolchain != null ? $"Uses {resolved.Toolchain.Label}" : $"{provider.ToolName} not found",
            string.IsNullOrEmpty(selected),
            new RelayCommand(() => SelectToolchain(provider, null))));
        foreach (var toolchain in found)
        {
            ToolchainChoices.Add(new ToolchainChoice(
                toolchain.Label,
                toolchain.ExecutablePath,
                !string.IsNullOrEmpty(selected) && string.Equals(selected, toolchain.ExecutablePath, StringComparison.Ordinal),
                new RelayCommand(() => SelectToolchain(provider, toolchain.ExecutablePath))));
        }

        foreach (var action in provider.Actions)
        {
            ToolchainActions.Add(new ToolchainActionChoice(action.Label, action.Description, new AsyncRelayCommand(() => RunToolchainActionAsync(provider, action.Id))));
        }

        ToolchainLabel = resolved.Toolchain?.Label ?? $"{provider.ToolName} not found";
        IsToolchainMissing = !resolved.IsFound;
    }

    private void SelectToolchain(IToolchainProvider provider, string? path)
    {
        provider.Select(path);
        CompilerStatusText = path == null ? $"{provider.ToolName}: automatic" : $"{provider.ToolName}: {path}";
        _ = RefreshToolchainsAsync();
    }

    private async Task RunToolchainActionAsync(IToolchainProvider provider, string actionId)
    {
        ShowConsoleTab();
        var tab = OpenTabs.FirstOrDefault(t => t.Id == Script.Id);
        void Append(string text) => _postToUiThread(() =>
        {
            if (tab != null) tab.ConsoleOutput += text;
            if (tab == null || tab.IsActive) ConsoleOutput += text;
        });

        CompilerStatusText = "Working…";
        var result = await Task.Run(() => provider.RunActionAsync(actionId, CurrentToolchainQuery(), Append));
        Append(result.Message + "\n");
        CompilerStatusText = result.Success ? result.Message : $"⚠️ {result.Message.Split('\n')[0]}";
        await RefreshToolchainsAsync();
    }
}
