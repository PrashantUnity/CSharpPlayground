using System.Collections.Generic;
using System.Threading;
using System.Windows.Input;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Material.Icons;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Studio;

/// <summary>
/// The branded card over the studio. It is shown only for blocking work (cold start, switching workspace) and only
/// after <c>ActivityTiming.BlockingShowAfter</c>; everything else shows as a progress line. Its rings and beams are
/// clock-driven animations that run only while the card is on screen.
/// </summary>
public partial class StudioLoadingOverlayControl : UserControl
{
    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<StudioLoadingOverlayControl, bool>(nameof(IsLoading), defaultValue: false);

    public static readonly StyledProperty<string> LoadingTitleProperty =
        AvaloniaProperty.Register<StudioLoadingOverlayControl, string>(nameof(LoadingTitle), defaultValue: "Loading...");

    public static readonly StyledProperty<string> LoadingSubtitleProperty =
        AvaloniaProperty.Register<StudioLoadingOverlayControl, string>(nameof(LoadingSubtitle), defaultValue: string.Empty);

    public static readonly StyledProperty<bool> ShowLogoProperty =
        AvaloniaProperty.Register<StudioLoadingOverlayControl, bool>(nameof(ShowLogo), defaultValue: true);

    public static readonly StyledProperty<MaterialIconKind> IconKindProperty =
        AvaloniaProperty.Register<StudioLoadingOverlayControl, MaterialIconKind>(nameof(IconKind), defaultValue: MaterialIconKind.ProgressClock);

    public static readonly StyledProperty<ICommand?> CancelCommandProperty =
        AvaloniaProperty.Register<StudioLoadingOverlayControl, ICommand?>(nameof(CancelCommand));

    public static readonly StyledProperty<bool> ShowCancelProperty =
        AvaloniaProperty.Register<StudioLoadingOverlayControl, bool>(nameof(ShowCancel));

    private Border? _spinnerBorder;
    private Border? _outerBorder;
    private Border? _auraBorder;
    private Border? _primaryBeamBorder;
    private Border? _secondaryBeamBorder;
    private CancellationTokenSource? _animations;

    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public string LoadingTitle
    {
        get => GetValue(LoadingTitleProperty);
        set => SetValue(LoadingTitleProperty, value);
    }

    public string LoadingSubtitle
    {
        get => GetValue(LoadingSubtitleProperty);
        set => SetValue(LoadingSubtitleProperty, value);
    }

    public bool ShowLogo
    {
        get => GetValue(ShowLogoProperty);
        set => SetValue(ShowLogoProperty, value);
    }

    public MaterialIconKind IconKind
    {
        get => GetValue(IconKindProperty);
        set => SetValue(IconKindProperty, value);
    }

    /// <summary>Cancels the blocking work (shown as a Cancel button when <see cref="ShowCancel"/>).</summary>
    public ICommand? CancelCommand
    {
        get => GetValue(CancelCommandProperty);
        set => SetValue(CancelCommandProperty, value);
    }

    public bool ShowCancel
    {
        get => GetValue(ShowCancelProperty);
        set => SetValue(ShowCancelProperty, value);
    }

    /// <summary>Whether the card's animations are running (they must not while it is hidden).</summary>
    public bool IsAnimationRunning => _animations != null;

    public StudioLoadingOverlayControl()
    {
        InitializeComponent();
        TryLoadLogo();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsLoadingProperty)
        {
            PseudoClasses.Set(":loading", IsLoading);
            UpdateAnimations();
        }
        else if (change.Property.Name == "IsEffectivelyVisible")
        {
            UpdateAnimations();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateAnimations();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopAnimations();
    }

    private void FindTransformReferences()
    {
        _spinnerBorder ??= this.FindControl<Border>("SpinnerRingBorder");
        _outerBorder ??= this.FindControl<Border>("OuterRingBorder");
        _auraBorder ??= this.FindControl<Border>("AuraPulseBorder");
        _primaryBeamBorder ??= this.FindControl<Border>("PrimaryBeamBorder");
        _secondaryBeamBorder ??= this.FindControl<Border>("SecondaryBeamBorder");
    }

    private void UpdateAnimations()
    {
        if (!(IsLoading && IsEffectivelyVisible && TopLevel.GetTopLevel(this) != null))
        {
            StopAnimations();
            return;
        }

        if (_animations != null) return;
        FindTransformReferences();
        _animations = new CancellationTokenSource();
        var token = _animations.Token;
        foreach (var (animation, target) in BuildAnimations())
        {
            _ = animation.RunAsync(target, token);
        }
    }

    private void StopAnimations()
    {
        if (_animations == null) return;
        _animations.Cancel();
        _animations.Dispose();
        _animations = null;
        StepAnimation(TimeSpan.Zero);
    }

