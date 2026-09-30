using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PdfEditorApp.Plugins.CSharpEditor.Controls;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;

namespace PdfEditorApp.Plugins.CSharpEditor.Views;

public partial class CSharpStudioHostView : UserControl, IDisposable
{
    private readonly KeepAlivePageHost _pages;

    public CSharpStudioHostView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnHostKeyDown, RoutingStrategies.Tunnel);

        _pages = this.FindControl<KeepAlivePageHost>("PageHost")!;
        _pages.Register<CSharpManagerViewModel>(() => new CSharpManagerView());
        _pages.Register<CSharpCodeStudioViewModel>(() => new CSharpCodeStudioView());
        _pages.Register<CSharpNotebookStudioViewModel>(() => new CSharpNotebookStudioView());
        _pages.Register<CSharpDocsViewModel>(() => new CSharpDocsView());
        _pages.Register<CSharpBlindProblemsViewModel>(() => new CSharpBlindProblemsView());
        _pages.Register<CSharpSettingsViewModel>(() => new CSharpSettingsView());
    }

    /// <summary>The page views, built lazily and kept alive (exposed so tests and tooling can count them).</summary>
    public KeepAlivePageHost Pages => _pages;

    /// <summary>Releases every page view; the studio is going away for good.</summary>
    public void Dispose() => _pages.Dispose();

    private void OnHostKeyDown(object? sender, KeyEventArgs e)
    {
        var isModifier = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (isModifier && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && (e.Key == Key.OemComma || e.Key == Key.Oem1))
        {
            if (DataContext is CSharpStudioHostViewModel hostVm)
            {
                hostVm.NavigateToSettings();
                e.Handled = true;
            }
        }
    }
}
