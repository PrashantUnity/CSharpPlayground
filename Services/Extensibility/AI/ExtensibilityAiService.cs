using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.AI;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.AI;

/// <summary>
/// Root implementation of <see cref="IAiApi"/> bridging the public extensibility facade
/// to the internal <see cref="AiComposerViewModel"/>, window extraction, and tool execution.
/// </summary>
public class ExtensibilityAiService : IAiApi
{
    private readonly Func<AiComposerViewModel?> _composerVmResolver;

    private AiComposerViewModel? Vm => _composerVmResolver();

    public event Action<AiDockMode>? DockModeChanged;
    public event Action<AiStyleOptions>? StyleChanged;
    public event Action<bool>? CaptureProtectionChanged;

    private readonly Func<IUiApi>? _uiApiResolver;
    private IDisposable? _activeDockRegistration;

    private IUiApi UI => _uiApiResolver?.Invoke() ?? StudioAppContext.Instance.UI;

    public ExtensibilityAiService(Func<AiComposerViewModel?> composerVmResolver, Func<IUiApi>? uiApiResolver = null)
    {
        _composerVmResolver = composerVmResolver ?? throw new ArgumentNullException(nameof(composerVmResolver));
        _uiApiResolver = uiApiResolver;
    }

    public void AttachViewModel(AiComposerViewModel vm)
    {
        vm.DockModeChanged += mode =>
        {
            HandleDockModeChanged(mode);
            DockModeChanged?.Invoke(mode);
        };
        vm.StyleChanged += style => StyleChanged?.Invoke(style);
        vm.CaptureProtectionChanged += isProtected => CaptureProtectionChanged?.Invoke(isProtected);
        vm.WindowExtractionRequested ??= options =>
        {
            PdfEditorApp.Plugins.CSharpEditor.Controls.AI.AiComposerWindow.ShowExtracted(vm, options);
        };
        vm.ReDockRequested ??= () =>
        {
            PdfEditorApp.Plugins.CSharpEditor.Controls.AI.AiComposerWindow.CloseExtracted(vm);
        };
    }

    private void HandleDockModeChanged(AiDockMode mode)
    {
        _activeDockRegistration?.Dispose();
        _activeDockRegistration = null;

        var vm = Vm;
        if (vm == null) return;

        switch (mode)
        {
            case AiDockMode.DockedSideBar:
                _activeDockRegistration = UI.RegisterSideBarView(new SideBarViewDescriptor
                {
                    Id = "ai.sidebar.panel",
                    Title = "AI ASSISTANT",
                    Order = 10,
                    ContentFactory = () => new PdfEditorApp.Plugins.CSharpEditor.Controls.AI.StudioFloatingComposerControl { DataContext = vm }
                });
                break;

            case AiDockMode.DockedBottomDeck:
                _activeDockRegistration = UI.RegisterBottomDeckTab(new BottomDeckTabDescriptor
                {
                    Id = "ai.bottom.tab",
                    Header = "AI AGENT",
                    ContentFactory = () => new PdfEditorApp.Plugins.CSharpEditor.Controls.AI.StudioFloatingComposerControl { DataContext = vm }
                });
                break;

            case AiDockMode.ExtractedWindow:
                break;

            case AiDockMode.FloatingOverlay:
            default:
                break;
        }
    }

    private static void RunOnUI(Action action)
    {
        if (Avalonia.Application.Current != null && !Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(action);
        }
        else
        {
            action();
        }
    }

    public AiDockMode DockMode => Vm?.DockMode ?? AiDockMode.FloatingOverlay;

    public bool IsExtracted => Vm?.IsExtracted ?? false;

    public bool IsVisible
    {
        get => Vm?.IsVisible ?? false;
        set
        {
            if (Vm != null)
            {
                RunOnUI(() => Vm.IsVisible = value);
            }
        }
    }

    public bool IsMinimized
    {
        get => Vm?.IsMinimized ?? false;
        set
        {
            if (Vm != null)
            {
                RunOnUI(() => Vm.IsMinimized = value);
            }
        }
    }

    public string ActivePersona => Vm?.ActivePromptPresetName ?? "Agent";

    public string ActiveModel => Vm?.SelectedModel ?? string.Empty;

    public double Opacity => Vm?.WindowOpacity ?? 1.0;

    public bool IsProtectedFromCapture
    {
        get => Vm?.IsProtectedFromCapture ?? false;
        set => SetCaptureProtection(value);
    }

