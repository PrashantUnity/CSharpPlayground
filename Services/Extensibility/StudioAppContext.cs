using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Commands;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Dialogs;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Editor;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Events;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Hooks;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Results;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.State;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Terminal;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.UI;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Workspace;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;

/// <summary>
/// Root implementation of IStudioApp. Central hub wiring together theming,
/// commands, editor manipulation, UI contribution points, hooks, state bag,
/// workspace automation, terminal/CLI execution, results (.Dump), and event bus.
/// </summary>
public class StudioAppContext : IStudioApp
{
    private static StudioAppContext? _instance;
    private static readonly object _instanceLock = new();

    /// <summary>
    /// Global ambient instance accessible as 'App' or 'Studio' in user scripts.
    /// </summary>
    public static StudioAppContext Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_instanceLock)
                {
                    _instance ??= new StudioAppContext();
                }
            }
            return _instance;
        }
        internal set
        {
            lock (_instanceLock)
            {
                _instance = value;
            }
        }
    }

    public DynamicThemeEngine ThemeEngine { get; }
    public ExtensibilityCommandPipeline CommandPipeline { get; }
    public ExtensibilityEditorService EditorService { get; }
    public ExtensibilityUiService UiService { get; }
    public ExtensibilityHookRegistry HookRegistry { get; }
    public InMemoryStateBag StateBag { get; }
    public ExtensibilityWorkspaceService WorkspaceService { get; }
    public ExtensibilityTerminalService TerminalService { get; }
    public ExtensibilityResultsService ResultsService { get; }
    public ExtensibilityEventBus EventBus { get; }

    private CustomizationManager? _customizationManager;
    public CustomizationManager CustomizationManager
    {
        get => _customizationManager ??= new CustomizationManager();
        set => _customizationManager = value;
    }

    private Extensions.ExtensionManager? _extensionManager;
    public Extensions.ExtensionManager ExtensionManager
    {
        get => _extensionManager ??= new Extensions.ExtensionManager();
        set => _extensionManager = value;
    }

    public IThemeApi Theme => ThemeEngine;
    public IThemeApi Themes => ThemeEngine;
    public ICommandApi Commands => CommandPipeline;
    public IEditorApi Editor => EditorService;
    public IUiApi UI => UiService;
    public IHookApi Hooks => HookRegistry;
    public IStateBag State => StateBag;
    public IWorkspaceApi Workspace => WorkspaceService;
    public ITerminalApi Terminal => TerminalService;
    public IResultsApi Results => ResultsService;
    public IEventBusApi Events => EventBus;
    public IDialogApi Dialogs => UiService.Dialogs;

    public StudioAppContext()
    {
        ThemeEngine = new DynamicThemeEngine();
        CommandPipeline = new ExtensibilityCommandPipeline();
        EditorService = new ExtensibilityEditorService();
        UiService = new ExtensibilityUiService();
        HookRegistry = new ExtensibilityHookRegistry();
        StateBag = new InMemoryStateBag();
        WorkspaceService = new ExtensibilityWorkspaceService();
        TerminalService = new ExtensibilityTerminalService();
        ResultsService = new ExtensibilityResultsService();
        EventBus = new ExtensibilityEventBus();

        // Forward theme changed events from theme engine to hook registry
        ThemeEngine.ThemeChanged += themeId =>
        {
            HookRegistry.InvokeThemeChanged(themeId);
        };

        // Built-in showcase command: Floating Snake Game window
        CommandPipeline.Register(
            "game.snake",
            "Play Snake Game (Floating Window)",
            () =>
            {
                var window = new SnakeGameWindow(this);
                window.Show();
            },
            gesture: "Ctrl+Alt+G",
            category: "Games");
    }
}
