using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Services.Activities;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.Common;

/// <summary>One zone's progress line, as the screen binds it.</summary>
public sealed partial class ActivityLineViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private bool _isIndeterminate = true;

    /// <summary>0..100 when <see cref="IsIndeterminate"/> is false.</summary>
    [ObservableProperty]
    private double _value;

    internal void Apply(ActivityLine line)
    {
        IsVisible = line.IsVisible;
        IsIndeterminate = line.Fraction is null;
        Value = (line.Fraction ?? 0) * 100;
    }
}

/// <summary>One toast: long work with Cancel, or an error.</summary>
public sealed partial class ActivityToastViewModel : ObservableObject
{
    public ActivityToastViewModel(long id, IRelayCommand<long> cancel, IRelayCommand<long> dismiss)
    {
        Id = id;
        CancelCommand = new RelayCommand(() => cancel.Execute(Id));
        DismissCommand = new RelayCommand(() => dismiss.Execute(Id));
    }

    public long Id { get; }

    public IRelayCommand CancelCommand { get; }

    public IRelayCommand DismissCommand { get; }

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetail))]
    private string? _detail;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProgress))]
    private double? _fraction;

    [ObservableProperty]
    private bool _isCancellable;

    [ObservableProperty]
    private bool _isError;

    public bool HasDetail => !string.IsNullOrEmpty(Detail);

    public bool HasProgress => Fraction.HasValue;

    public double ProgressValue => (Fraction ?? 0) * 100;

    partial void OnFractionChanged(double? value) => OnPropertyChanged(nameof(ProgressValue));

    internal void Apply(ActivityToast toast)
    {
        Title = toast.Title;
        Detail = toast.Detail;
        Fraction = toast.Fraction;
        IsCancellable = toast.Cancellable;
        IsError = toast.IsError;
    }
}

/// <summary>
/// Shows the studio's running work (<see cref="IActivityService"/>) on screen, following <see cref="ActivityTiming"/>:
/// a line per zone, a status-bar entry, toasts, and the branded card for blocking work. Changes arrive from any thread;
/// they are coalesced into at most one update per UI turn, and a timer runs only while there is work to time.
/// </summary>
public sealed partial class ActivityPresenterViewModel : ObservableObject, IDisposable
{
    private readonly IActivityService _service;
    private readonly ActivityPresentationTracker _tracker;
    private readonly Action<Action> _post;
    private readonly object _refreshGate = new();
    private ITimer? _timer;
    private int _refreshQueued;
    private bool _disposed;

    /// <param name="post">Runs an update on the UI thread; inline when not given and there is no running Avalonia app (tests).</param>
    public ActivityPresenterViewModel(IActivityService service, Action<Action>? post = null)
    {
        _service = service;
        _tracker = new ActivityPresentationTracker(service.Time);
        _post = post ?? PostToUiThread;
        CancelCommand = new RelayCommand<long>(id => _service.Cancel(id));
        DismissCommand = new RelayCommand<long>(id =>
        {
            lock (_refreshGate) _tracker.Dismiss(id);
            QueueRefresh();
        });

        Toasts.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasToasts));
        _service.Changed += QueueRefresh;
        _service.Failed += OnFailed;
        QueueRefresh();
    }

    public IActivityService Service => _service;

    public ActivityLineViewModel Window { get; } = new();
    public ActivityLineViewModel Editor { get; } = new();
    public ActivityLineViewModel Notebook { get; } = new();
    public ActivityLineViewModel Explorer { get; } = new();
    public ActivityLineViewModel Hub { get; } = new();

    public ActivityLineViewModel LineFor(ActivityLocation location) => location switch
    {
        ActivityLocation.Window => Window,
        ActivityLocation.Editor => Editor,
        ActivityLocation.Notebook => Notebook,
        ActivityLocation.Explorer => Explorer,
        ActivityLocation.Hub => Hub,
        _ => Window,
    };

    public ObservableCollection<ActivityToastViewModel> Toasts { get; } = new();

    public bool HasToasts => Toasts.Count > 0;

    public IRelayCommand<long> CancelCommand { get; }

    public IRelayCommand<long> DismissCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string? _statusText;

    [ObservableProperty]
    private string? _statusDetail;

    public bool HasStatus => !string.IsNullOrEmpty(StatusText);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCard))]
    private ActivityCard? _card;

    public bool HasCard => Card != null;

    [RelayCommand]
    private void CancelCard()
    {
        if (Card is { } card) _service.Cancel(card.Id);
    }

    private void OnFailed(ActivitySnapshot snapshot, Exception error)
    {
        lock (_refreshGate) _tracker.AddError(snapshot, error);
        QueueRefresh();
    }

    private void QueueRefresh()
    {
        if (_disposed || Interlocked.Exchange(ref _refreshQueued, 1) == 1) return;
        _post(() =>
        {
            Interlocked.Exchange(ref _refreshQueued, 0);
            Refresh();
        });
    }

    /// <summary>Re-evaluates what to show now (tests call it after moving their clock).</summary>
    public void Refresh()
    {
        // In the studio every refresh runs on the UI thread; serialising here keeps a test (or a host without a UI
        // thread) from applying two presentations at once.
        lock (_refreshGate)
        {
            if (_disposed) return;
            var presentation = _tracker.Compute(_service.Snapshot());
            foreach (var location in Zones)
            {
                LineFor(location).Apply(presentation.LineFor(location));
            }

            StatusText = presentation.StatusText;
            StatusDetail = presentation.StatusDetail;
            Card = presentation.Card;
            SyncToasts(presentation);
            UpdateTimer(presentation.NeedsTick);
        }
    }

    private static readonly ActivityLocation[] Zones =
        [ActivityLocation.Window, ActivityLocation.Editor, ActivityLocation.Notebook, ActivityLocation.Explorer, ActivityLocation.Hub];

    private void SyncToasts(ActivityPresentation presentation)
    {
        var wanted = presentation.Toasts;
        for (int i = Toasts.Count - 1; i >= 0; i--)
        {
            if (!wanted.Any(t => t.Id == Toasts[i].Id && t.IsError == Toasts[i].IsError)) Toasts.RemoveAt(i);
        }

        foreach (var toast in wanted)
        {
            var existing = Toasts.FirstOrDefault(t => t.Id == toast.Id && t.IsError == toast.IsError);
            if (existing == null)
            {
                existing = new ActivityToastViewModel(toast.Id, CancelCommand, DismissCommand);
                Toasts.Add(existing);
            }

            existing.Apply(toast);
        }
    }

    private void UpdateTimer(bool needed)
    {
        lock (_refreshGate)
        {
            if (needed && _timer == null && !_disposed)
            {
                _timer = _service.Time.CreateTimer(_ => QueueRefresh(), null, ActivityTiming.PresenterTick, ActivityTiming.PresenterTick);
            }
            else if (!needed && _timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }
        }
    }

    /// <summary>Whether the presenter's timer is running (it must stop when nothing is pending; tests check it).</summary>
    public bool IsTicking
    {
        get
        {
            lock (_refreshGate) return _timer != null;
        }
    }

    private static void PostToUiThread(Action action)
    {
        if (Avalonia.Application.Current == null || Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action, DispatcherPriority.Background);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _service.Changed -= QueueRefresh;
        _service.Failed -= OnFailed;
        lock (_refreshGate)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }
}
