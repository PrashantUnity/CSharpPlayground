using System;
using System.ComponentModel;
using System.Threading;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using PdfEditorApp.Plugins.CSharpEditor.Services.Activities;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Common;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Activities;

/// <summary>
/// The thin progress line of one zone (VS Code's "progress bar under the tabs"). It shows only when the zone's work has
/// run past <see cref="ActivityTiming.ShowAfter"/>: a sliding segment while the amount is unknown, a filling bar when the
/// work reports a fraction. Place it over the zone's top or bottom edge (it takes no space of its own when hidden). The
/// slide runs only while the line is on screen.
/// </summary>
public sealed class ActivityProgressLine : Panel
{
    public static readonly StyledProperty<ActivityLocation> LocationProperty =
        AvaloniaProperty.Register<ActivityProgressLine, ActivityLocation>(nameof(Location), ActivityLocation.Window);

    private const double SegmentShare = 0.3;
    private readonly Border _fill;
    private readonly Border _segment;
    private readonly TranslateTransform _slide = new();
    private ActivityLineViewModel? _line;
    private CancellationTokenSource? _animation;
    private double _animatedWidth = -1;

    public ActivityProgressLine()
    {
        Height = 2;
        ClipToBounds = true;
        IsHitTestVisible = false;
        IsVisible = false;
        VerticalAlignment = VerticalAlignment.Top;

        // Stretch: ArrangeOverride gives each its size; a left-aligned border would shrink to its (empty) desired width.
        _fill = new Border { HorizontalAlignment = HorizontalAlignment.Stretch };
        _segment = new Border { HorizontalAlignment = HorizontalAlignment.Stretch, RenderTransform = _slide };
        _fill.Bind(Border.BackgroundProperty, _fill.GetResourceObservable("DsPrimaryBrush"));
        _segment.Bind(Border.BackgroundProperty, _segment.GetResourceObservable("DsPrimaryBrush"));
        Children.Add(_fill);
        Children.Add(_segment);
    }

    public ActivityLocation Location
    {
        get => GetValue(LocationProperty);
        set => SetValue(LocationProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ActivityScope.PresenterProperty || change.Property == LocationProperty)
        {
            Attach(ActivityScope.GetPresenter(this)?.LineFor(Location));
        }
        else if (change.Property == BoundsProperty || change.Property.Name == "IsEffectivelyVisible")
        {
            UpdateAnimation();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Attach(ActivityScope.GetPresenter(this)?.LineFor(Location));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Attach(null);
    }

    private void Attach(ActivityLineViewModel? line)
    {
        if (ReferenceEquals(line, _line)) return;
        if (_line != null) _line.PropertyChanged -= OnLineChanged;
        _line = line;
        if (_line != null) _line.PropertyChanged += OnLineChanged;
        Apply();
    }

    private void OnLineChanged(object? sender, PropertyChangedEventArgs e) => Apply();

    private void Apply()
    {
        var line = _line;
        IsVisible = line is { IsVisible: true };
        bool indeterminate = line?.IsIndeterminate ?? true;
        _segment.IsVisible = indeterminate;
        _fill.IsVisible = !indeterminate;
        InvalidateArrange();
        UpdateAnimation();
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double value = _line?.Value ?? 0;
        _fill.Arrange(new Rect(0, 0, finalSize.Width * Math.Clamp(value / 100, 0, 1), finalSize.Height));
        _segment.Arrange(new Rect(0, 0, finalSize.Width * SegmentShare, finalSize.Height));
        return finalSize;
    }

    /// <summary>Whether the slide animation is running (tests check that a hidden line costs nothing).</summary>
    public bool IsAnimationRunning => _animation != null;

    private void UpdateAnimation()
    {
        double width = Bounds.Width;
        // A control without a window still calls itself "effectively visible": require a real top level too.
        bool wanted = IsEffectivelyVisible && _segment.IsVisible && width > 0 && TopLevel.GetTopLevel(this) != null;
        if (!wanted)
        {
            StopAnimation();
            return;
        }

        if (_animation != null && Math.Abs(width - _animatedWidth) < 0.5) return;
        StopAnimation();
        _animatedWidth = width;
        double segment = width * SegmentShare;
        // A still frame (a snapshot, a paused clock) shows the segment on screen rather than parked off the left edge.
        _slide.X = width * 0.2;
        var animation = new Animation
        {
            Duration = TimeSpan.FromSeconds(1.4),
            IterationCount = IterationCount.Infinite,
            Easing = new Avalonia.Animation.Easings.QuadraticEaseInOut(),
            Children =
            {
                new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(TranslateTransform.XProperty, -segment) } },
                new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(TranslateTransform.XProperty, width) } },
            },
        };
        _animation = new CancellationTokenSource();
        // Transform animations run on the control and drive its RenderTransform.
        _ = animation.RunAsync(_segment, _animation.Token);
    }

    private void StopAnimation()
    {
        if (_animation == null) return;
        _animation.Cancel();
        _animation.Dispose();
        _animation = null;
        _animatedWidth = -1;
    }
}
