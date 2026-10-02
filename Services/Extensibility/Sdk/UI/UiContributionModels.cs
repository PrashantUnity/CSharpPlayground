using System;

namespace FrySharp.Sdk;

/// <summary>
/// Severity level for desktop and studio notifications.
/// </summary>
public enum NotificationSeverity
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
/// Model for displaying toast notifications to the user.
/// </summary>
public class NotificationMessage
{
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public NotificationSeverity Severity { get; init; } = NotificationSeverity.Info;
    public TimeSpan Duration { get; init; } = TimeSpan.FromSeconds(4);
}

/// <summary>
/// Descriptor for registering a status bar widget into Zone 5.
/// </summary>
public class StatusBarWidgetDescriptor
{
    public string Id { get; init; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? Tooltip { get; set; }
    public Action? OnClick { get; init; }
    public int Priority { get; init; } = 50;
    public bool IsVisible { get; set; } = true;
}

/// <summary>
/// Descriptor for registering an Activity Bar rail item into Zone 1.
/// </summary>
public class ActivityBarDescriptor
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string IconKind { get; init; } = "Extension";
    public Action OnClick { get; init; } = null!;
    public int Order { get; init; } = 50;
}

/// <summary>
/// Descriptor for registering a tab into the Bottom Tool Deck (Zone 4).
/// </summary>
public class BottomDeckTabDescriptor
{
    public string Id { get; init; } = string.Empty;
    public string Header { get; init; } = string.Empty;
    public Func<object> ContentFactory { get; init; } = null!;
    public Action? OnSelected { get; init; }
}
