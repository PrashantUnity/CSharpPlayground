using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.AI;
using PdfEditorApp.Plugins.CSharpEditor.Models.AI;
using PdfEditorApp.Plugins.CSharpEditor.Services.AI.Agent;
using PdfEditorApp.Plugins.CSharpEditor.Services.AI.Core;
using PdfEditorApp.Plugins.CSharpEditor.Services.AI.Tools;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.AI;

/// <summary>
/// ViewModel for the app-wide floating Cursor-like Composer &amp; AI Agent overlay.
/// </summary>
public partial class AiComposerViewModel : ObservableObject
{
    private readonly PdfEditorApp.Plugins.CSharpEditor.Services.Settings.StudioSettingsStore? _settingsStore;
    private readonly AiSettings _settings;
    private readonly AiModelDiscoveryService _discoveryService;
    private AiAgentToolRegistry? _toolRegistry;
    private AiAgentExecutionCoordinator? _coordinator;
    private CancellationTokenSource? _activeCts;

    [ObservableProperty]
    private bool _isVisible = false;

    [ObservableProperty]
    private bool _isMinimized = false;

    [ObservableProperty]
    private bool _isAgentMode = true;

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private string _promptText = string.Empty;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _activeContextSummary = string.Empty;

    [ObservableProperty]
    private double _positionX = 100;

    [ObservableProperty]
    private double _positionY = 60;

    [ObservableProperty]
    private double _windowWidth = 520;

    [ObservableProperty]
    private double _windowHeight = 650;

    [ObservableProperty]
    private string _selectedModel;

    public ObservableCollection<string> AvailableModels { get; } = new();
    public ObservableCollection<ChatMessageItem> Messages { get; } = new();
    public ObservableCollection<ModifiedFileItem> SessionModifiedFiles { get; } = new();

    public AiSettings Settings => _settings;

    public bool HasModifiedFiles => SessionModifiedFiles.Count > 0;

    public AiComposerViewModel(
        AiSettings? settings = null,
        AiModelDiscoveryService? discoveryService = null,
        PdfEditorApp.Plugins.CSharpEditor.Services.Settings.StudioSettingsStore? settingsStore = null)
    {
        _settingsStore = settingsStore;
        if (_settingsStore != null)
        {
            _settings = settings ?? _settingsStore.GetSettings().Ai ?? new AiSettings();
            _settingsStore.SettingsChanged += OnSettingsStoreChanged;
        }
        else
        {
            _settings = settings ?? new AiSettings();
        }

        _discoveryService = discoveryService ?? new AiModelDiscoveryService();
        _selectedModel = _settings.ModelName;

        AvailableModels.Add(_settings.ModelName);
        InitializePromptState();
    }

    /// <summary>
    /// Attaches the active studio VM and storage services to the agent tool pipeline.
    /// </summary>
    public void InitializeServices(
        CSharpCodeStudioViewModel? codeStudioVm,
        PdfEditorApp.Plugins.CSharpEditor.Services.Storage.IScriptStorageService? storageService,
        PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn.RoslynCompilerService? compilerService,
        PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.CustomizationManager? customizationManager)
    {
        _toolRegistry = new AiAgentToolRegistry(
            storageService: storageService,
            codeStudioViewModel: codeStudioVm,
            compilerService: compilerService,
            customizationManager: customizationManager,
            onFileModified: OnFileModified);

        _coordinator = new AiAgentExecutionCoordinator(_settings, _toolRegistry);
        _ = RefreshModelsAsync();
    }

