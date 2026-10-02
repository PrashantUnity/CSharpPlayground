using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Threading;
using FrySharp.Sdk;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.UI;

/// <summary>
/// Service managing UI contribution points (Activity Bar, Status Bar, Bottom Deck) and toast notifications.
/// </summary>
public class ExtensibilityUiService : IUiApi
{
    private readonly ConcurrentDictionary<string, ActivityBarDescriptor> _activityBarItems = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, StatusBarWidgetDescriptor> _statusBarWidgets = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, BottomDeckTabDescriptor> _bottomDeckTabs = new(StringComparer.OrdinalIgnoreCase);

    public event Action<NotificationMessage>? NotificationPosted;
    public event Action? ContributionsChanged;

    public IReadOnlyList<ActivityBarDescriptor> ActivityBarItems => _activityBarItems.Values.OrderBy(x => x.Order).ToList();
    public IReadOnlyList<StatusBarWidgetDescriptor> StatusBarWidgets => _statusBarWidgets.Values.OrderByDescending(x => x.Priority).ToList();
    public IReadOnlyList<BottomDeckTabDescriptor> BottomDeckTabs => _bottomDeckTabs.Values.ToList();

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
