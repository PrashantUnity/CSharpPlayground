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
    public bool EnableSyntaxHighlighting { get; set; } = true;
    public bool EnableAutoCompletion { get; set; } = true;
    public double FontSize { get; set; } = 13.0;

    // Execution & Terminal
    public int ExecutionTimeoutSeconds { get; set; } = 0; // 0 = unlimited
    public bool AutoClearConsoleOnRun { get; set; } = false;
    public int MaxTerminalOutputLines { get; set; } = 10000;

    // Roslyn C# Options
    public bool NullableChecksEnabled { get; set; } = true;
    public string LanguageVersion { get; set; } = "13.0";
    public string CSharpExecutionEngine { get; set; } = "internal"; // "internal" (Roslyn) or "external" (dotnet CLI)

    // AI Agent & Local LLM Preferences
    public PdfEditorApp.Plugins.CSharpEditor.Models.AI.AiSettings Ai { get; set; } = new();

    // Theme, density, colour overrides and the theme studio's controls
    public AppearanceSettings Appearance { get; set; } = new();

    /// <summary>The shape of this file; moved when a change needs old files read differently.</summary>
    public int SchemaVersion { get; set; } = 1;

    private static readonly System.Text.Json.JsonSerializerOptions CloneOptions = new();

    /// <summary>A deep copy (through JSON, so a new setting can never be forgotten by a hand-written copy).</summary>
    public StudioSettings Clone() =>
        System.Text.Json.JsonSerializer.Deserialize<StudioSettings>(System.Text.Json.JsonSerializer.Serialize(this, CloneOptions), CloneOptions) ?? new StudioSettings();
}
