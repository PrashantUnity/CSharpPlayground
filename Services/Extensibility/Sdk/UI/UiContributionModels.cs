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
/// Descriptor for contributing a full custom Primary Side Bar panel (Zone 2) activated by Zone 1.
/// </summary>
public class SideBarViewDescriptor
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string IconKind { get; init; } = "Extension";
    public Func<object> ContentFactory { get; init; } = null!;
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

/// <summary>
/// Descriptor for contributing an action button or widget into the Editor Area toolbar (Zone 3).
/// </summary>
public class EditorToolbarItemDescriptor
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string IconKind { get; init; } = "CogOutline";
    public Action? OnClick { get; init; }
    public Func<object>? CustomContentFactory { get; init; }
    public int Order { get; init; } = 50;
    public bool IsVisible { get; set; } = true;
}

/// <summary>
/// Descriptor for contributing a dynamic floating overlay or HUD into the studio workspace.
/// </summary>
public class FloatingOverlayDescriptor
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public Func<object> ContentFactory { get; init; } = null!;
    public double X { get; set; } = 24;
    public double Y { get; set; } = 24;
    public double? Width { get; set; }
    public double? Height { get; set; }
    public bool IsVisible { get; set; } = true;
    public bool IsDraggable { get; set; } = true;
    public Action? OnClosed { get; set; }
}

/// <summary>
/// Descriptor for contributing custom buttons, pills, or widgets into the AI Composer interface.
/// </summary>
public class ComposerActionDescriptor
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Tooltip { get; init; }
    public string IconKind { get; init; } = "LightningBoltOutline";
    public Action? OnClick { get; init; }
    public Func<object>? CustomContentFactory { get; init; }
    public int Order { get; init; } = 50;
    public bool IsVisible { get; set; } = true;
}
