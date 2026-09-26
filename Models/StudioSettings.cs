namespace PdfEditorApp.Plugins.CSharpEditor.Models;

/// <summary>
/// User-configurable IDE, editor, compiler and execution preferences.
/// Persisted to studio_settings.json.
/// </summary>
public sealed class StudioSettings
{
    // Editor & Formatting
    public int TabSize { get; set; } = 4;
    public bool ConvertTabsToSpaces { get; set; } = true;
    public bool WordWrap { get; set; } = false;
    public bool ShowLineNumbers { get; set; } = true;
    public double FontSize { get; set; } = 13.0;

    // Execution & Terminal
    public int ExecutionTimeoutSeconds { get; set; } = 0; // 0 = unlimited
    public bool AutoClearConsoleOnRun { get; set; } = false;
    public int MaxTerminalOutputLines { get; set; } = 10000;

    // Roslyn C# Options
    public bool NullableChecksEnabled { get; set; } = true;
    public string LanguageVersion { get; set; } = "13.0";

    public StudioSettings Clone() => new()
    {
        TabSize = TabSize,
        ConvertTabsToSpaces = ConvertTabsToSpaces,
        WordWrap = WordWrap,
        ShowLineNumbers = ShowLineNumbers,
        FontSize = FontSize,
        ExecutionTimeoutSeconds = ExecutionTimeoutSeconds,
        AutoClearConsoleOnRun = AutoClearConsoleOnRun,
        MaxTerminalOutputLines = MaxTerminalOutputLines,
        NullableChecksEnabled = NullableChecksEnabled,
        LanguageVersion = LanguageVersion
    };
}
