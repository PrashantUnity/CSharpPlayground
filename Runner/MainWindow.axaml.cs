using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;

namespace PdfEditorApp.Plugins.CSharpEditor.Runner;

public partial class MainWindow : Window
{
    public CSharpStudioHostViewModel StudioHostVm { get; }

    private CSharpCodeStudioViewModel? ActiveCodeStudio => StudioHostVm.CurrentPage as CSharpCodeStudioViewModel;
    private CSharpNotebookStudioViewModel? ActiveNotebookStudio => StudioHostVm.CurrentPage as CSharpNotebookStudioViewModel;

    public MainWindow()
    {
        InitializeComponent();

        var settingsStore = new StandaloneSettingsStore();
        var sp = new StandaloneServiceProvider(settingsStore);

        StudioHostVm = new CSharpStudioHostViewModel(sp);
        StudioHostVm.RequestClose = () => Close();
        DataContext = StudioHostVm;
        StudioHost.DataContext = StudioHostVm;
    }

    // ── File Menu Actions ──
    private async void NewScript_OnClick(object? sender, EventArgs e)
    {
        if (ActiveCodeStudio is { } code) await code.NewScriptCommand.ExecuteAsync(null);
        else await StudioHostVm.ManagerViewModel.CreateNewScriptAsync();
    }

    private async void NewNotebook_OnClick(object? sender, EventArgs e)
    {
        await StudioHostVm.ManagerViewModel.CreateNewNotebookAsync();
    }

    private async void NewServer_OnClick(object? sender, EventArgs e) =>
        await StudioHostVm.ManagerViewModel.CreateNewServerCommand.ExecuteAsync(null);

