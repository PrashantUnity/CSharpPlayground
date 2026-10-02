using System;
using System.IO;
using System.Threading.Tasks;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Storage;

/// <summary>
/// Manages discovery, reading, writing, and initialization of user customization scripts.
/// Resolves ~/.frysharp/init.csx and workspace-level .frysharp/init.csx.
/// </summary>
public class CustomizationStorageService
{
    public static readonly string DefaultInitScriptName = "init.csx";

    private readonly string _globalFolder;
    private readonly string _globalInitScriptPath;

    public string GlobalFolder => _globalFolder;
    public string GlobalInitScriptPath => _globalInitScriptPath;

    public string? WorkspaceFolder { get; set; }
    public string? WorkspaceInitScriptPath =>
        !string.IsNullOrWhiteSpace(WorkspaceFolder)
            ? Path.Combine(WorkspaceFolder, ".frysharp", DefaultInitScriptName)
            : null;

    public CustomizationStorageService(string? customFolder = null)
    {
        _globalFolder = customFolder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".frysharp");

        _globalInitScriptPath = Path.Combine(_globalFolder, DefaultInitScriptName);
    }

    /// <summary>
    /// Ensures ~/.frysharp/ directory and starter init.csx exist.
    /// </summary>
    public async Task<string> EnsureInitScriptExistsAsync()
    {
        if (!Directory.Exists(_globalFolder))
        {
            Directory.CreateDirectory(_globalFolder);
        }

        if (!File.Exists(_globalInitScriptPath))
        {
            string starter = GetStarterInitScript();
            await File.WriteAllTextAsync(_globalInitScriptPath, starter);
            return starter;
        }

        return await File.ReadAllTextAsync(_globalInitScriptPath);
    }

    /// <summary>
    /// Reads active initialization script contents.
    /// </summary>
    public async Task<string?> ReadInitScriptAsync()
    {
        if (File.Exists(_globalInitScriptPath))
        {
            return await File.ReadAllTextAsync(_globalInitScriptPath);
        }

        return null;
    }

    /// <summary>
    /// Reads workspace-level initialization script contents if present.
    /// </summary>
    public async Task<string?> ReadWorkspaceInitScriptAsync()
    {
        if (WorkspaceInitScriptPath != null && File.Exists(WorkspaceInitScriptPath))
        {
            return await File.ReadAllTextAsync(WorkspaceInitScriptPath);
        }

        return null;
    }

    /// <summary>
    /// Saves updated content to the global init script.
    /// </summary>
    public async Task SaveInitScriptAsync(string code)
    {
        if (!Directory.Exists(_globalFolder))
        {
            Directory.CreateDirectory(_globalFolder);
        }

        await File.WriteAllTextAsync(_globalInitScriptPath, code);
    }

    public static string GetStarterInitScript()
    {
        return """
            // ~/.frysharp/init.csx - C# Code Studio Customization Script
            // This script runs automatically on studio startup and reloads live when saved.
            using FrySharp.Sdk;

            // =========================================================================
            // 1. THEME & VISUAL CUSTOMIZATION
            // =========================================================================
            // Built-in theme presets: 'dark-plus', 'light-plus', 'dracula', 'cyberpunk', 'monokai', 'one-dark'
            // App.Themes.ApplyTheme("dracula");

            // Override specific color tokens live:
            // App.Themes.SetColor("DsPrimaryBrush", "#00FFCC"); // Neon cyan accent
            // App.Themes.SetColor("DsBgBrush", "#0D1117");

            // Set layout density: Compact, Comfortable, or Spacious
            // App.Themes.SetDensity(LayoutDensity.Comfortable);

            // Dynamically adjust spacing and dimensions:
            // App.Themes.SetSpacing("DensityPaddingSmall", 3.0);
            // App.Themes.SetDimension("DensityTabHeight", 34.0);

            // =========================================================================
            // 2. CUSTOM COMMANDS & SHORTCUTS
            // =========================================================================
            App.Commands.Register("custom.welcome", "Show Welcome Notification", () =>
            {
                App.UI.ShowSuccess("Welcome to customized C# Code Studio!");
            }, gesture: "Ctrl+Alt+H");

            // =========================================================================
            // 3. LIFECYCLE HOOKS & EXECUTION PIPELINE
            // =========================================================================
            App.Hooks.OnBeforeScriptRun(ctx =>
            {
                // Inspect or guard before script execution:
                // App.UI.ShowInfo($"Running: {ctx.DocumentTitle}");
            });

            App.Hooks.OnThemeChanged(themeId =>
            {
                // React to theme changes
            });
            """;
    }
}
