using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Models.AI;
using PdfEditorApp.Plugins.CSharpEditor.Services.AI.Core;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings;

public partial class CSharpSettingsViewModel
{
    // ── AI Agent & Local LLM Preferences ──
    [ObservableProperty]
    private AiProviderKind _aiProvider = AiProviderKind.Ollama;

    [ObservableProperty]
    private string _aiEndpointUrl = AiSettings.DefaultOllamaEndpoint;

    [ObservableProperty]
    private string _aiModelName = AiSettings.DefaultModelName;

    [ObservableProperty]
    private string? _aiApiKey;

    [ObservableProperty]
    private float _aiTemperature = 0.2f;

    [ObservableProperty]
    private int _aiMaxOutputTokens = 4096;

    [ObservableProperty]
    private bool _aiAutoApproveEdits = false;

    [ObservableProperty]
    private string _aiSystemPrompt = AiSettings.DefaultSystemPrompt;

    // ── AI Connection & Discovery State ──
    [ObservableProperty]
    private string _aiConnectionStatus = "Not tested";

    [ObservableProperty]
    private bool _isAiConnecting = false;

    [ObservableProperty]
    private bool _isAiConnected = false;

    [ObservableProperty]
    private bool _isAiConnectionError = false;

    public ObservableCollection<string> DiscoveredAiModels { get; } = new();

    public IReadOnlyList<AiProviderKind> AvailableAiProviders { get; } = new[]
    {
        AiProviderKind.LMStudio,
        AiProviderKind.Ollama,
        AiProviderKind.OpenAiCompatible,
        AiProviderKind.OpenAI,
        AiProviderKind.AzureOpenAI
    };

    private void InitializeAiSettings()
    {
        var settings = _settingsStore.GetSettings();
        var ai = settings.Ai ?? new AiSettings();

        AiProvider = ai.Provider;
        AiEndpointUrl = ai.EndpointUrl;
        AiModelName = ai.ModelName;
        AiApiKey = ai.ApiKey;
        AiTemperature = ai.Temperature;
        AiMaxOutputTokens = ai.MaxOutputTokens;
        AiAutoApproveEdits = ai.AutoApproveEdits;
        AiSystemPrompt = string.IsNullOrWhiteSpace(ai.SystemPrompt) ? AiSettings.DefaultSystemPrompt : ai.SystemPrompt;

        DiscoveredAiModels.Clear();
        if (!string.IsNullOrWhiteSpace(AiModelName))
        {
            DiscoveredAiModels.Add(AiModelName);
        }
    }

    private void SaveAiSettings(StudioSettings settings)
    {
        // Only the fields this page shows: the AI composer's own (reasoning on/off) are left as they are.
        var ai = settings.Ai ?? new AiSettings();
        ai.Provider = AiProvider;
        ai.EndpointUrl = string.IsNullOrWhiteSpace(AiEndpointUrl) ? AiSettings.DefaultOllamaEndpoint : AiEndpointUrl.Trim();
        ai.ModelName = string.IsNullOrWhiteSpace(AiModelName) ? AiSettings.DefaultModelName : AiModelName.Trim();
        ai.ApiKey = string.IsNullOrWhiteSpace(AiApiKey) ? null : AiApiKey.Trim();
        ai.Temperature = AiTemperature;
        ai.MaxOutputTokens = AiMaxOutputTokens;
        ai.AutoApproveEdits = AiAutoApproveEdits;
        ai.SystemPrompt = string.IsNullOrWhiteSpace(AiSystemPrompt) ? AiSettings.DefaultSystemPrompt : AiSystemPrompt;
        settings.Ai = ai;
    }

    private void ResetAiSettingsToDefaults()
    {
        var def = new AiSettings();
        AiProvider = def.Provider;
        AiEndpointUrl = def.EndpointUrl;
        AiModelName = def.ModelName;
        AiApiKey = def.ApiKey;
        AiTemperature = def.Temperature;
        AiMaxOutputTokens = def.MaxOutputTokens;
        AiAutoApproveEdits = def.AutoApproveEdits;
        AiSystemPrompt = def.SystemPrompt;

        AiConnectionStatus = "Reset to defaults (not tested)";
        IsAiConnected = false;
        IsAiConnectionError = false;
    }

