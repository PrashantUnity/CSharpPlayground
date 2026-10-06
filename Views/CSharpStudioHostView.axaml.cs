using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PdfEditorApp.Plugins.CSharpEditor.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Controls.Common;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using CSharpBlindProblemsViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.BlindProblems.CSharpBlindProblemsViewModel;
using CSharpCodeStudioViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio.CSharpCodeStudioViewModel;
using CSharpDocsViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Docs.CSharpDocsViewModel;
using CSharpManagerViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Hub.CSharpManagerViewModel;
using CSharpNotebookStudioViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Notebooks.CSharpNotebookStudioViewModel;
using CSharpSettingsViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings.CSharpSettingsViewModel;
using CSharpStudioHostViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Common.CSharpStudioHostViewModel;

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
        _pages.Register<PdfEditorApp.Plugins.CSharpEditor.ViewModels.Server.FryServerStudioViewModel>(() => new PdfEditorApp.Plugins.CSharpEditor.Views.Server.FryServerStudioView());
    }

    /// <summary>The page views, built lazily and kept alive (exposed so tests and tooling can count them).</summary>
    public KeepAlivePageHost Pages => _pages;

    /// <summary>Releases every page view; the studio is going away for good.</summary>
    public void Dispose()
    {
        _stallMonitor?.Dispose();
        _stallMonitor = null;
        _pages.Dispose();
    }

    // Debug builds report every UI-thread stall over 200 ms to the debug log, with the work that was running.
    private UiStallMonitor? _stallMonitor;

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
#if DEBUG
        if (_stallMonitor == null && DataContext is CSharpStudioHostViewModel host) _stallMonitor = new UiStallMonitor(host.Activities);
#endif
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _stallMonitor?.Dispose();
        _stallMonitor = null;
    }

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
