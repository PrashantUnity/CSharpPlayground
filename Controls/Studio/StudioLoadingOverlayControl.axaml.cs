using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
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

    public StudioLoadingOverlayControl()
    {
        InitializeComponent();
        TryLoadLogo();
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
