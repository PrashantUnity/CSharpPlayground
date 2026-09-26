using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

public sealed record EnvironmentPropertyItem(string Key, string Value);

/// <summary>
/// JetBrains-style language and environment model for a single language in the settings deck.
/// </summary>
public sealed partial class LanguageSettingItemViewModel : ObservableObject
{
    public ILanguageDefinition Language { get; }
    public IToolchainProvider? Provider { get; }
    public bool IsToolchainLanguage => Provider != null;

    public string DisplayName => Language.DisplayName;
    public string IconKind => Language.IconKind;
    public string AccentHex => Language.AccentHex;
    public string RuntimeDescription => Language.RuntimeDescription;

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

    public LanguageSettingItemViewModel(ILanguageDefinition language, IToolchainProvider? provider = null)
    {
        Language = language;
        Provider = provider ?? language.Toolchain;

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
