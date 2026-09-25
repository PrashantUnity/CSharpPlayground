using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Material.Icons.Avalonia;

namespace PdfEditorApp.Plugins.CSharpEditor.Tools.UiSnapshots;

/// <summary>
/// The app's look, set up the way Runner/App.axaml does it: the Material 3 tokens (every <c>M3…Brush</c> the views use),
/// Fluent, Material icons, AvaloniaEdit's styles and the Material 3 control styles. Without these the views still lay
/// out, but their DynamicResource brushes resolve to nothing and the snapshot doesn't look like the app.
/// </summary>
internal sealed class SnapshotApp : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        var baseUri = new Uri("avares://UiSnapshots/");
        Resources.MergedDictionaries.Add(new ResourceInclude(baseUri) { Source = new Uri("avares://FrySharp/Material3ExpressiveTokens.axaml") });
        Styles.Add(new FluentTheme());
        Styles.Add(new MaterialIconStyles(null));
        Styles.Add(new StyleInclude(baseUri) { Source = new Uri("avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml") });
        Styles.Add(new StyleInclude(baseUri) { Source = new Uri("avares://FrySharp/Material3ExpressiveStyles.axaml") });
    }
}
