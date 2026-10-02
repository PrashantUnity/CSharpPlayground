using System;

namespace FrySharp.Sdk;

/// <summary>
/// Public API for registering UI contribution points and sending user notifications.
/// </summary>
public interface IUiApi
{
    /// <summary>Registers an item on the Activity Bar (Zone 1).</summary>
    IDisposable RegisterActivityBarItem(ActivityBarDescriptor descriptor);

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
}
