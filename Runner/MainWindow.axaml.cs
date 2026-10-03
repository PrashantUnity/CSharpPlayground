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
    private readonly MainWindowMenuCoordinator _menuCoordinator;

    private CSharpCodeStudioViewModel? ActiveCodeStudio => StudioHostVm.CurrentPage as CSharpCodeStudioViewModel;
    private CSharpNotebookStudioViewModel? ActiveNotebookStudio => StudioHostVm.CurrentPage as CSharpNotebookStudioViewModel;
    private FryServerStudioViewModel? ActiveServerStudio => StudioHostVm.CurrentPage as FryServerStudioViewModel;
    private CSharpManagerViewModel? ActiveManager => StudioHostVm.CurrentPage as CSharpManagerViewModel;
    private CSharpDocsViewModel? ActiveDocs => StudioHostVm.CurrentPage as CSharpDocsViewModel;
    private CSharpSettingsViewModel? ActiveSettings => StudioHostVm.CurrentPage as CSharpSettingsViewModel;
    private CSharpBlindProblemsViewModel? ActiveBlindProblems => StudioHostVm.CurrentPage as CSharpBlindProblemsViewModel;

    public MainWindow() : this(null)
    {
    }

    public MainWindow(CSharpStudioHostViewModel? hostVm)
    {
        InitializeComponent();

        if (hostVm != null)
        {
            StudioHostVm = hostVm;
        }
        else
        {
            var settingsStore = new StandaloneSettingsStore();
            var sp = new StandaloneServiceProvider(settingsStore);
            StudioHostVm = new CSharpStudioHostViewModel(sp);
        }

        StudioHostVm.RequestClose = () => Close();
        DataContext = StudioHostVm;
        StudioHost.DataContext = StudioHostVm;

        _menuCoordinator = new MainWindowMenuCoordinator(NativeMenu.GetMenu(this) ?? MainWindowMenuCoordinator.CreateDefaultMenu(), StudioHostVm);
        _menuCoordinator.HookActivePageEvents();
        _menuCoordinator.UpdateMenuStates();

        if (hostVm == null)
        {
            InitializeExtensibility();
        }
    }

    private void InitializeExtensibility()
    {
        var uiService = PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.StudioAppContext.Instance.UiService;
        uiService.ActiveTopLevelResolver = () => this;
        uiService.MainWindowResolver = () => this;
        uiService.DialogsService.PickFileHandler = async (title, exts) =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            });
            return files.Count > 0 ? files[0].TryGetLocalPath() : null;
        };
        uiService.DialogsService.PickFolderHandler = async title =>
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            });
            return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        };

        var customizationManager = new PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.CustomizationManager();
        PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.StudioAppContext.Instance.CustomizationManager = customizationManager;

        var extensionManager = new PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Extensions.ExtensionManager();
        PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.StudioAppContext.Instance.ExtensionManager = extensionManager;

        var globalExtensionsDir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), ".frysharp", "extensions");
        _ = Task.Run(async () =>
        {
            await customizationManager.InitializeAsync(enableHotReload: true);
            if (System.IO.Directory.Exists(globalExtensionsDir))
            {
                await extensionManager.DiscoverAndLoadAllAsync(globalExtensionsDir, enableHotReload: true);
            }
        });
    }

    // ── Context-Aware Native Menu State Synchronization ──
    public NativeMenuItem? GetSubItem(string menuHeader, string itemHeader, bool exact = false) =>
        _menuCoordinator.GetSubItem(menuHeader, itemHeader, exact);

    public void UpdateMenuStates() => _menuCoordinator.UpdateMenuStates();

}