    private async void OpenFile_OnClick(object? sender, EventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Script or Project",
            AllowMultiple = false
        });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            if (ext is ".ipynb" or ".csnb" or ".frynb")
            {
                var nb = await StudioHostVm.StorageService.LoadNotebookAsync(path);
                if (nb != null) StudioHostVm.NavigateToNotebookStudio(nb);
            }
            else if (ext is ".fryserver")
            {
                var srv = await StudioHostVm.StorageService.LoadServerDocumentAsync(path);
                if (srv != null) StudioHostVm.NavigateToServerStudio(srv, path);
            }
            else if (ext is ".zip" or ".csproj" or ".frycsproj")
            {
                await StudioHostVm.OpenExistingProjectAsync(path);
            }
            else if (ActiveCodeStudio is { } code)
            {
                await code.OpenExternalProjectAsync(path);
            }
            else
            {
                var doc = new ScriptDocumentItem
                {
                    Title = System.IO.Path.GetFileName(path),
                    SourceFilePath = path,
                    Code = System.IO.File.Exists(path) ? await System.IO.File.ReadAllTextAsync(path) : string.Empty
                };
                StudioHostVm.NavigateToCodeStudio(doc);
            }
        }
    }

    private async void OpenFolder_OnClick(object? sender, EventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open Workspace Folder",
            AllowMultiple = false
        });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            await StudioHostVm.OpenExistingProjectAsync(path);
        }
    }

    private async void Save_OnClick(object? sender, EventArgs e)
    {
        if (ActiveCodeStudio is { } code) await code.SaveCommand.ExecuteAsync(null);
        else if (ActiveNotebookStudio is { } nb) await nb.SaveAsync();
    }

    private async void SaveAs_OnClick(object? sender, EventArgs e)
    {
        if (ActiveCodeStudio is { } code)
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Script As",
                DefaultExtension = ".csx",
                SuggestedFileName = code.Script?.Title ?? "Script.csx"
            });
            if (file?.TryGetLocalPath() is { } path)
            {
                await System.IO.File.WriteAllTextAsync(path, code.Code);
            }
        }
    }

    private async void SaveAll_OnClick(object? sender, EventArgs e)
    {
        if (ActiveCodeStudio is { } code) await code.SaveCommand.ExecuteAsync(null);
        else if (ActiveNotebookStudio is { } nb) await nb.SaveAsync();
    }

    private async void CloseTab_OnClick(object? sender, EventArgs e)
    {
        if (ActiveCodeStudio is { } code)
        {
            var tab = code.OpenTabs.FirstOrDefault(t => t.IsActive);
            if (tab != null) await code.CloseTabAsync(tab);
        }
        else
        {
            StudioHostVm.NavigateToManager();
        }
    }

    private void CloseWindow_OnClick(object? sender, EventArgs e) => Close();

    // ── Edit Menu Actions (enabled for standard Cocoa / Avalonia routing) ──
    private void Undo_OnClick(object? sender, EventArgs e) { }
    private void Redo_OnClick(object? sender, EventArgs e) { }
    private void Cut_OnClick(object? sender, EventArgs e) { }
    private void Copy_OnClick(object? sender, EventArgs e) { }
    private void Paste_OnClick(object? sender, EventArgs e) { }
    private void SelectAll_OnClick(object? sender, EventArgs e) { }

    private void Find_OnClick(object? sender, EventArgs e) => ActiveCodeStudio?.ToggleSearch();
    private void Replace_OnClick(object? sender, EventArgs e) => ActiveCodeStudio?.ToggleSearch();
    private void FindInFiles_OnClick(object? sender, EventArgs e) => ActiveCodeStudio?.SelectActivityBarItem(1);
    private void FormatDocument_OnClick(object? sender, EventArgs e) => ActiveCodeStudio?.FormatCode();

    // ── View Menu Actions ──
    private void CommandPalette_OnClick(object? sender, EventArgs e) => ActiveCodeStudio?.ShowCommandPalette();
    private void ViewExplorer_OnClick(object? sender, EventArgs e) => ActiveCodeStudio?.SelectActivityBarItem(0);
    private void ViewSearch_OnClick(object? sender, EventArgs e) => ActiveCodeStudio?.SelectActivityBarItem(1);
    private void ViewRunDebug_OnClick(object? sender, EventArgs e) => ActiveCodeStudio?.SelectActivityBarItem(2);
    private void ViewNuGet_OnClick(object? sender, EventArgs e) => ActiveCodeStudio?.SelectActivityBarItem(3);
    private void ViewProblems_OnClick(object? sender, EventArgs e) => ActiveCodeStudio?.ShowProblemsTabCommand.Execute(null);
    private void ToggleSideBar_OnClick(object? sender, EventArgs e) => ActiveCodeStudio?.ToggleSideBar();
    private void ToggleBottomDeck_OnClick(object? sender, EventArgs e) => ActiveCodeStudio?.ToggleBottomDeck();
    private void ZoomIn_OnClick(object? sender, EventArgs e) => ActiveCodeStudio?.ZoomIn();
    private void ZoomOut_OnClick(object? sender, EventArgs e) => ActiveCodeStudio?.ZoomOut();
    private void ResetZoom_OnClick(object? sender, EventArgs e) => ActiveCodeStudio?.ResetZoom();
    private void ReturnToHub_OnClick(object? sender, EventArgs e) => StudioHostVm.NavigateToManager();
    private void OpenDocs_OnClick(object? sender, EventArgs e) => StudioHostVm.NavigateToDocs();
    private void OpenBlind_OnClick(object? sender, EventArgs e) => StudioHostVm.NavigateToBlindProblems();
    private void OpenSettings_OnClick(object? sender, EventArgs e) => StudioHostVm.NavigateToSettings();

    // ── Run Menu Actions ──
    private async void StartDebugging_OnClick(object? sender, EventArgs e)
    {
        if (ActiveCodeStudio is { } code) await code.DebugCodeCommand.ExecuteAsync(null);
    }

    private async void RunWithoutDebugging_OnClick(object? sender, EventArgs e)
    {
        if (ActiveCodeStudio is { } code) await code.RunCodeCommand.ExecuteAsync(null);
    }

    private void StopExecution_OnClick(object? sender, EventArgs e)
    {
        if (ActiveCodeStudio is { } code)
        {
            if (code.IsDebugging) code.StopDebug();
            else code.StopCommand.Execute(null);
        }
    }

    private void StepOver_OnClick(object? sender, EventArgs e) => ActiveCodeStudio?.StepOver();
    private void StepInto_OnClick(object? sender, EventArgs e) => ActiveCodeStudio?.StepInto();
    private void ToggleBreakpoint_OnClick(object? sender, EventArgs e)
    {
        if (ActiveCodeStudio is { } code) code.ToggleBreakpoint(code.CaretLine);
    }
    private void ClearAllBreakpoints_OnClick(object? sender, EventArgs e) => ActiveCodeStudio?.ClearAllBreakpoints();

    // ── Window Menu Actions ──
    private void Minimize_OnClick(object? sender, EventArgs e) => WindowState = WindowState.Minimized;
    private void ToggleMaximize_OnClick(object? sender, EventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void BringAllToFront_OnClick(object? sender, EventArgs e) => Activate();

    // ── Help Menu Actions ──
    private void KeyboardShortcuts_OnClick(object? sender, EventArgs e)
    {
        if (ActiveCodeStudio is { } code) code.ShowCommandPalette();
        else StudioHostVm.NavigateToSettings("Keymap");
    }

    private async void ReportIssue_OnClick(object? sender, EventArgs e)
    {
        try { await Launcher.LaunchUriAsync(new Uri("https://github.com/CodeFryDev")); }
        catch { }
    }

    private void About_OnClick(object? sender, EventArgs e)
    {
        var about = new AboutWindow();
        about.OpenDocumentationAction = () => StudioHostVm.NavigateToDocs();
        about.ShowDialog(this);
    }
}
