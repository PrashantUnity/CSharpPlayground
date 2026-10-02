using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.BlindProblems;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Common;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Docs;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Hub;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Notebooks;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Server;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings;
using PdfEditorApp.Plugins.CSharpEditor.Views;

namespace PdfEditorApp.Plugins.CSharpEditor.Runner;

public partial class MainWindow : Window
{
    public CSharpStudioHostViewModel StudioHostVm { get; }

    private CSharpCodeStudioViewModel? ActiveCodeStudio => StudioHostVm.CurrentPage as CSharpCodeStudioViewModel;
    private CSharpNotebookStudioViewModel? ActiveNotebookStudio => StudioHostVm.CurrentPage as CSharpNotebookStudioViewModel;
    private FryServerStudioViewModel? ActiveServerStudio => StudioHostVm.CurrentPage as FryServerStudioViewModel;
    private CSharpManagerViewModel? ActiveManager => StudioHostVm.CurrentPage as CSharpManagerViewModel;
    private CSharpDocsViewModel? ActiveDocs => StudioHostVm.CurrentPage as CSharpDocsViewModel;
    private CSharpSettingsViewModel? ActiveSettings => StudioHostVm.CurrentPage as CSharpSettingsViewModel;
    private CSharpBlindProblemsViewModel? ActiveBlindProblems => StudioHostVm.CurrentPage as CSharpBlindProblemsViewModel;

    private INotifyPropertyChanged? _monitoredPageVm;

