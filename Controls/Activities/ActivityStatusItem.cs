using System;
using System.ComponentModel;
using System.Threading;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Common;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Activities;

/// <summary>
/// The status-bar entry for running work: a turning ring and the title of the oldest work on screen ("+2" when there is
/// more), with its detail as the tooltip. Hidden while nothing is running past its show delay. It turns only while on
/// screen.
/// </summary>
public sealed class ActivityStatusItem : StackPanel
{
    private readonly Border _ring;
    private readonly TextBlock _text;
    private readonly RotateTransform _turn = new();
    private ActivityPresenterViewModel? _presenter;
    private CancellationTokenSource? _animation;

    public ActivityStatusItem()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 4;
        VerticalAlignment = VerticalAlignment.Center;
        IsVisible = false;

        // A ring open on two sides, like the card's spinner: plain border drawing, nothing to load or parse.
        _ring = new Border
        {
            Width = 10,
            Height = 10,
            CornerRadius = new CornerRadius(PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Layout.LayoutTokens.Radius("SM") + 1),
            BorderThickness = new Thickness(PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Layout.LayoutTokens.BorderWidth * 1.5, PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Layout.LayoutTokens.BorderWidth * 1.5, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = RelativePoint.Center,
            RenderTransform = _turn,
        };
        _ring.Bind(Border.BorderBrushProperty, _ring.GetResourceObservable("DsPrimaryBrush"));

        _text = new TextBlock
        {
            FontSize = PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Layout.LayoutTokens.FontSize("100"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 280,
        };
        _text.Bind(TextBlock.ForegroundProperty, _text.GetResourceObservable("DsMutedBrush"));

        Children.Add(_ring);
        Children.Add(_text);
    }

    /// <summary>Whether the ring is turning (tests check that a hidden entry costs nothing).</summary>
    public bool IsAnimationRunning => _animation != null;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ActivityScope.PresenterProperty)
        {
            Attach(ActivityScope.GetPresenter(this));
        }
        else if (change.Property.Name == "IsEffectivelyVisible")
        {
            UpdateAnimation();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Attach(ActivityScope.GetPresenter(this));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Attach(null);
    }

    private void Attach(ActivityPresenterViewModel? presenter)
    {
        if (ReferenceEquals(presenter, _presenter)) return;
        if (_presenter != null) _presenter.PropertyChanged -= OnPresenterChanged;
        _presenter = presenter;
        if (_presenter != null) _presenter.PropertyChanged += OnPresenterChanged;
        Apply();
    }

    private void OnPresenterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ActivityPresenterViewModel.StatusText) or nameof(ActivityPresenterViewModel.StatusDetail)) Apply();
    }

    private void Apply()
    {
        _text.Text = _presenter?.StatusText;
        ToolTip.SetTip(this, string.IsNullOrEmpty(_presenter?.StatusDetail) ? _presenter?.StatusText : $"{_presenter?.StatusText}\n{_presenter?.StatusDetail}");
        IsVisible = _presenter?.HasStatus == true;
        UpdateAnimation();
    }

    private void UpdateAnimation()
    {
        // A control without a window still calls itself "effectively visible": require a real top level too.
        if (!IsEffectivelyVisible || TopLevel.GetTopLevel(this) == null)
        {
            if (_animation == null) return;
            _animation.Cancel();
            _animation.Dispose();
            _animation = null;
            return;
        }

        if (_animation != null) return;
        var animation = new Animation
        {
            Duration = TimeSpan.FromSeconds(1),
            IterationCount = IterationCount.Infinite,
            Children =
            {
                new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(RotateTransform.AngleProperty, 0d) } },
                new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(RotateTransform.AngleProperty, 360d) } },
            },
        };
        _animation = new CancellationTokenSource();
        // Transform animations run on the control and drive its RenderTransform.
        _ = animation.RunAsync(_ring, _animation.Token);
    }
}
