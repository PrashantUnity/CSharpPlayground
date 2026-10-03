using System;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Services.Common;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.BlindProblems;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Common;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Docs;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Hub;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Notebooks;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Server;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings;

namespace PdfEditorApp.Plugins.CSharpEditor.Runner;

/// <summary>
/// Coordinates state synchronization between CSharpStudioHostViewModel pages and the native menu bar.
/// Decoupled from Window rendering so menu state rules can be unit tested without headless compositor overhead.
/// </summary>
public sealed class MainWindowMenuCoordinator
{
    private readonly NativeMenu _rootMenu;
    private INotifyPropertyChanged? _monitoredPageVm;

    public CSharpStudioHostViewModel StudioHostVm { get; }

    public MainWindowMenuCoordinator(NativeMenu rootMenu, CSharpStudioHostViewModel hostVm)
    {
        _rootMenu = rootMenu ?? throw new ArgumentNullException(nameof(rootMenu));
        StudioHostVm = hostVm ?? throw new ArgumentNullException(nameof(hostVm));

        StudioHostVm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName is nameof(CSharpStudioHostViewModel.CurrentPage) or nameof(CSharpStudioHostViewModel.IsOnManagerPage))
            {
                HookActivePageEvents();
                UpdateMenuStates();
            }
        };

        HookNativeMenuUpdates();
    }

    private CSharpCodeStudioViewModel? ActiveCodeStudio => StudioHostVm.CurrentPage as CSharpCodeStudioViewModel;
    private CSharpNotebookStudioViewModel? ActiveNotebookStudio => StudioHostVm.CurrentPage as CSharpNotebookStudioViewModel;
    private FryServerStudioViewModel? ActiveServerStudio => StudioHostVm.CurrentPage as FryServerStudioViewModel;
    private CSharpManagerViewModel? ActiveManager => StudioHostVm.CurrentPage as CSharpManagerViewModel;
    private CSharpDocsViewModel? ActiveDocs => StudioHostVm.CurrentPage as CSharpDocsViewModel;
    private CSharpSettingsViewModel? ActiveSettings => StudioHostVm.CurrentPage as CSharpSettingsViewModel;
    private CSharpBlindProblemsViewModel? ActiveBlindProblems => StudioHostVm.CurrentPage as CSharpBlindProblemsViewModel;

    private NativeMenuItem? GetTopMenu(string headerPrefix) =>
        _rootMenu.Items.OfType<NativeMenuItem>()
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

    public void SetItemEnabled(string menu, string item, bool enabled, bool exact = false)
    {
        var menuItem = GetSubItem(menu, item, exact);
        if (menuItem != null) menuItem.IsEnabled = enabled;
    }

    public void HookNativeMenuUpdates()
    {
        _rootMenu.NeedsUpdate += (_, _) => UpdateMenuStates();
        foreach (var item in _rootMenu.Items.OfType<NativeMenuItem>())
        {
            if (item.Menu != null) item.Menu.NeedsUpdate += (_, _) => UpdateMenuStates();
        }
    }

    public void HookActivePageEvents()
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

    private void OnMonitoredPagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UiDispatchHelper.RunOnUi(UpdateMenuStates);
    }

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
        SetItemEnabled("View", "Customization", true);
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
            SetItemEnabled("Run", "Apply Script as Customization", !isRunning);
            SetItemEnabled("Run", "Reload Customizations", true);
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
            SetItemEnabled("Run", "Apply Script as Customization", false);
            SetItemEnabled("Run", "Reload Customizations", true);
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
            SetItemEnabled("Run", "Apply Script as Customization", false);
            SetItemEnabled("Run", "Reload Customizations", true);
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
            SetItemEnabled("Run", "Apply Script as Customization", false);
            SetItemEnabled("Run", "Reload Customizations", isSettings);
        }

        // Help Menu
        SetItemEnabled("Help", "Welcome", !isHub);
        SetItemEnabled("Help", "Documentation", !isDocs);
        SetItemEnabled("Help", "Blind 75", !isBlind);
    }

    public static NativeMenu CreateDefaultMenu()
    {
        var root = new NativeMenu();

        // File
        var fileMenu = new NativeMenu();
        fileMenu.Items.Add(new NativeMenuItem("New Script"));
        fileMenu.Items.Add(new NativeMenuItem("New Notebook"));
        fileMenu.Items.Add(new NativeMenuItem("New API Server"));
        fileMenu.Items.Add(new NativeMenuItemSeparator());
        fileMenu.Items.Add(new NativeMenuItem("Open File..."));
        fileMenu.Items.Add(new NativeMenuItem("Open Folder / Workspace..."));
        fileMenu.Items.Add(new NativeMenuItem("Open User Customization Script (init.csx)"));
        fileMenu.Items.Add(new NativeMenuItem("Open Workspace Customization Script"));
        fileMenu.Items.Add(new NativeMenuItemSeparator());
        fileMenu.Items.Add(new NativeMenuItem("Save"));
        fileMenu.Items.Add(new NativeMenuItem("Save As..."));
        fileMenu.Items.Add(new NativeMenuItem("Save All"));
        fileMenu.Items.Add(new NativeMenuItemSeparator());
        fileMenu.Items.Add(new NativeMenuItem("Close Tab"));
        fileMenu.Items.Add(new NativeMenuItem("Close Window"));
        root.Items.Add(new NativeMenuItem("File") { Menu = fileMenu });

        // Edit
        var editMenu = new NativeMenu();
        editMenu.Items.Add(new NativeMenuItem("Undo"));
        editMenu.Items.Add(new NativeMenuItem("Redo"));
        editMenu.Items.Add(new NativeMenuItemSeparator());
        editMenu.Items.Add(new NativeMenuItem("Cut"));
        editMenu.Items.Add(new NativeMenuItem("Copy"));
        editMenu.Items.Add(new NativeMenuItem("Paste"));
        editMenu.Items.Add(new NativeMenuItem("Select All"));
        editMenu.Items.Add(new NativeMenuItemSeparator());
        editMenu.Items.Add(new NativeMenuItem("Find..."));
        editMenu.Items.Add(new NativeMenuItem("Replace..."));
        editMenu.Items.Add(new NativeMenuItem("Find in Files"));
        editMenu.Items.Add(new NativeMenuItemSeparator());
        editMenu.Items.Add(new NativeMenuItem("Format Document"));
        root.Items.Add(new NativeMenuItem("Edit") { Menu = editMenu });

        // View
        var viewMenu = new NativeMenu();
        viewMenu.Items.Add(new NativeMenuItem("Command Palette..."));
        viewMenu.Items.Add(new NativeMenuItemSeparator());
        viewMenu.Items.Add(new NativeMenuItem("Explorer"));
        viewMenu.Items.Add(new NativeMenuItem("Search"));
        viewMenu.Items.Add(new NativeMenuItem("Run & Debug"));
        viewMenu.Items.Add(new NativeMenuItem("Dependencies & Packages"));
        viewMenu.Items.Add(new NativeMenuItem("Problems"));
        viewMenu.Items.Add(new NativeMenuItemSeparator());
        viewMenu.Items.Add(new NativeMenuItem("Toggle Primary Side Bar"));
        viewMenu.Items.Add(new NativeMenuItem("Toggle Bottom Panel"));
        viewMenu.Items.Add(new NativeMenuItemSeparator());
        viewMenu.Items.Add(new NativeMenuItem("Zoom In"));
        viewMenu.Items.Add(new NativeMenuItem("Zoom Out"));
        viewMenu.Items.Add(new NativeMenuItem("Reset Zoom"));
        viewMenu.Items.Add(new NativeMenuItemSeparator());
        viewMenu.Items.Add(new NativeMenuItem("Return to Hub"));
        viewMenu.Items.Add(new NativeMenuItem("Documentation & Learning"));
        viewMenu.Items.Add(new NativeMenuItem("Blind 75 Algorithm Hub"));
        viewMenu.Items.Add(new NativeMenuItem("Customization & Extensions..."));
        viewMenu.Items.Add(new NativeMenuItem("Settings..."));
        root.Items.Add(new NativeMenuItem("View") { Menu = viewMenu });

        // Run
        var runMenu = new NativeMenu();
        runMenu.Items.Add(new NativeMenuItem("Start Debugging"));
        runMenu.Items.Add(new NativeMenuItem("Run Without Debugging"));
        runMenu.Items.Add(new NativeMenuItem("Stop Execution"));
        runMenu.Items.Add(new NativeMenuItemSeparator());
        runMenu.Items.Add(new NativeMenuItem("Apply Script as Customization"));
        runMenu.Items.Add(new NativeMenuItem("Reload Customizations"));
        runMenu.Items.Add(new NativeMenuItemSeparator());
        runMenu.Items.Add(new NativeMenuItem("Step Over"));
        runMenu.Items.Add(new NativeMenuItem("Step Into"));
        runMenu.Items.Add(new NativeMenuItemSeparator());
        runMenu.Items.Add(new NativeMenuItem("Toggle Breakpoint"));
        runMenu.Items.Add(new NativeMenuItem("Clear All Breakpoints"));
        root.Items.Add(new NativeMenuItem("Run") { Menu = runMenu });

        // Window
        var windowMenu = new NativeMenu();
        windowMenu.Items.Add(new NativeMenuItem("Minimize"));
        windowMenu.Items.Add(new NativeMenuItem("Zoom"));
        windowMenu.Items.Add(new NativeMenuItemSeparator());
        windowMenu.Items.Add(new NativeMenuItem("Bring All to Front"));
        root.Items.Add(new NativeMenuItem("Window") { Menu = windowMenu });

        // Help
        var helpMenu = new NativeMenu();
        helpMenu.Items.Add(new NativeMenuItem("Welcome"));
        helpMenu.Items.Add(new NativeMenuItem("Documentation & Learning"));
        helpMenu.Items.Add(new NativeMenuItem("Blind 75 Algorithm Hub"));
        helpMenu.Items.Add(new NativeMenuItemSeparator());
        helpMenu.Items.Add(new NativeMenuItem("About FrySharp"));
        root.Items.Add(new NativeMenuItem("Help") { Menu = helpMenu });

        return root;
    }
}
