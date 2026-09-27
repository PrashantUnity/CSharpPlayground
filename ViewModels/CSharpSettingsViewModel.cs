using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Settings;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

public sealed record SettingsCategoryItem(string Id, string Title, string IconKind, string Description);
public sealed record KeymapShortcutItem(string Action, string Shortcut, string Category, string Description);

/// <summary>
/// JetBrains-style Settings and Environment Setup controller for C# Code Studio.
/// </summary>
public partial class CSharpSettingsViewModel : ObservableObject
{
    private readonly StudioLanguageServices _languageServices;
    private readonly StudioSettingsStore _settingsStore;
    private readonly Action? _backToHubAction;
    private readonly Action? _backToPreviousAction;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLanguagesCategoryActive))]
    [NotifyPropertyChangedFor(nameof(IsEditorCategoryActive))]
    [NotifyPropertyChangedFor(nameof(IsExecutionCategoryActive))]
    [NotifyPropertyChangedFor(nameof(IsKeymapCategoryActive))]
    private string _activeCategory = "Languages";

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _hasStatusMessage;

    [ObservableProperty]
    private bool _isStatusError;

    [ObservableProperty]
    private bool _hasPendingChanges;

    public bool IsLanguagesCategoryActive => ActiveCategory == "Languages";
    public bool IsEditorCategoryActive => ActiveCategory == "Editor";
    public bool IsExecutionCategoryActive => ActiveCategory == "Execution";
    public bool IsKeymapCategoryActive => ActiveCategory == "Keymap";

    public ObservableCollection<SettingsCategoryItem> Categories { get; } = new();
    public ObservableCollection<KeymapShortcutItem> Shortcuts { get; } = new();

    public CSharpSettingsViewModel(
        StudioLanguageServices languageServices,
        StudioSettingsStore? settingsStore = null,
        Action? backToHubAction = null,
        Action? backToPreviousAction = null)
    {
        _languageServices = languageServices;
        _settingsStore = settingsStore ?? languageServices.StudioSettings;
        _backToHubAction = backToHubAction;
        _backToPreviousAction = backToPreviousAction;

        PopulateCategories();
        PopulateKeymap();
        InitializeKeymap();
        InitializeEditorSettings();
        InitializeLanguages();

        _settingsStore.SettingsChanged += OnStoreSettingsChanged;
    }

    private void OnStoreSettingsChanged(StudioSettings s)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (!HasPendingChanges && Math.Abs(FontSize - s.FontSize) > 0.05)
            {
                FontSize = s.FontSize;
                HasPendingChanges = false;
            }
        });
    }

    private void PopulateCategories()
    {
        Categories.Add(new SettingsCategoryItem("Languages", "Languages & Runtimes", "TuneVariant", "Interpreters, SDKs, virtualenvs & compilers"));
        Categories.Add(new SettingsCategoryItem("Editor", "Editor & Formatting", "CodeBraces", "Indentation, font size, line numbers & wrap"));
        Categories.Add(new SettingsCategoryItem("Execution", "Execution & Terminal", "Console", "Execution timeout, stdout buffers & process lifecycle"));
        Categories.Add(new SettingsCategoryItem("Keymap", "Keymap & Shortcuts", "KeyboardOutline", "Visual Studio Code & studio keybindings"));
    }

    private void PopulateKeymap()
    {
        Shortcuts.Add(new KeymapShortcutItem("Open Settings", "Ctrl+, / ⌘,", "General", "Open this settings & environments page"));
        Shortcuts.Add(new KeymapShortcutItem("Toggle Side Bar", "Ctrl+B / ⌘B", "Layout", "Show or hide primary tool side bar"));
        Shortcuts.Add(new KeymapShortcutItem("Toggle Bottom Panel", "Ctrl+J / ⌘J", "Layout", "Show or hide output, problems & terminal deck"));
        Shortcuts.Add(new KeymapShortcutItem("Start Debugging / Run", "F5", "Execution", "Start debugging active C# script or run script"));
        Shortcuts.Add(new KeymapShortcutItem("Run without Debugging", "Ctrl+F5", "Execution", "Execute active script in fast runner"));
        Shortcuts.Add(new KeymapShortcutItem("Stop Execution", "Shift+F5", "Execution", "Halt running script or active debug session"));
        Shortcuts.Add(new KeymapShortcutItem("Step Over", "F10", "Debug", "Step to next line without entering method"));
        Shortcuts.Add(new KeymapShortcutItem("Step Into", "F11", "Debug", "Step into method under debugger"));
        Shortcuts.Add(new KeymapShortcutItem("Format Document", "Shift+Alt+F / Ctrl+K Ctrl+D", "Editor", "Format code using language standard indentation"));
        Shortcuts.Add(new KeymapShortcutItem("Hover Quick Info", "Ctrl+K Ctrl+I", "Editor", "Display XML doc comments and symbol signature"));
        Shortcuts.Add(new KeymapShortcutItem("Find & Replace", "Ctrl+F / ⌘F", "Editor", "Search text within the active document canvas"));
        Shortcuts.Add(new KeymapShortcutItem("Zoom In", "Ctrl+= / ⌘+", "Editor", "Increase code canvas and terminal font size"));
        Shortcuts.Add(new KeymapShortcutItem("Zoom Out", "Ctrl+- / ⌘-", "Editor", "Decrease code canvas and terminal font size"));
        Shortcuts.Add(new KeymapShortcutItem("Reset Zoom", "Ctrl+0 / ⌘0", "Editor", "Reset font typography to default 100% (13px)"));
        Shortcuts.Add(new KeymapShortcutItem("Mouse Wheel Zoom", "Ctrl+Wheel / ⌘+Wheel", "Editor", "Smoothly scale font size up or down"));
        Shortcuts.Add(new KeymapShortcutItem("Focus Explorer", "Ctrl+Shift+E", "Side Bar", "Open Explorer tree view in side bar"));
        Shortcuts.Add(new KeymapShortcutItem("Focus Search", "Ctrl+Shift+F", "Side Bar", "Open Workspace Text Search in side bar"));
        Shortcuts.Add(new KeymapShortcutItem("Focus Run & Debug", "Ctrl+Shift+D", "Side Bar", "Open Debugger panel in side bar"));
        Shortcuts.Add(new KeymapShortcutItem("Focus Problems", "Ctrl+Shift+M", "Bottom Deck", "Open Problems diagnostics panel in tool deck"));
    }

    [RelayCommand]
    public void SelectCategory(string categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId)) return;
        ActiveCategory = categoryId;
        OnPropertyChanged(nameof(IsLanguagesCategoryActive));
        OnPropertyChanged(nameof(IsEditorCategoryActive));
        OnPropertyChanged(nameof(IsExecutionCategoryActive));
        OnPropertyChanged(nameof(IsKeymapCategoryActive));
    }

    [RelayCommand]
    public void BackToHub()
    {
        _backToHubAction?.Invoke();
    }

    [RelayCommand]
    public void BackToPrevious()
    {
        if (_backToPreviousAction != null)
        {
            _backToPreviousAction.Invoke();
        }
        else
        {
            _backToHubAction?.Invoke();
        }
    }

    [RelayCommand]
    public void Apply()
    {
        SaveEditorSettings();
        SaveLanguageSettings();
        HasPendingChanges = false;
        ShowNotification("Settings and environment choices saved successfully.", isError: false);
    }

    [RelayCommand]
    public void ResetDefaults()
    {
        ResetEditorSettingsToDefaults();
        ResetLanguageSettingsToDefaults();
        HasPendingChanges = true;
        ShowNotification("Settings reset to defaults. Click Apply to persist.", isError: false);
    }

    public void ShowNotification(string message, bool isError = false)
    {
        StatusMessage = message;
        IsStatusError = isError;
        HasStatusMessage = true;
    }

    [RelayCommand]
    public void DismissNotification()
    {
        HasStatusMessage = false;
        StatusMessage = null;
    }

    partial void OnSearchQueryChanged(string value)
    {
        ApplySearchFilter(value);
    }
}