    private static Animation Loop(double seconds, AvaloniaProperty property, params (double Cue, object Value)[] frames)
    {
        var animation = new Animation { Duration = TimeSpan.FromSeconds(seconds), IterationCount = IterationCount.Infinite };
        foreach (var (cue, value) in frames)
        {
            animation.Children.Add(new KeyFrame { Cue = new Cue(cue), Setters = { new Setter(property, value) } });
        }

        return animation;
    }

    private IEnumerable<(Animation Animation, Animatable Target)> BuildAnimations()
    {
        // Transform animations run on the control and drive its RenderTransform.
        // 1. Primary orbital spinner: a full turn every 1.2 s.
        if (_spinnerBorder?.RenderTransform is RotateTransform)
            yield return (Loop(1.2, RotateTransform.AngleProperty, (0, 0d), (1, 360d)), _spinnerBorder);

        // 2. Counter-orbiting outer ring: a reverse turn every 2 s.
        if (_outerBorder?.RenderTransform is RotateTransform)
            yield return (Loop(2.0, RotateTransform.AngleProperty, (0, 360d), (1, 0d)), _outerBorder);

        // 3. Breathing aura: 0.25 to 0.75 opacity and back every 2 s.
        if (_auraBorder != null)
            yield return (Loop(2.0, OpacityProperty, (0, 0.5), (0.25, 0.75), (0.75, 0.25), (1, 0.5)), _auraBorder);

        // 4. Primary beam across the track every 1.4 s.
        if (_primaryBeamBorder?.RenderTransform is TranslateTransform)
            yield return (Loop(1.4, TranslateTransform.XProperty, (0, -110d), (1, 260d)), _primaryBeamBorder);

        // 5. Secondary trailing beam, a quarter turn behind, fading in and out.
        if (_secondaryBeamBorder?.RenderTransform is TranslateTransform)
        {
            var trail = Loop(1.4, TranslateTransform.XProperty, (0, -55d), (1, 260d));
            trail.Delay = TimeSpan.FromSeconds(0.35);
            yield return (trail, _secondaryBeamBorder);
            var fade = Loop(1.4, OpacityProperty, (0, 0d), (0.5, 0.85), (1, 0d));
            fade.Delay = TimeSpan.FromSeconds(0.35);
            yield return (fade, _secondaryBeamBorder);
        }
    }

    /// <summary>
    /// Poses the card as it looks <paramref name="elapsed"/> into its animation, without running it (snapshot tools and
    /// tests; a headless renderer has no animation clock).
    /// </summary>
    public void StepAnimation(TimeSpan elapsed)
    {
        FindTransformReferences();
        double seconds = elapsed.TotalSeconds;

        if (_spinnerBorder != null)
        {
            if (_spinnerBorder.RenderTransform is not RotateTransform spin) _spinnerBorder.RenderTransform = spin = new RotateTransform();
            spin.Angle = (seconds / 1.2 * 360.0) % 360.0;
        }

        if (_outerBorder != null)
        {
            if (_outerBorder.RenderTransform is not RotateTransform outer) _outerBorder.RenderTransform = outer = new RotateTransform();
            outer.Angle = (360.0 - ((seconds / 2.0 * 360.0) % 360.0)) % 360.0;
        }

        if (_auraBorder != null)
        {
            var wave = (Math.Sin(seconds * Math.PI) + 1.0) / 2.0;
            _auraBorder.Opacity = 0.25 + 0.5 * wave;
        }

        if (_primaryBeamBorder != null)
        {
            if (_primaryBeamBorder.RenderTransform is not TranslateTransform prim) _primaryBeamBorder.RenderTransform = prim = new TranslateTransform();
            prim.X = -110.0 + (seconds / 1.4 % 1.0) * 370.0;
        }

        if (_secondaryBeamBorder != null)
        {
            if (_secondaryBeamBorder.RenderTransform is not TranslateTransform sec) _secondaryBeamBorder.RenderTransform = sec = new TranslateTransform();
            var t2 = ((seconds + 0.35) / 1.4) % 1.0;
            sec.X = -55.0 + t2 * 315.0;
            _secondaryBeamBorder.Opacity = Math.Clamp(Math.Sin(t2 * Math.PI) * 0.85, 0.0, 0.85);
        }
    }

    private void TryLoadLogo()
    {
        try
        {
            var uri = new Uri("avares://CSharpEditorPlugin/Assets/app-logo.png");
            if (AssetLoader.Exists(uri))
            {
                using var stream = AssetLoader.Open(uri);
                var logoImage = this.FindControl<Image>("LogoImage");
                if (logoImage != null)
                {
                    // The badge is 48 px: decode the logo at that size, not at its full resolution.
                    logoImage.Source = Bitmap.DecodeToWidth(stream, 96);
                }
            }
        }
        catch
        {
            // Fall back gracefully in headless unit tests or when AssetLoader is uninitialized
        }
    }
}
