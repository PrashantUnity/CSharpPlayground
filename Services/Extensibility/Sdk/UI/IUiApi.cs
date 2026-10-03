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
}
