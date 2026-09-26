using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

public sealed record EnvironmentPropertyItem(string Key, string Value);
public sealed record CapabilityItem(string Name, bool IsSupported, string Description, string IconKind);

/// <summary>
/// Language and environment model for a single language in the settings deck.
/// </summary>
public sealed partial class LanguageSettingItemViewModel : ObservableObject
{
    public ILanguageDefinition Language { get; }
    public IToolchainProvider? Provider { get; }
    public CSharpSettingsViewModel? ParentSettings { get; }
    public bool IsToolchainLanguage => Provider != null;
    public bool IsCSharp => Language.Id == LanguageIds.CSharp;

    public string DisplayName => Language.DisplayName;
    public string IconKind => Language.IconKind;
    public string AccentHex => Language.AccentHex;
    public string RuntimeDescription => Language.RuntimeDescription;
    public string FileExtensionsDisplay => string.Join(", ", Language.FileExtensions);
    public string StorageBadge => Language.Storage == LanguageStorageKind.FryDocument ? "FryDocument (.frycs)" : "Source File";
    public string StorageExplanation => Language.Storage == LanguageStorageKind.FryDocument
        ? "Stores code, notebook metadata, notes, and breakpoints in structured JSON."
        : "Standard plain-text file compatible with external editors, command-line tools, and git.";
    public string CommentPrefix => Language.LineCommentPrefix;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMissing))]
    private bool _isChecking = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMissing))]
    private bool _isFound;

    public bool IsMissing => !IsChecking && !IsFound && IsToolchainLanguage;

    [ObservableProperty]
    private string _statusBadge = "Checking…";

    [ObservableProperty]
    private string _statusColor = "#E3B341"; // amber

    [ObservableProperty]
    private string _activeToolchainLabel = string.Empty;

    [ObservableProperty]
    private string _activeExecutablePath = string.Empty;

    [ObservableProperty]
    private string _activeVersion = string.Empty;

    [ObservableProperty]
    private string _activeSource = string.Empty;

    [ObservableProperty]
    private bool _isAutoDetect = true;

    [ObservableProperty]
    private string _customPath = string.Empty;

    [ObservableProperty]
    private ToolchainInfo? _selectedDiscoveredToolchain;

    [ObservableProperty]
    private string _missingTitle = string.Empty;

    [ObservableProperty]
    private string _missingSummary = string.Empty;

    [ObservableProperty]
    private string? _missingDownloadUrl;

    [ObservableProperty]
    private string _actionOutputLog = string.Empty;

    [ObservableProperty]
    private bool _isRunningAction;

    [ObservableProperty]
    private string _actionStatusMessage = string.Empty;

    public ObservableCollection<ToolchainInfo> DiscoveredToolchains { get; } = new();
    public ObservableCollection<ToolchainAction> AvailableActions { get; } = new();
    public ObservableCollection<string> MissingSteps { get; } = new();
    public ObservableCollection<EnvironmentPropertyItem> EnvironmentDetails { get; } = new();
    public ObservableCollection<CapabilityItem> CapabilityItems { get; } = new();

    public LanguageSettingItemViewModel(ILanguageDefinition language, IToolchainProvider? provider = null, CSharpSettingsViewModel? parent = null)
    {
        Language = language;
        Provider = provider ?? language.Toolchain;
        ParentSettings = parent;

        PopulateCapabilities();

        if (Provider == null)
        {
            // Built-in compiled/Roslyn language (e.g. C#)
            IsChecking = false;
            IsFound = true;
            StatusBadge = "In-Process Ready";
            StatusColor = "#4EBA6F"; // Green
            ActiveToolchainLabel = Language.RuntimeDescription;
            ActiveExecutablePath = "Built-in Roslyn Compiler (.NET 10)";
            ActiveVersion = ".NET 10.0 (C# 13)";
            ActiveSource = "Core Runtime";
            EnvironmentDetails.Add(new EnvironmentPropertyItem("Compiler Engine", "Roslyn In-Memory Engine"));
            EnvironmentDetails.Add(new EnvironmentPropertyItem("Language Standard", "C# 13.0"));
            EnvironmentDetails.Add(new EnvironmentPropertyItem("Target Framework", ".NET 10.0"));
            EnvironmentDetails.Add(new EnvironmentPropertyItem("Execution Sandbox", "In-Process Interactive Kernel"));
            EnvironmentDetails.Add(new EnvironmentPropertyItem("Host Architecture", System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString()));
            EnvironmentDetails.Add(new EnvironmentPropertyItem(".NET Runtime", System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription));
        }
        else
        {
            var saved = Provider.SelectedPath;
            IsAutoDetect = string.IsNullOrWhiteSpace(saved);
            CustomPath = saved ?? string.Empty;
            foreach (var action in Provider.Actions)
            {
                AvailableActions.Add(action);
            }
        }
    }

    private void PopulateCapabilities()
    {
        CapabilityItems.Clear();
        CapabilityItems.Add(new CapabilityItem("Syntax Highlighting", true, "VS Code Dark+ & Light+ theme palettes", "PaletteOutline"));
        CapabilityItems.Add(new CapabilityItem("Smart Indentation", true, "Language-aware smart indentation rules", "FormatAlignLeft"));
        CapabilityItems.Add(new CapabilityItem("Live Diagnostics", Language.Has(LanguageCapabilities.LiveDiagnostics), "Real-time compiler diagnostics while editing", "AlertCircleOutline"));
        CapabilityItems.Add(new CapabilityItem("Interactive Debugging", Language.Has(LanguageCapabilities.Debugging), "Breakpoints, stepping (F10/F11), and variable inspector", "BugPlayOutline"));
        CapabilityItems.Add(new CapabilityItem("Hover Quick Info", Language.Has(LanguageCapabilities.QuickInfo), "Symbol signatures and XML documentation on hover", "InformationOutline"));
        CapabilityItems.Add(new CapabilityItem("Document Formatting", Language.Has(LanguageCapabilities.Formatting), "Format document code indentation (Shift+Alt+F)", "FormatLineSpacing"));
        CapabilityItems.Add(new CapabilityItem("Code Completion", Language.Has(LanguageCapabilities.Completion), "IntelliSense symbol and keyword completions", "CodeBraces"));
        CapabilityItems.Add(new CapabilityItem("Interactive Stdin", Language.Has(LanguageCapabilities.StandardInput), "Stream interactive input() from terminal buffer", "ConsoleLine"));
        CapabilityItems.Add(new CapabilityItem("Notebook Code Cells", Language.Has(LanguageCapabilities.NotebookCells), "Polyglot interactive notebook cell execution", "BookOpenOutline"));
        CapabilityItems.Add(new CapabilityItem("Variable Sharing", Language.Has(LanguageCapabilities.ValueSharing), "Cross-kernel value exchange via #!share", "SwapHorizontal"));
        CapabilityItems.Add(new CapabilityItem("Package Manager", Language.Has(LanguageCapabilities.Packages), "In-app package resolution and installation", "PackageVariantClosed"));
        CapabilityItems.Add(new CapabilityItem("Test Cases Deck", Language.Has(LanguageCapabilities.TestCases), "Automated Check(...) assertion verification", "CheckboxMarkedCircleOutline"));
    }

    public void UpdateResolution(ToolchainResolution resolution, IReadOnlyList<ToolchainInfo> allFound)
    {
        IsChecking = false;
        IsFound = resolution.IsFound;
        DiscoveredToolchains.Clear();
        EnvironmentDetails.Clear();
        MissingSteps.Clear();

        foreach (var t in allFound)
        {
            DiscoveredToolchains.Add(t);
        }

        if (resolution.Toolchain is { } toolchain)
        {
            StatusBadge = $"Ready: {toolchain.DisplayName}";
            StatusColor = "#4EBA6F"; // green
            ActiveToolchainLabel = toolchain.Label;
            ActiveExecutablePath = toolchain.ExecutablePath;
            ActiveVersion = toolchain.Version.ToString();
            ActiveSource = toolchain.Source;
            SelectedDiscoveredToolchain = DiscoveredToolchains.FirstOrDefault(d =>
                string.Equals(d.ExecutablePath, toolchain.ExecutablePath, StringComparison.OrdinalIgnoreCase));

            EnvironmentDetails.Add(new EnvironmentPropertyItem("Executable Path", toolchain.ExecutablePath));
            EnvironmentDetails.Add(new EnvironmentPropertyItem("Runtime Version", toolchain.Version.ToString()));
            EnvironmentDetails.Add(new EnvironmentPropertyItem("Environment Source", toolchain.Source));

            foreach (var (k, v) in toolchain.Properties)
            {
                if (!string.IsNullOrWhiteSpace(v))
                {
                    var label = k switch
                    {
                        "prefix" => "Environment Prefix",
                        "basePrefix" => "Base Prefix",
                        "virtualEnvironment" => "Virtual Environment",
                        "externallyManaged" => "Externally Managed",
                        "pip" => "Package Manager (pip)",
                        "venv" => "Virtualenv Module (venv)",
                        "sitePackages" => "Site-Packages Directory",
                        "studioEnvironment" => "Studio Environment",
                        _ => k
                    };
                    EnvironmentDetails.Add(new EnvironmentPropertyItem(label, v));
                }
            }
        }
        else
        {
            StatusBadge = $"{Provider?.ToolName ?? DisplayName} Not Detected";
            StatusColor = "#F85149"; // red
            ActiveToolchainLabel = "None detected";
            ActiveExecutablePath = string.Empty;
            ActiveVersion = "Not found";
            ActiveSource = "Missing";
            SelectedDiscoveredToolchain = null;

            if (resolution.Missing is { } missing)
            {
                MissingTitle = missing.Title;
                MissingSummary = missing.Summary;
                MissingDownloadUrl = missing.DownloadUrl;
                foreach (var step in missing.Steps)
                {
                    MissingSteps.Add(step);
                }
            }
            else
            {
                MissingTitle = $"{Provider?.ToolName ?? DisplayName} wasn't found";
                MissingSummary = "Please install the runtime or specify an existing executable path.";
            }
        }
    }
}
