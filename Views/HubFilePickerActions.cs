using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;

namespace PdfEditorApp.Plugins.CSharpEditor.Views;

/// <summary>
/// File/folder-picker actions shared by CSharpManagerView and the Hub child controls
/// (HubSidebarControl, HubWelcomeSectionControl, HubWorkspacesPanelControl,
/// HubCreateItemDialogControl) that expose an "Open Project" or "Browse" button, so the
/// StorageProvider boilerplate isn't duplicated across each one.
/// </summary>
internal static class HubFilePickerActions
{
    public static async Task OpenProjectFileAsync(Visual owner, CSharpManagerViewModel vm)
    {
        var topLevel = TopLevel.GetTopLevel(owner);
        if (topLevel?.StorageProvider is not { } storageProvider) return;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Existing Project or Document",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("All Supported FryPDF & C# Files")
                {
                    Patterns = new[] { "*.frycsproj", "*.frynbproj", "*.frycs", "*.frynb", "*.cs", "*.csx", "*.csproj", "*.zip" }
                },
                new("FryPDF Projects (*.frycsproj, *.frynbproj)")
                {
                    Patterns = new[] { "*.frycsproj", "*.frynbproj" }
                },
                new("FryPDF Documents (*.frycs, *.frynb)")
                {
                    Patterns = new[] { "*.frycs", "*.frynb" }
                },
                new("C# Code & Projects (*.cs, *.csx, *.csproj)")
                {
                    Patterns = new[] { "*.cs", "*.csx", "*.csproj" }
                },
                new("Project Archives (*.zip)")
                {
                    Patterns = new[] { "*.zip" }
                },
                new("All Files (*.*)")
                {
                    Patterns = new[] { "*.*" }
                }
            }
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } filePath)
        {
            await vm.OpenExistingProjectAsync(filePath);
        }
    }

    public static async Task OpenProjectFolderAsync(Visual owner, CSharpManagerViewModel vm)
    {
        var topLevel = TopLevel.GetTopLevel(owner);
        if (topLevel?.StorageProvider is not { } storageProvider) return;

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open Existing Project Folder",
            AllowMultiple = false
        });

        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } folderPath)
        {
            await vm.OpenExistingProjectAsync(folderPath);
        }
    }

    public static async Task BrowseFolderAsync(Visual owner, CSharpManagerViewModel vm)
    {
        var topLevel = TopLevel.GetTopLevel(owner);
        if (topLevel?.StorageProvider is not { } storageProvider) return;

        var activeRoot = vm.ActiveWorkspaceRootPath;
        var startFolder = await storageProvider.TryGetFolderFromPathAsync(new Uri(activeRoot));

        var result = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose a Folder",
            AllowMultiple = false,
            SuggestedStartLocation = startFolder
        });

        if (result.Count == 0 || result[0].TryGetLocalPath() is not { } pickedPath)
        {
            return;
        }

        var relative = Path.GetRelativePath(activeRoot, pickedPath).Replace(Path.DirectorySeparatorChar, '/');
        var isOutsideRoot = relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative);

        if (isOutsideRoot)
        {
            vm.SelectedFolderPath = pickedPath;
            vm.LocationWarning = "This document will be saved outside the current workspace folder, at the exact folder you chose.";
        }
        else
        {
            vm.SelectedFolderPath = relative == "." ? null : relative;
            vm.LocationWarning = null;
        }
    }
}