    [RelayCommand]
    public async Task TestAiConnectionAsync()
    {
        IsAiConnecting = true;
        IsAiConnected = false;
        IsAiConnectionError = false;
        AiConnectionStatus = "Testing endpoint connectivity...";

        var config = new AiSettings
        {
            Provider = AiProvider,
            EndpointUrl = AiEndpointUrl,
            ModelName = AiModelName,
            ApiKey = AiApiKey
        };

        try
        {
            var discovery = new AiModelDiscoveryService();
            var health = await discovery.PingEndpointAsync(config);
            if (!health.IsOnline)
            {
                IsAiConnecting = false;
                IsAiConnectionError = true;
                AiConnectionStatus = $"Unreachable: Endpoint at '{config.EndpointUrl}' failed to respond ({health.ErrorMessage ?? "connection error"}).";
                return;
            }

            var models = await discovery.GetAvailableModelsAsync(config);
            DiscoveredAiModels.Clear();
            foreach (var m in models)
            {
                DiscoveredAiModels.Add(m);
            }

            IsAiConnecting = false;
            IsAiConnected = true;
            AiConnectionStatus = models.Count > 0
                ? $"Connected successfully! Discovered {models.Count} model(s) ({health.Latency.TotalMilliseconds:0}ms latency)."
                : $"Connected successfully ({health.Latency.TotalMilliseconds:0}ms latency, no models enumerated).";
        }
        catch (Exception ex)
        {
            IsAiConnecting = false;
            IsAiConnectionError = true;
            AiConnectionStatus = $"Connection failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public void SelectDiscoveredModel(string? model)
    {
        if (!string.IsNullOrWhiteSpace(model))
        {
            AiModelName = model;
        }
    }

    public int AiSystemPromptLength => AiSystemPrompt?.Length ?? 0;
    public string AiPromptPresetName => AiSettings.GetPromptPresetName(AiSystemPrompt);

    [RelayCommand]
    public void ResetAiPromptToDefault()
    {
        AiSystemPrompt = AiSettings.DefaultSystemPrompt;
        HasPendingChanges = true;
    }

    [RelayCommand]
    public void SetDefaultPromptPreset()
    {
        AiSystemPrompt = AiSettings.DefaultSystemPrompt;
        HasPendingChanges = true;
    }

    [RelayCommand]
    public void SetConcisePromptPreset()
    {
        AiSystemPrompt = AiSettings.ConciseSystemPrompt;
        HasPendingChanges = true;
    }

    [RelayCommand]
    public void SetReviewerPromptPreset()
    {
        AiSystemPrompt = AiSettings.ReviewerSystemPrompt;
        HasPendingChanges = true;
    }

    [RelayCommand]
    public void SetTddPromptPreset()
    {
        AiSystemPrompt = AiSettings.TddArchitectSystemPrompt;
        HasPendingChanges = true;
    }

    [RelayCommand]
    public void SetLmStudioPreset()
    {
        AiProvider = AiProviderKind.LMStudio;
        AiEndpointUrl = AiSettings.DefaultLmStudioEndpoint;
        AiModelName = AiSettings.DefaultLmStudioModel;
        AiConnectionStatus = "Loaded preset: LM Studio (port 1234, google/gemma-4-e2b)";
        HasPendingChanges = true;
    }

    [RelayCommand]
    public void SetOllamaPreset()
    {
        AiProvider = AiProviderKind.Ollama;
        AiEndpointUrl = AiSettings.DefaultOllamaEndpoint;
        AiModelName = AiSettings.DefaultModelName;
        AiConnectionStatus = "Loaded preset: Ollama (port 11434, llama3.2)";
        HasPendingChanges = true;
    }

    partial void OnAiProviderChanged(AiProviderKind value)
    {
        HasPendingChanges = true;
        if (value == AiProviderKind.LMStudio && (string.IsNullOrWhiteSpace(AiEndpointUrl) || AiEndpointUrl == AiSettings.DefaultOllamaEndpoint))
        {
            AiEndpointUrl = AiSettings.DefaultLmStudioEndpoint;
            AiModelName = AiSettings.DefaultLmStudioModel;
        }
        else if (value == AiProviderKind.Ollama && (string.IsNullOrWhiteSpace(AiEndpointUrl) || AiEndpointUrl == AiSettings.DefaultLmStudioEndpoint))
        {
            AiEndpointUrl = AiSettings.DefaultOllamaEndpoint;
            AiModelName = AiSettings.DefaultModelName;
        }
    }
    partial void OnAiEndpointUrlChanged(string value) => HasPendingChanges = true;
    partial void OnAiModelNameChanged(string value) => HasPendingChanges = true;
    partial void OnAiApiKeyChanged(string? value) => HasPendingChanges = true;
    partial void OnAiTemperatureChanged(float value) => HasPendingChanges = true;
    partial void OnAiMaxOutputTokensChanged(int value) => HasPendingChanges = true;
    partial void OnAiAutoApproveEditsChanged(bool value) => HasPendingChanges = true;
    partial void OnAiSystemPromptChanged(string value)
    {
        HasPendingChanges = true;
        OnPropertyChanged(nameof(AiSystemPromptLength));
        OnPropertyChanged(nameof(AiPromptPresetName));
    }
}