    private void OnFileModified(ModifiedFileItem item)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var existing = SessionModifiedFiles.FirstOrDefault(f => f.FilePath.Equals(item.FilePath, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                SessionModifiedFiles.Remove(existing);
            }
            SessionModifiedFiles.Add(item);
            OnPropertyChanged(nameof(HasModifiedFiles));
        });
    }

    [RelayCommand]
    public async Task RefreshModelsAsync()
    {
        try
        {
            var models = await _discoveryService.GetAvailableModelsAsync(_settings.EndpointUrl, _settings.Provider, _settings.ApiKey);
            if (models.Count > 0)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    AvailableModels.Clear();
                    foreach (var m in models) AvailableModels.Add(m);
                    if (!AvailableModels.Contains(SelectedModel))
                    {
                        SelectedModel = AvailableModels.First();
                    }
                });
            }
        }
        catch
        {
            // Endpoint offline or unreachable
        }
    }

    partial void OnSelectedModelChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            _settings.ModelName = value;
            if (_coordinator != null)
            {
                _coordinator = new AiAgentExecutionCoordinator(_settings, _toolRegistry);
            }
        }
    }

    [RelayCommand]
    public async Task SwitchToLmStudioAsync()
    {
        _settings.Provider = AiProviderKind.LMStudio;
        _settings.EndpointUrl = AiSettings.DefaultLmStudioEndpoint;
        _settings.ModelName = AiSettings.DefaultLmStudioModel;
        SelectedModel = _settings.ModelName;
        StatusText = "Switched to LM Studio (1234)";
        if (_coordinator != null)
        {
            _coordinator = new AiAgentExecutionCoordinator(_settings, _toolRegistry);
        }
        await RefreshModelsAsync();
    }

    [RelayCommand]
    public async Task SwitchToOllamaAsync()
    {
        _settings.Provider = AiProviderKind.Ollama;
        _settings.EndpointUrl = AiSettings.DefaultOllamaEndpoint;
        _settings.ModelName = AiSettings.DefaultModelName;
        SelectedModel = _settings.ModelName;
        StatusText = "Switched to Ollama (11434)";
        if (_coordinator != null)
        {
            _coordinator = new AiAgentExecutionCoordinator(_settings, _toolRegistry);
        }
        await RefreshModelsAsync();
    }

    [RelayCommand]
    public async Task SendMessageAsync()
    {
        var prompt = PromptText?.Trim();
        if (string.IsNullOrWhiteSpace(prompt) || IsGenerating) return;

        PromptText = string.Empty;
        IsGenerating = true;
        StatusText = "Thinking...";

        var userMessage = new ChatMessageItem
        {
            Role = ChatRole.User,
            Content = prompt
        };
        Messages.Add(userMessage);

        var assistantMessage = new ChatMessageItem
        {
            Role = ChatRole.Assistant,
            IsStreaming = true
        };
        Messages.Add(assistantMessage);

        _activeCts = new CancellationTokenSource();

        try
        {
            if (_coordinator == null)
            {
                _coordinator = new AiAgentExecutionCoordinator(_settings, _toolRegistry);
            }

            string? activeContext = _toolRegistry?.GetActiveFileContext();

            await Task.Run(async () =>
            {
                var result = await _coordinator.ExecuteTaskAsync(
                    userPrompt: prompt,
                    activeContext: activeContext,
                    onTokenChunk: chunk =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            assistantMessage.Content += chunk;
                        });
                    },
                    onReasoningChunk: rChunk =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            assistantMessage.ReasoningContent += rChunk;
                        });
                    },
                    onStepUpdate: step =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            if (!assistantMessage.Steps.Contains(step))
                            {
                                assistantMessage.Steps.Add(step);
                            }
                            StatusText = step.Title;
                        });
                    },
                    cancellationToken: _activeCts.Token);

                Dispatcher.UIThread.Post(() =>
                {
                    assistantMessage.Content = result.Content;
                    assistantMessage.ReasoningContent = result.ReasoningContent;
                    assistantMessage.ErrorMessage = result.ErrorMessage;
                    assistantMessage.IsStreaming = false;

                    foreach (var f in result.ModifiedFiles)
                    {
                        if (!assistantMessage.ModifiedFiles.Contains(f))
                        {
                            assistantMessage.ModifiedFiles.Add(f);
                        }
                    }
                });
            }, _activeCts.Token);
        }
        catch (OperationCanceledException)
        {
            assistantMessage.ErrorMessage = "Stopped by user.";
        }
        catch (Exception ex)
        {
            assistantMessage.ErrorMessage = ex.Message;
        }
        finally
        {
            assistantMessage.IsStreaming = false;
            IsGenerating = false;
            StatusText = "Ready";
            _activeCts = null;
        }
    }

    [RelayCommand]
    public void StopGeneration()
    {
        _activeCts?.Cancel();
    }

    [RelayCommand]
    public void ToggleFloating()
    {
        if (IsMinimized)
        {
            IsMinimized = false;
            IsVisible = true;
        }
        else
        {
            IsVisible = !IsVisible;
        }
    }

    [RelayCommand]
    public void ToggleMinimize()
    {
        IsMinimized = !IsMinimized;
    }

    [RelayCommand]
    public void Close()
    {
        IsVisible = false;
    }

    [RelayCommand]
    public void AcceptAllChanges()
    {
        foreach (var file in SessionModifiedFiles)
        {
            file.IsAccepted = true;
        }
        SessionModifiedFiles.Clear();
        OnPropertyChanged(nameof(HasModifiedFiles));
    }

    [RelayCommand]
    public void RejectAllChanges()
    {
        _coordinator?.RollbackSession();
        SessionModifiedFiles.Clear();
        OnPropertyChanged(nameof(HasModifiedFiles));
    }

    [RelayCommand]
    public void ClearChat()
    {
        Messages.Clear();
        SessionModifiedFiles.Clear();
        OnPropertyChanged(nameof(HasModifiedFiles));
        StatusText = "Ready";
    }
}
