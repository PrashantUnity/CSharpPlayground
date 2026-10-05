using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Material.Icons;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Studio;

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

    private readonly DispatcherTimer _animationTimer;
    private readonly Stopwatch _stopwatch = new();

    private Border? _spinnerBorder;
    private Border? _outerBorder;
    private Border? _auraBorder;
    private Border? _primaryBeamBorder;
    private Border? _secondaryBeamBorder;

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

    static StudioLoadingOverlayControl()
    {
        IsLoadingProperty.Changed.AddClassHandler<StudioLoadingOverlayControl>((control, e) =>
        {
            control.OnIsLoadingChanged(e.GetNewValue<bool>());
        });
    }

    public StudioLoadingOverlayControl()
    {
        InitializeComponent();
        TryLoadLogo();

        _animationTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Normal, OnAnimationTick);

        AttachedToVisualTree += (s, e) =>
        {
            FindTransformReferences();
            if (IsLoading)
            {
                StartAnimation();
            }
        };

        DetachedFromVisualTree += (s, e) =>
        {
            StopAnimation();
        };
    }

    private void OnIsLoadingChanged(bool isLoading)
    {
        PseudoClasses.Set(":loading", isLoading);
        if (isLoading)
        {
            FindTransformReferences();
            StartAnimation();
        }
        else
        {
            StopAnimation();
        }
    }

    private void FindTransformReferences()
    {
        _spinnerBorder ??= this.FindControl<Border>("SpinnerRingBorder");
        _outerBorder ??= this.FindControl<Border>("OuterRingBorder");
        _auraBorder ??= this.FindControl<Border>("AuraPulseBorder");
        _primaryBeamBorder ??= this.FindControl<Border>("PrimaryBeamBorder");
        _secondaryBeamBorder ??= this.FindControl<Border>("SecondaryBeamBorder");
    }

    private void StartAnimation()
    {
        _stopwatch.Restart();
        UpdateAnimation(0.0);
        if (!_animationTimer.IsEnabled)
        {
            _animationTimer.Start();
        }
    }

    private void StopAnimation()
    {
        if (_animationTimer.IsEnabled)
        {
            _animationTimer.Stop();
        }
        _stopwatch.Reset();
        UpdateAnimation(0.0);
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        if (!IsLoading)
        {
            StopAnimation();
            return;
        }

        UpdateAnimation(_stopwatch.Elapsed.TotalSeconds);
    }

    /// <summary>
    /// Advances the animation to a specific elapsed time, used by tests and snapshot drivers.
    /// </summary>
    public void StepAnimation(TimeSpan elapsed)
    {
        FindTransformReferences();
        UpdateAnimation(elapsed.TotalSeconds);
    }

    private void UpdateAnimation(double seconds)
    {
        FindTransformReferences();

        // 1. Primary orbital spinner: 360 deg every 1.2s
        if (_spinnerBorder != null)
        {
            if (_spinnerBorder.RenderTransform is not RotateTransform spin)
            {
                _spinnerBorder.RenderTransform = spin = new RotateTransform();
            }
            spin.Angle = (seconds / 1.2 * 360.0) % 360.0;
        }

        // 2. Counter-orbiting outer whisper ring: reverse 360 deg every 2.0s
        if (_outerBorder != null)
        {
            if (_outerBorder.RenderTransform is not RotateTransform outer)
            {
                _outerBorder.RenderTransform = outer = new RotateTransform();
            }
            outer.Angle = (360.0 - ((seconds / 2.0 * 360.0) % 360.0)) % 360.0;
        }

        // 3. Ambient breathing aura pulse: 0.25 to 0.75 opacity
        if (_auraBorder != null)
        {
            var wave = (Math.Sin(seconds * Math.PI) + 1.0) / 2.0;
            _auraBorder.Opacity = 0.25 + 0.5 * wave;
        }

        // 4. Primary traveling beam: -110 to 260 across 1.4s
        if (_primaryBeamBorder != null)
        {
            if (_primaryBeamBorder.RenderTransform is not TranslateTransform prim)
            {
                _primaryBeamBorder.RenderTransform = prim = new TranslateTransform();
            }
            var t1 = (seconds / 1.4) % 1.0;
            prim.X = -110.0 + t1 * 370.0;
        }

        // 5. Secondary trailing beam: staggered by 0.35s
        if (_secondaryBeamBorder != null)
        {
            if (_secondaryBeamBorder.RenderTransform is not TranslateTransform sec)
            {
                _secondaryBeamBorder.RenderTransform = sec = new TranslateTransform();
            }
            var t2 = ((seconds + 0.35) / 1.4) % 1.0;
            sec.X = -55.0 + t2 * 315.0;
            var opacity = Math.Sin(t2 * Math.PI);
            _secondaryBeamBorder.Opacity = Math.Clamp(opacity * 0.85, 0.0, 0.85);
        }

        _spinnerBorder?.InvalidateVisual();
        _outerBorder?.InvalidateVisual();
        _auraBorder?.InvalidateVisual();
        _primaryBeamBorder?.InvalidateVisual();
        _secondaryBeamBorder?.InvalidateVisual();
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
                    logoImage.Source = new Bitmap(stream);
                }
            }
        }
        catch
        {
            // Fall back gracefully in headless unit tests or when AssetLoader is uninitialized
        }
    }
}
