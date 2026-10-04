using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FrySharp.Sdk;

/// <summary>
/// Public API for registering UI contribution points, dialogs, modals, and notifications.
/// </summary>
public interface IUiApi
{
    /// <summary>Dialogs, input prompts, confirmation boxes, and file pickers.</summary>
    IDialogApi Dialogs { get; }

    /// <summary>Fires whenever UI contributions, slots, or widgets are registered or unregistered.</summary>
    event Action? ContributionsChanged;

    /// <summary>Registers an item on the Activity Bar (Zone 1).</summary>
    IDisposable RegisterActivityBarItem(ActivityBarDescriptor descriptor);

    /// <summary>Registers a full custom Side Bar panel (Zone 2) with an Activity Bar launcher button (Zone 1).</summary>
    IDisposable RegisterSideBarView(SideBarViewDescriptor descriptor);

    /// <summary>Gets all registered custom Side Bar views.</summary>
    IReadOnlyList<SideBarViewDescriptor> SideBarViews => Array.Empty<SideBarViewDescriptor>();

    /// <summary>Registers a widget in the Status Bar (Zone 5).</summary>
    IDisposable RegisterStatusBarWidget(StatusBarWidgetDescriptor descriptor);

    /// <summary>Registers a tab in the Bottom Tool Deck (Zone 4).</summary>
    IDisposable RegisterBottomDeckTab(BottomDeckTabDescriptor descriptor);

    /// <summary>Gets all registered custom Bottom Tool Deck tabs.</summary>
    IReadOnlyList<BottomDeckTabDescriptor> BottomDeckTabs => Array.Empty<BottomDeckTabDescriptor>();

    /// <summary>Registers a custom action or button in the Editor Area toolbar (Zone 3).</summary>
    IDisposable RegisterEditorToolbarItem(EditorToolbarItemDescriptor descriptor) => new EmptyDisposable();

    /// <summary>Gets all registered Editor Area toolbar items.</summary>
    IReadOnlyList<EditorToolbarItemDescriptor> EditorToolbarItems => Array.Empty<EditorToolbarItemDescriptor>();

    /// <summary>Registers a dynamic floating overlay or HUD into the studio workspace.</summary>
    IDisposable RegisterFloatingOverlay(FloatingOverlayDescriptor descriptor) => new EmptyDisposable();

    /// <summary>Gets all active floating overlays.</summary>
    IReadOnlyList<FloatingOverlayDescriptor> FloatingOverlays => Array.Empty<FloatingOverlayDescriptor>();

    /// <summary>Registers an extension button or custom widget into the AI Composer interface.</summary>
    IDisposable RegisterComposerAction(ComposerActionDescriptor descriptor) => new EmptyDisposable();

    /// <summary>Gets all registered AI Composer actions.</summary>
    IReadOnlyList<ComposerActionDescriptor> ComposerActions => Array.Empty<ComposerActionDescriptor>();

    /// <summary>Instantiates a visual Control from an Avalonia XAML snippet protected by an error boundary.</summary>
    object? CreateComponent(string xaml, string? title = null) => null;

    /// <summary>Displays an in-app toast notification.</summary>
    void ShowNotification(NotificationMessage notification);

    /// <summary>Displays an informational notification.</summary>
    void ShowInfo(string message, string title = "Customization");

    /// <summary>Displays a success notification.</summary>
    void ShowSuccess(string message, string title = "Customization");

    /// <summary>Displays a warning notification.</summary>
    void ShowWarning(string message, string title = "Customization");

    /// <summary>Displays an error notification.</summary>
    void ShowError(string message, string title = "Customization");

    /// <summary>Prompts user for text input.</summary>
    Task<string?> PromptAsync(string message, string title = "Prompt", string defaultValue = "", string? placeholder = null) =>
        Dialogs.PromptAsync(message, title, defaultValue, placeholder);

    /// <summary>Prompts user for confirmation.</summary>
    Task<bool> ConfirmAsync(string message, string title = "Confirm") =>
        Dialogs.ConfirmAsync(message, title);

    /// <summary>Shows an informational alert box.</summary>
    Task AlertAsync(string message, string title = "Alert") =>
        Dialogs.AlertAsync(message, title);

    /// <summary>Prompts user to pick a file.</summary>
    Task<string?> PickFileAsync(string title = "Select File", IReadOnlyList<string>? extensions = null) =>
        Dialogs.PickFileAsync(title, extensions);

    /// <summary>Prompts user to pick a folder.</summary>
    Task<string?> PickFolderAsync(string title = "Select Folder") =>
        Dialogs.PickFolderAsync(title);

    /// <summary>Reference to the active Avalonia TopLevel or Window (for advanced visual tree operations).</summary>
    object? ActiveTopLevel => null;

    /// <summary>Reference to the main application Window if running as a standalone app.</summary>
    object? MainWindow => null;

    /// <summary>Visual tree querying, control discovery, and dynamic adorner injection.</summary>
    IVisualTreeApi VisualTree => new EmptyVisualTreeApi();

    private sealed class EmptyDisposable : IDisposable
    {
        public void Dispose() { }
    }

    private sealed class EmptyVisualTreeApi : IVisualTreeApi
    {
        public object? FindControl(string name) => null;
        public IReadOnlyList<VisualElementInfo> DumpVisualTree(int maxDepth = 6) => Array.Empty<VisualElementInfo>();
        public string DumpVisualTreeSummary(int maxDepth = 6) => "Visual tree unavailable.";
        public IDisposable AttachAdorner(object target, object adorner) => new EmptyDisposable();
    }
}
