using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Threading;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Dialogs;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.UI;

/// <summary>
/// Service managing UI contribution points (Activity Bar, Status Bar, Bottom Deck), modals, and notifications.
/// </summary>
public class ExtensibilityUiService : IUiApi
{
    private readonly ConcurrentDictionary<string, ActivityBarDescriptor> _activityBarItems = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SideBarViewDescriptor> _sideBarViews = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, StatusBarWidgetDescriptor> _statusBarWidgets = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, BottomDeckTabDescriptor> _bottomDeckTabs = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, EditorToolbarItemDescriptor> _editorToolbarItems = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, FloatingOverlayDescriptor> _floatingOverlays = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ComposerActionDescriptor> _composerActions = new(StringComparer.OrdinalIgnoreCase);

    public ExtensibilityDialogService DialogsService { get; } = new();
    public IDialogApi Dialogs => DialogsService;

    public Func<object?>? ActiveTopLevelResolver { get; set; }
    public Func<object?>? MainWindowResolver { get; set; }
    public object? ActiveTopLevel => ActiveTopLevelResolver?.Invoke();
    public object? MainWindow => MainWindowResolver?.Invoke();

    public IVisualTreeApi VisualTree { get; }

    public ExtensibilityUiService()
    {
        VisualTree = new VisualTreeManager(() => ActiveTopLevel ?? MainWindow);
    }

    public event Action<NotificationMessage>? NotificationPosted;
    public event Action? ContributionsChanged;

    public IReadOnlyList<ActivityBarDescriptor> ActivityBarItems => _activityBarItems.Values.OrderBy(x => x.Order).ToList();
    public IReadOnlyList<SideBarViewDescriptor> SideBarViews => _sideBarViews.Values.OrderBy(x => x.Order).ToList();
    public IReadOnlyList<StatusBarWidgetDescriptor> StatusBarWidgets => _statusBarWidgets.Values.OrderByDescending(x => x.Priority).ToList();
    public IReadOnlyList<BottomDeckTabDescriptor> BottomDeckTabs => _bottomDeckTabs.Values.ToList();
    public IReadOnlyList<EditorToolbarItemDescriptor> EditorToolbarItems => _editorToolbarItems.Values.OrderBy(x => x.Order).ToList();
    public IReadOnlyList<FloatingOverlayDescriptor> FloatingOverlays => _floatingOverlays.Values.ToList();
    public IReadOnlyList<ComposerActionDescriptor> ComposerActions => _composerActions.Values.OrderBy(x => x.Order).ToList();

    public IDisposable RegisterActivityBarItem(ActivityBarDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.Id);

        _activityBarItems[descriptor.Id] = descriptor;
        NotifyContributionsChanged();

        return new ActionDisposable(() =>
        {
            _activityBarItems.TryRemove(descriptor.Id, out _);
            NotifyContributionsChanged();
        });
    }

    public IDisposable RegisterSideBarView(SideBarViewDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.Id);

        _sideBarViews[descriptor.Id] = descriptor;
        NotifyContributionsChanged();

        return new ActionDisposable(() =>
        {
            _sideBarViews.TryRemove(descriptor.Id, out _);
            NotifyContributionsChanged();
        });
    }

    public IDisposable RegisterStatusBarWidget(StatusBarWidgetDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.Id);

        _statusBarWidgets[descriptor.Id] = descriptor;
        NotifyContributionsChanged();

        return new ActionDisposable(() =>
        {
            _statusBarWidgets.TryRemove(descriptor.Id, out _);
            NotifyContributionsChanged();
        });
    }

    public IDisposable RegisterBottomDeckTab(BottomDeckTabDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.Id);

        _bottomDeckTabs[descriptor.Id] = descriptor;
        NotifyContributionsChanged();

        return new ActionDisposable(() =>
        {
            _bottomDeckTabs.TryRemove(descriptor.Id, out _);
            NotifyContributionsChanged();
        });
    }

    public IDisposable RegisterEditorToolbarItem(EditorToolbarItemDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.Id);

        _editorToolbarItems[descriptor.Id] = descriptor;
        NotifyContributionsChanged();

        return new ActionDisposable(() =>
        {
            _editorToolbarItems.TryRemove(descriptor.Id, out _);
            NotifyContributionsChanged();
        });
    }

    public IDisposable RegisterFloatingOverlay(FloatingOverlayDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.Id);

        _floatingOverlays[descriptor.Id] = descriptor;
        NotifyContributionsChanged();

        return new ActionDisposable(() =>
        {
            if (_floatingOverlays.TryRemove(descriptor.Id, out var removed))
            {
                removed.OnClosed?.Invoke();
            }
            NotifyContributionsChanged();
        });
    }

    public IDisposable RegisterComposerAction(ComposerActionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.Id);

        _composerActions[descriptor.Id] = descriptor;
        NotifyContributionsChanged();

        return new ActionDisposable(() =>
        {
            _composerActions.TryRemove(descriptor.Id, out _);
            NotifyContributionsChanged();
        });
    }

    public object? CreateComponent(string xaml, string? title = null)
    {
        return DynamicViewFactory.CreateFromXaml(xaml, title);
    }

    public void ShowNotification(NotificationMessage notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        Dispatcher.UIThread.Post(() =>
        {
            NotificationPosted?.Invoke(notification);
        });
    }

    public void ShowInfo(string message, string title = "Customization") =>
        ShowNotification(new NotificationMessage { Message = message, Title = title, Severity = NotificationSeverity.Info });

    public void ShowSuccess(string message, string title = "Customization") =>
        ShowNotification(new NotificationMessage { Message = message, Title = title, Severity = NotificationSeverity.Success });

    public void ShowWarning(string message, string title = "Customization") =>
        ShowNotification(new NotificationMessage { Message = message, Title = title, Severity = NotificationSeverity.Warning });

    public void ShowError(string message, string title = "Customization") =>
        ShowNotification(new NotificationMessage { Message = message, Title = title, Severity = NotificationSeverity.Error });

    private void NotifyContributionsChanged()
    {
        Dispatcher.UIThread.Post(() =>
        {
            ContributionsChanged?.Invoke();
        });
    }

    private sealed class ActionDisposable : IDisposable
    {
        private Action? _action;
        public ActionDisposable(Action action) => _action = action;
        public void Dispose()
        {
            var act = System.Threading.Interlocked.Exchange(ref _action, null);
            act?.Invoke();
        }
    }
}
