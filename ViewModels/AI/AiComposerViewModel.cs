using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrySharp.Sdk;
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

    [ObservableProperty]
    private AiDockMode _dockMode = AiDockMode.FloatingOverlay;

    [ObservableProperty]
    private bool _isExtracted = false;

    [ObservableProperty]
    private double _windowOpacity = 1.0;

    [ObservableProperty]
    private string _customAccentColorHex = "#38BDF8";

    [ObservableProperty]
    private string _customBackgroundHex = "#1E1E26";

    [ObservableProperty]
    private double _customCornerRadius = 10;

    [ObservableProperty]
    private bool _autoAcceptDiffs = false;

    [ObservableProperty]
    private int _maxSteps = 25;

    [ObservableProperty]
    private bool _isProtectedFromCapture = false;

    [ObservableProperty]
    private bool _isGhostHidden = false;

    [ObservableProperty]
    private double _sideBarDockWidth = 380;

    [ObservableProperty]
    private double _bottomDockHeight = 280;

    [ObservableProperty]
    private GridLength _sideBarDockGridLength = new GridLength(0, GridUnitType.Pixel);

    [ObservableProperty]
    private GridLength _bottomDockGridLength = new GridLength(0, GridUnitType.Pixel);

    public bool IsOverlayVisible => IsVisible && !IsExtracted && !IsGhostHidden && DockMode == AiDockMode.FloatingOverlay;
    public bool IsSideBarDocked => IsVisible && !IsExtracted && !IsGhostHidden && DockMode == AiDockMode.DockedSideBar;
    public bool IsBottomDocked => IsVisible && !IsExtracted && !IsGhostHidden && DockMode == AiDockMode.DockedBottomDeck;
    public bool CanShowMinimizedCapsule => IsMinimized && DockMode == AiDockMode.FloatingOverlay;

    public bool CanDockToFloating => !IsExtracted && DockMode != AiDockMode.FloatingOverlay;
    public bool CanDockToSideBar => !IsExtracted && DockMode != AiDockMode.DockedSideBar;
    public bool CanDockToBottomDeck => !IsExtracted && DockMode != AiDockMode.DockedBottomDeck;
    public bool CanExtractToWindow => !IsExtracted;
    public bool CanMaximizeAndMinimize => !IsExtracted && DockMode == AiDockMode.FloatingOverlay;

    private void NotifyDockStateChanged()
    {
        if (IsSideBarDocked)
        {
            SideBarDockGridLength = new GridLength(SideBarDockWidth >= 200 ? SideBarDockWidth : 380, GridUnitType.Pixel);
        }
        else
        {
            if (SideBarDockGridLength.IsAbsolute && SideBarDockGridLength.Value >= 200)
            {
                SideBarDockWidth = SideBarDockGridLength.Value;
            }
            SideBarDockGridLength = new GridLength(0, GridUnitType.Pixel);
        }

        if (IsBottomDocked)
        {
            BottomDockGridLength = new GridLength(BottomDockHeight >= 150 ? BottomDockHeight : 280, GridUnitType.Pixel);
        }
        else
        {
            if (BottomDockGridLength.IsAbsolute && BottomDockGridLength.Value >= 150)
            {
                BottomDockHeight = BottomDockGridLength.Value;
            }
            BottomDockGridLength = new GridLength(0, GridUnitType.Pixel);
        }

        OnPropertyChanged(nameof(IsOverlayVisible));
        OnPropertyChanged(nameof(IsSideBarDocked));
        OnPropertyChanged(nameof(IsBottomDocked));
        OnPropertyChanged(nameof(CanShowMinimizedCapsule));
        OnPropertyChanged(nameof(CanDockToFloating));
        OnPropertyChanged(nameof(CanDockToSideBar));
        OnPropertyChanged(nameof(CanDockToBottomDeck));
        OnPropertyChanged(nameof(CanExtractToWindow));
        OnPropertyChanged(nameof(CanMaximizeAndMinimize));
    }

    partial void OnSideBarDockGridLengthChanged(GridLength value)
    {
        if (value.IsAbsolute && value.Value >= 200)
        {
            _sideBarDockWidth = value.Value;
        }
    }

    partial void OnBottomDockGridLengthChanged(GridLength value)
    {
        if (value.IsAbsolute && value.Value >= 150)
        {
            _bottomDockHeight = value.Value;
        }
    }

    partial void OnIsVisibleChanged(bool value) => NotifyDockStateChanged();
    partial void OnIsMinimizedChanged(bool value) => NotifyDockStateChanged();
    partial void OnIsExtractedChanged(bool value) => NotifyDockStateChanged();
    partial void OnDockModeChanged(AiDockMode value)
    {
        NotifyDockStateChanged();
        DockModeChanged?.Invoke(value);
    }
    partial void OnIsGhostHiddenChanged(bool value) => NotifyDockStateChanged();
    partial void OnIsProtectedFromCaptureChanged(bool value) => CaptureProtectionChanged?.Invoke(value);
    partial void OnIsAgentModeChanged(bool value)
    {
        StatusText = value ? "Agent Mode: Autonomous Tools" : "Chat Mode: Direct Assistant";
    }

    public event Action<AiDockMode>? DockModeChanged;
    public event Action<AiStyleOptions>? StyleChanged;
    public event Action<bool>? CaptureProtectionChanged;
    public Action<AiWindowOptions?>? WindowExtractionRequested { get; set; }
    public Action? ReDockRequested { get; set; }

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
        InitializeConnectionState();
        InitializeMentions();
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

            if (AutoAcceptDiffs)
            {
                item.IsAccepted = true;
                SessionModifiedFiles.Clear();
                OnPropertyChanged(nameof(HasModifiedFiles));
            }
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
        EndpointUrl = _settings.EndpointUrl;
        SelectedModel = _settings.ModelName;
        StatusText = "Switched to LM Studio (1234)";
        if (_coordinator != null)
        {
            _coordinator = new AiAgentExecutionCoordinator(_settings, _toolRegistry);
        }
        await PingEndpointAsync();
    }

    [RelayCommand]
    public async Task SwitchToOllamaAsync()
    {
        _settings.Provider = AiProviderKind.Ollama;
        _settings.EndpointUrl = AiSettings.DefaultOllamaEndpoint;
        _settings.ModelName = AiSettings.DefaultModelName;
        EndpointUrl = _settings.EndpointUrl;
        SelectedModel = _settings.ModelName;
        StatusText = "Switched to Ollama (11434)";
        if (_coordinator != null)
        {
            _coordinator = new AiAgentExecutionCoordinator(_settings, _toolRegistry);
        }
        await PingEndpointAsync();
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
            activeContext = EnrichContextWithMentions(prompt, activeContext);

            await Task.Run(async () =>
            {
                var result = await _coordinator.ExecuteTaskAsync(
                    userPrompt: prompt,
                    activeContext: activeContext,
                    isAgentMode: IsAgentMode,
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
    public void ApplyModifiedFile(ModifiedFileItem? item)
    {
        if (item == null) return;
        if (_toolRegistry != null && !string.IsNullOrEmpty(item.ModifiedContent))
        {
            _toolRegistry.ModifyActiveDocument(item.ModifiedContent, "Applied by user from AI Composer");
            item.IsAccepted = true;
            SessionModifiedFiles.Remove(item);
            OnPropertyChanged(nameof(HasModifiedFiles));
            StatusText = $"Applied {item.FileName} to editor";
        }
    }

    [RelayCommand]
    public void InsertCodeAtCursor(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return;
        if (_toolRegistry != null)
        {
            _toolRegistry.InsertAtCursor(code);
            StatusText = "Inserted code at cursor";
        }
    }

    [RelayCommand]
    public void ClearChat()
    {
        Messages.Clear();
        SessionModifiedFiles.Clear();
        _coordinator?.ResetConversation();
        OnPropertyChanged(nameof(HasModifiedFiles));
        StatusText = "Ready";
    }

    [RelayCommand]
    public void DockTo(AiDockMode mode)
    {
        IsVisible = true;
        IsMinimized = false;

        if (DockMode == mode && !IsExtracted) return;

        if (mode == AiDockMode.ExtractedWindow)
        {
            ExtractToWindow();
            return;
        }

        if (IsExtracted)
        {
            ReDock(mode);
            return;
        }

        DockMode = mode;
    }

    [RelayCommand]
    public void DockToSideBar() => DockTo(AiDockMode.DockedSideBar);

    [RelayCommand]
    public void DockToBottomDeck() => DockTo(AiDockMode.DockedBottomDeck);

    [RelayCommand]
    public void DockToFloating() => DockTo(AiDockMode.FloatingOverlay);

    [RelayCommand]
    public void ExtractToWindow(AiWindowOptions? options = null)
    {
        IsExtracted = true;
        DockMode = AiDockMode.ExtractedWindow;
        DockModeChanged?.Invoke(DockMode);
        WindowExtractionRequested?.Invoke(options);
    }

    [RelayCommand]
    public void ReDock(AiDockMode targetMode = AiDockMode.FloatingOverlay)
    {
        IsExtracted = false;
        DockMode = targetMode;
        ReDockRequested?.Invoke();
        DockModeChanged?.Invoke(DockMode);
    }

    public void SetStyle(AiStyleOptions style)
    {
        ArgumentNullException.ThrowIfNull(style);
        if (style.Opacity.HasValue) WindowOpacity = Math.Clamp(style.Opacity.Value, 0.1, 1.0);
        if (!string.IsNullOrWhiteSpace(style.AccentColorHex)) CustomAccentColorHex = style.AccentColorHex;
        if (!string.IsNullOrWhiteSpace(style.BackgroundHex)) CustomBackgroundHex = style.BackgroundHex;
        if (style.CornerRadius.HasValue) CustomCornerRadius = Math.Clamp(style.CornerRadius.Value, 0, 32);
        if (style.Width.HasValue) WindowWidth = Math.Clamp(style.Width.Value, 320, 1600);
        if (style.Height.HasValue) WindowHeight = Math.Clamp(style.Height.Value, 300, 1400);
        if (style.PositionX.HasValue) PositionX = style.PositionX.Value;
        if (style.PositionY.HasValue) PositionY = style.PositionY.Value;

        StyleChanged?.Invoke(style);
    }

    public void ConfigurePolicy(AiPolicyOptions policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (policy.AutoAcceptDiffs.HasValue) AutoAcceptDiffs = policy.AutoAcceptDiffs.Value;
        if (policy.MaxSteps.HasValue) MaxSteps = Math.Clamp(policy.MaxSteps.Value, 1, 100);
        if (policy.Temperature.HasValue && _settings != null) _settings.Temperature = (float)policy.Temperature.Value;
    }

    public void SetCaptureProtection(bool enabled)
    {
        IsProtectedFromCapture = enabled;
    }

    [RelayCommand]
    public void ToggleCaptureProtection()
    {
        IsProtectedFromCapture = !IsProtectedFromCapture;
        StatusText = IsProtectedFromCapture ? "Protected from screen capture" : "Capture protection disabled";
    }
}
