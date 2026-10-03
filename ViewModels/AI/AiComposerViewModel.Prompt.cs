using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Models.AI;
using PdfEditorApp.Plugins.CSharpEditor.Services.AI.Agent;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.AI;

public partial class AiComposerViewModel
{
    [ObservableProperty]
    private bool _isPromptEditorOpen;

    [ObservableProperty]
    private string _customSystemPrompt = string.Empty;

    [ObservableProperty]
    private string _activePromptPresetName = "Agent";

    public int CustomPromptLength => CustomSystemPrompt?.Length ?? 0;

    private void InitializePromptState()
    {
        CustomSystemPrompt = _settings.SystemPrompt ?? AiSettings.DefaultSystemPrompt;
        ActivePromptPresetName = AiSettings.GetPromptPresetName(CustomSystemPrompt);
    }

    private void OnSettingsStoreChanged(StudioSettings studioSettings)
    {
        if (studioSettings.Ai != null)
        {
            PdfEditorApp.Plugins.CSharpEditor.Services.Common.UiDispatchHelper.RunOnUi(() =>
            {
                ApplySettingsFromStore(studioSettings.Ai);
            });
        }
    }

    public void ApplySettingsFromStore(AiSettings ai)
    {
        _settings.Provider = ai.Provider;
        _settings.EndpointUrl = ai.EndpointUrl;
        _settings.ModelName = ai.ModelName;
        _settings.ApiKey = ai.ApiKey;
        _settings.Temperature = ai.Temperature;
        _settings.MaxOutputTokens = ai.MaxOutputTokens;
        _settings.AutoApproveEdits = ai.AutoApproveEdits;
        _settings.SystemPrompt = ai.SystemPrompt;

        SelectedModel = ai.ModelName;
        CustomSystemPrompt = ai.SystemPrompt ?? AiSettings.DefaultSystemPrompt;
        ActivePromptPresetName = AiSettings.GetPromptPresetName(CustomSystemPrompt);

        if (_coordinator != null)
        {
            _coordinator = new AiAgentExecutionCoordinator(_settings, _toolRegistry);
        }
    }

    [RelayCommand]
    public void TogglePromptEditor()
    {
        IsPromptEditorOpen = !IsPromptEditorOpen;
    }

    [RelayCommand]
    public void SetComposerDefaultPrompt()
    {
        CustomSystemPrompt = AiSettings.DefaultSystemPrompt;
        ApplyCustomPrompt();
    }

    [RelayCommand]
    public void SetComposerConcisePrompt()
    {
        CustomSystemPrompt = AiSettings.ConciseSystemPrompt;
        ApplyCustomPrompt();
    }

    [RelayCommand]
    public void SetComposerReviewerPrompt()
    {
        CustomSystemPrompt = AiSettings.ReviewerSystemPrompt;
        ApplyCustomPrompt();
    }

    [RelayCommand]
    public void SetComposerTddPrompt()
    {
        CustomSystemPrompt = AiSettings.TddArchitectSystemPrompt;
        ApplyCustomPrompt();
    }

    [RelayCommand]
    public void ResetComposerPrompt()
    {
        SetComposerDefaultPrompt();
    }

    [RelayCommand]
    public void ApplyCustomPrompt()
    {
        var text = string.IsNullOrWhiteSpace(CustomSystemPrompt)
            ? AiSettings.DefaultSystemPrompt
            : CustomSystemPrompt.Trim();

        _settings.SystemPrompt = text;
        ActivePromptPresetName = AiSettings.GetPromptPresetName(text);

        if (_coordinator != null)
        {
            _coordinator = new AiAgentExecutionCoordinator(_settings, _toolRegistry);
        }

        SaveSettingsToStoreIfAttached();
        StatusText = $"Updated system prompt ({ActivePromptPresetName})";
    }

    partial void OnCustomSystemPromptChanged(string value)
    {
        ActivePromptPresetName = AiSettings.GetPromptPresetName(value);
        OnPropertyChanged(nameof(CustomPromptLength));
    }

    private void SaveSettingsToStoreIfAttached()
    {
        if (_settingsStore != null)
        {
            var current = _settingsStore.GetSettings();
            current.Ai = _settings.Clone();
            _settingsStore.SaveSettings(current);
        }
    }
}
