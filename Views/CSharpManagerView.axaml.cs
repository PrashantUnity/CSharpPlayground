using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;

namespace PdfEditorApp.Plugins.CSharpEditor.Views;

public partial class CSharpManagerView : UserControl
{
    public CSharpManagerView()
    {
        InitializeComponent();
    }

    // Opens the OS's own native folder browser for the "New Script"/"New Notebook" location prompt —
    // starts inside the plugin's library root, but the user can navigate anywhere on disk from there.
    // A folder inside the root is stored as a path relative to it (existing behavior); a folder
    // outside it is stored as an absolute path, which LocalScriptStorageService.WriteNewDocumentAsync
    // recognizes and registers in its external-documents index so the new document still shows up in
    // "Your Workspace" later (see LoadWorkspaceSummariesAsync / FindExistingFilePath) even though it
    // doesn't live under the library folder.
    private async void OnBrowseFolderClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not CSharpManagerViewModel vm) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } storageProvider) return;

        var libraryRoot = vm.LibraryRootPath;
        var startFolder = await storageProvider.TryGetFolderFromPathAsync(new Uri(libraryRoot));

        var result = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose a Folder",
            AllowMultiple = false,
            SuggestedStartLocation = startFolder
        });

        if (result.Count == 0 || result[0].TryGetLocalPath() is not { } pickedPath)
        {
            return; // user cancelled the dialog
        }

        var relative = Path.GetRelativePath(libraryRoot, pickedPath).Replace(Path.DirectorySeparatorChar, '/');
        var isOutsideRoot = relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative);

        if (isOutsideRoot)
        {
            vm.SelectedFolderPath = pickedPath; // absolute — WriteNewDocumentAsync treats this as external
            vm.LocationWarning = "This document will be saved outside your script library, at the exact folder you chose.";
        }
        else
        {
            vm.SelectedFolderPath = relative == "." ? null : relative;
            vm.LocationWarning = null;
        }
    }
}
