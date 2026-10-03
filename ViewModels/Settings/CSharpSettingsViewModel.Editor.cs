using CommunityToolkit.Mvvm.ComponentModel;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings;

public partial class CSharpSettingsViewModel
{
    // ── Editor & Formatting ──
    [ObservableProperty]
    private int _tabSize = 4;

    [ObservableProperty]
    private bool _convertTabsToSpaces = true;

    [ObservableProperty]
    private bool _wordWrap = false;

    [ObservableProperty]
    private bool _showLineNumbers = true;

    [ObservableProperty]
    private bool _enableSyntaxHighlighting = true;

    [ObservableProperty]
    private bool _enableAutoCompletion = true;

    [ObservableProperty]
    private double _fontSize = 13.0;

    // ── Execution & Terminal ──
    [ObservableProperty]
    private int _executionTimeoutSeconds = 0; // 0 = unlimited

    [ObservableProperty]
    private bool _autoClearConsoleOnRun = false;

    [ObservableProperty]
    private int _maxTerminalOutputLines = 10000;

    // ── Roslyn C# Compiler Options ──
    [ObservableProperty]
    private bool _nullableChecksEnabled = true;

    [ObservableProperty]
    private string _languageVersion = "13.0";

    private void InitializeEditorSettings()
    {
        var settings = _settingsStore.GetSettings();
        TabSize = settings.TabSize;
        ConvertTabsToSpaces = settings.ConvertTabsToSpaces;
        WordWrap = settings.WordWrap;
        ShowLineNumbers = settings.ShowLineNumbers;
        EnableSyntaxHighlighting = settings.EnableSyntaxHighlighting;
        EnableAutoCompletion = settings.EnableAutoCompletion;
        FontSize = settings.FontSize;
        ExecutionTimeoutSeconds = settings.ExecutionTimeoutSeconds;
        AutoClearConsoleOnRun = settings.AutoClearConsoleOnRun;
        MaxTerminalOutputLines = settings.MaxTerminalOutputLines;
        NullableChecksEnabled = settings.NullableChecksEnabled;
        LanguageVersion = settings.LanguageVersion;
        HasPendingChanges = false;
    }

    private void SaveEditorSettings()
    {
        var settings = new StudioSettings
        {
            TabSize = TabSize,
            ConvertTabsToSpaces = ConvertTabsToSpaces,
            WordWrap = WordWrap,
            ShowLineNumbers = ShowLineNumbers,
            EnableSyntaxHighlighting = EnableSyntaxHighlighting,
            EnableAutoCompletion = EnableAutoCompletion,
            FontSize = FontSize,
            ExecutionTimeoutSeconds = ExecutionTimeoutSeconds,
            AutoClearConsoleOnRun = AutoClearConsoleOnRun,
            MaxTerminalOutputLines = MaxTerminalOutputLines,
            NullableChecksEnabled = NullableChecksEnabled,
            LanguageVersion = LanguageVersion
        };
        SaveAiSettings(settings);
        _settingsStore.SaveSettings(settings);
    }

    private void ResetEditorSettingsToDefaults()
    {
        var def = new StudioSettings();
        TabSize = def.TabSize;
        ConvertTabsToSpaces = def.ConvertTabsToSpaces;
        WordWrap = def.WordWrap;
        ShowLineNumbers = def.ShowLineNumbers;
        EnableSyntaxHighlighting = def.EnableSyntaxHighlighting;
        EnableAutoCompletion = def.EnableAutoCompletion;
        FontSize = def.FontSize;
        ExecutionTimeoutSeconds = def.ExecutionTimeoutSeconds;
        AutoClearConsoleOnRun = def.AutoClearConsoleOnRun;
        MaxTerminalOutputLines = def.MaxTerminalOutputLines;
        NullableChecksEnabled = def.NullableChecksEnabled;
        LanguageVersion = def.LanguageVersion;
        ResetAiSettingsToDefaults();
    }

    partial void OnTabSizeChanged(int value) => HasPendingChanges = true;
    partial void OnConvertTabsToSpacesChanged(bool value) => HasPendingChanges = true;
    partial void OnWordWrapChanged(bool value) => HasPendingChanges = true;
    partial void OnShowLineNumbersChanged(bool value) => HasPendingChanges = true;
    partial void OnEnableSyntaxHighlightingChanged(bool value) => HasPendingChanges = true;
    partial void OnEnableAutoCompletionChanged(bool value) => HasPendingChanges = true;
    partial void OnFontSizeChanged(double value) => HasPendingChanges = true;
    partial void OnExecutionTimeoutSecondsChanged(int value) => HasPendingChanges = true;
    partial void OnAutoClearConsoleOnRunChanged(bool value) => HasPendingChanges = true;
    partial void OnMaxTerminalOutputLinesChanged(int value) => HasPendingChanges = true;
    partial void OnNullableChecksEnabledChanged(bool value) => HasPendingChanges = true;
    partial void OnLanguageVersionChanged(string value) => HasPendingChanges = true;
}