    public AiStyleOptions CurrentStyle => new()
    {
        AccentColorHex = Vm?.CustomAccentColorHex,
        BackgroundHex = Vm?.CustomBackgroundHex,
        Opacity = Vm?.WindowOpacity,
        CornerRadius = Vm?.CustomCornerRadius,
        Width = Vm?.WindowWidth,
        Height = Vm?.WindowHeight,
        PositionX = Vm?.PositionX,
        PositionY = Vm?.PositionY
    };

    public AiPolicyOptions CurrentPolicy => new()
    {
        AutoAcceptDiffs = Vm?.AutoAcceptDiffs,
        MaxSteps = Vm?.MaxSteps
    };

    public void DockTo(AiDockMode mode)
    {
        RunOnUI(() =>
        {
            Vm?.DockTo(mode);
        });
    }

    public void ExtractToWindow(AiWindowOptions? options = null)
    {
        RunOnUI(() =>
        {
            Vm?.ExtractToWindow(options);
        });
    }

    public void ReDock(AiDockMode targetMode = AiDockMode.FloatingOverlay)
    {
        RunOnUI(() =>
        {
            Vm?.ReDock(targetMode);
        });
    }

    public void SetStyle(AiStyleOptions style)
    {
        ArgumentNullException.ThrowIfNull(style);
        RunOnUI(() =>
        {
            Vm?.SetStyle(style);
        });
    }

    public void SetOpacity(double opacity)
    {
        SetStyle(new AiStyleOptions { Opacity = opacity });
    }

    public void SetDimensions(double width, double height)
    {
        SetStyle(new AiStyleOptions { Width = width, Height = height });
    }

    public void SetPosition(double x, double y)
    {
        SetStyle(new AiStyleOptions { PositionX = x, PositionY = y });
    }

    public void SetPersona(string presetName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(presetName);
        RunOnUI(() =>
        {
            if (Vm == null) return;
            switch (presetName.Trim().ToLowerInvariant())
            {
                case "agent":
                case "default":
                    Vm.SetComposerDefaultPrompt();
                    break;
                case "concise":
                case "diff":
                    Vm.SetComposerConcisePrompt();
                    break;
                case "reviewer":
                case "review":
                    Vm.SetComposerReviewerPrompt();
                    break;
                case "tdd":
                case "test":
                    Vm.SetComposerTddPrompt();
                    break;
                default:
                    Vm.ActivePromptPresetName = presetName;
                    break;
            }
        });
    }

    public void SetCustomPrompt(string systemPrompt)
    {
        ArgumentNullException.ThrowIfNull(systemPrompt);
        RunOnUI(() =>
        {
            if (Vm == null) return;
            Vm.CustomSystemPrompt = systemPrompt;
            Vm.ApplyCustomPrompt();
        });
    }

    public void SetModel(string modelName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);
        RunOnUI(() =>
        {
            if (Vm == null) return;
            Vm.SelectedModel = modelName;
        });
    }

    public void ConfigurePolicy(AiPolicyOptions policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        RunOnUI(() =>
        {
            Vm?.ConfigurePolicy(policy);
        });
    }

    public async Task SendMessageAsync(string prompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        if (Vm == null) return;

        if (Avalonia.Application.Current != null && !Dispatcher.UIThread.CheckAccess())
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                Vm.PromptText = prompt;
                await Vm.SendMessageAsync();
            });
        }
        else
        {
            Vm.PromptText = prompt;
            await Vm.SendMessageAsync();
        }
    }

    public void ClearChat()
    {
        RunOnUI(() =>
        {
            Vm?.ClearChat();
        });
    }

    public void SetCaptureProtection(bool enabled)
    {
        RunOnUI(() =>
        {
            Vm?.SetCaptureProtection(enabled);

            // Also apply OS capture protection to the main IDE window if active
            if (StudioAppContext.Instance.UI.MainWindow is Avalonia.Controls.Window mainWin)
            {
                PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.UI.WindowProtectionService.SetProtected(mainWin, enabled);
            }
        });
    }

    public async Task<byte[]> CaptureWorkspaceScreenshotAsync(CaptureTarget target = CaptureTarget.WorkspaceArea, bool excludeSelf = true)
    {
        return await PdfEditorApp.Plugins.CSharpEditor.Services.AI.Vision.StudioScreenshotService.CaptureAsync(target, excludeSelf, Vm);
    }

    public async Task<string> CaptureWorkspaceScreenshotToFileAsync(string? outputPath = null, CaptureTarget target = CaptureTarget.WorkspaceArea, bool excludeSelf = true)
    {
        return await PdfEditorApp.Plugins.CSharpEditor.Services.AI.Vision.StudioScreenshotService.CaptureToFileAsync(outputPath, target, excludeSelf, Vm);
    }
}