    public MainWindow()
    {
        InitializeComponent();

        var settingsStore = new StandaloneSettingsStore();
        var sp = new StandaloneServiceProvider(settingsStore);

        StudioHostVm = new CSharpStudioHostViewModel(sp);
        StudioHostVm.RequestClose = () => Close();
        DataContext = StudioHostVm;
        StudioHost.DataContext = StudioHostVm;

        StudioHostVm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName is nameof(CSharpStudioHostViewModel.CurrentPage) or nameof(CSharpStudioHostViewModel.IsOnManagerPage))
            {
                HookActivePageEvents();
                UpdateMenuStates();
            }
        };

        HookNativeMenuUpdates();
        HookActivePageEvents();
        UpdateMenuStates();
    }

    // ── Context-Aware Native Menu State Synchronization ──
    private NativeMenu? RootMenu => NativeMenu.GetMenu(this);

    private NativeMenuItem? GetTopMenu(string headerPrefix) =>
        RootMenu?.Items.OfType<NativeMenuItem>()
            .FirstOrDefault(m => m.Header != null && m.Header.StartsWith(headerPrefix, StringComparison.OrdinalIgnoreCase));

    public NativeMenuItem? GetSubItem(string menuHeader, string itemHeader, bool exact = false)
    {
        var top = GetTopMenu(menuHeader);
        return top?.Menu?.Items.OfType<NativeMenuItem>()
            .FirstOrDefault(i => i.Header != null &&
                (exact
                    ? string.Equals(i.Header, itemHeader, StringComparison.OrdinalIgnoreCase)
                    : i.Header.StartsWith(itemHeader, StringComparison.OrdinalIgnoreCase)));
    }

    private void SetItemEnabled(string menu, string item, bool enabled, bool exact = false)
    {
        var menuItem = GetSubItem(menu, item, exact);
        if (menuItem != null) menuItem.IsEnabled = enabled;
    }

    private void HookNativeMenuUpdates()
    {
        if (RootMenu == null) return;
        RootMenu.NeedsUpdate += (_, _) => UpdateMenuStates();
        foreach (var item in RootMenu.Items.OfType<NativeMenuItem>())
        {
            if (item.Menu != null) item.Menu.NeedsUpdate += (_, _) => UpdateMenuStates();
        }
    }

    private void HookActivePageEvents()
    {
        if (_monitoredPageVm != null)
        {
            _monitoredPageVm.PropertyChanged -= OnMonitoredPagePropertyChanged;
            _monitoredPageVm = null;
        }

        if (ActiveCodeStudio is { } code)
        {
            _monitoredPageVm = code;
            code.PropertyChanged += OnMonitoredPagePropertyChanged;
        }
        else if (ActiveNotebookStudio is { } nb)
        {
            _monitoredPageVm = nb;
            nb.PropertyChanged += OnMonitoredPagePropertyChanged;
        }
        else if (ActiveServerStudio is { } srv)
        {
            _monitoredPageVm = srv;
            srv.PropertyChanged += OnMonitoredPagePropertyChanged;
        }
    }

    private void OnMonitoredPagePropertyChanged(object? sender, PropertyChangedEventArgs e) => UpdateMenuStates();

    public void UpdateMenuStates()
    {
        bool isCode = ActiveCodeStudio != null;
        bool isNotebook = ActiveNotebookStudio != null;
        bool isServer = ActiveServerStudio != null;
        bool isDocs = ActiveDocs != null;
        bool isSettings = ActiveSettings != null;
        bool isBlind = ActiveBlindProblems != null;
        bool isHub = ActiveManager != null || StudioHostVm.IsOnManagerPage;

        // File Menu
        bool canSave = isCode || isNotebook || isServer;
        SetItemEnabled("File", "Save", canSave, exact: true);
        SetItemEnabled("File", "Save As", canSave);
        SetItemEnabled("File", "Save All", canSave);
        SetItemEnabled("File", "Close Tab", !isHub);

        // Edit Menu
        SetItemEnabled("Edit", "Replace", isCode);
        SetItemEnabled("Edit", "Find in Files", isCode);
        SetItemEnabled("Edit", "Format Document", isCode || isNotebook);

        // View Menu
        SetItemEnabled("View", "Explorer", isCode || isNotebook);
        SetItemEnabled("View", "Search", isCode || isNotebook);
        SetItemEnabled("View", "Run & Debug", isCode);
        SetItemEnabled("View", "Dependencies", isCode);
        SetItemEnabled("View", "Problems", isCode);
        SetItemEnabled("View", "Toggle Primary", isCode || isNotebook);
        SetItemEnabled("View", "Toggle Bottom", isCode || isNotebook);
        SetItemEnabled("View", "Zoom In", isCode || isNotebook);
        SetItemEnabled("View", "Zoom Out", isCode || isNotebook);
        SetItemEnabled("View", "Reset Zoom", isCode || isNotebook);
        SetItemEnabled("View", "Return to Hub", !isHub);
        SetItemEnabled("View", "Documentation", !isDocs);
        SetItemEnabled("View", "Blind 75", !isBlind);
        SetItemEnabled("View", "Settings", !isSettings);

        // Run Menu
        if (isCode)
        {
            var code = ActiveCodeStudio!;
            bool isRunning = code.IsExecuting || code.IsDebugging;
            SetItemEnabled("Run", "Start Debugging", !isRunning);
            SetItemEnabled("Run", "Run Without Debugging", !isRunning);
            SetItemEnabled("Run", "Stop Execution", isRunning);
            SetItemEnabled("Run", "Step Over", code.IsDebugging && code.IsPaused);
            SetItemEnabled("Run", "Step Into", code.IsDebugging && code.IsPaused);
            SetItemEnabled("Run", "Toggle Breakpoint", true);
            SetItemEnabled("Run", "Clear All Breakpoints", code.Breakpoints?.Count > 0);
        }
        else if (isNotebook)
        {
            var nb = ActiveNotebookStudio!;
            SetItemEnabled("Run", "Start Debugging", false);
            SetItemEnabled("Run", "Run Without Debugging", !nb.IsExecuting);
            SetItemEnabled("Run", "Stop Execution", nb.IsExecuting);
            SetItemEnabled("Run", "Step Over", false);
            SetItemEnabled("Run", "Step Into", false);
            SetItemEnabled("Run", "Toggle Breakpoint", false);
            SetItemEnabled("Run", "Clear All Breakpoints", false);
        }
        else if (isServer)
        {
            var srv = ActiveServerStudio!;
            SetItemEnabled("Run", "Start Debugging", false);
            SetItemEnabled("Run", "Run Without Debugging", !srv.IsServerRunning);
            SetItemEnabled("Run", "Stop Execution", srv.IsServerRunning);
            SetItemEnabled("Run", "Step Over", false);
            SetItemEnabled("Run", "Step Into", false);
            SetItemEnabled("Run", "Toggle Breakpoint", false);
            SetItemEnabled("Run", "Clear All Breakpoints", false);
        }
        else
        {
            SetItemEnabled("Run", "Start Debugging", false);
            SetItemEnabled("Run", "Run Without Debugging", false);
            SetItemEnabled("Run", "Stop Execution", false);
            SetItemEnabled("Run", "Step Over", false);
            SetItemEnabled("Run", "Step Into", false);
            SetItemEnabled("Run", "Toggle Breakpoint", false);
            SetItemEnabled("Run", "Clear All Breakpoints", false);
        }

        // Help Menu
        SetItemEnabled("Help", "Welcome", !isHub);
        SetItemEnabled("Help", "Documentation", !isDocs);
        SetItemEnabled("Help", "Blind 75", !isBlind);
    }

}
