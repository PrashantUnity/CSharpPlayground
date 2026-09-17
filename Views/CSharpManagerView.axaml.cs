using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;

namespace PdfEditorApp.Plugins.CSharpEditor.Views;

public partial class CSharpManagerView : UserControl
{
    public CSharpManagerView()
    {
        InitializeComponent();
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DropEvent, OnFileDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if ((e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)) && e.Key == Key.O)
        {
            e.Handled = true;
            OnOpenProjectFileClick(this, new RoutedEventArgs());
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Contains(DataFormat.File))
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private async void OnFileDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not CSharpManagerViewModel vm) return;
        var files = e.DataTransfer.TryGetFiles();
        if (files == null) return;

        foreach (var file in files)
        {
            if (file.TryGetLocalPath() is { } path)
            {
                await vm.OpenExistingProjectAsync(path);
                break;
            }
        }
    }

    public async void OnOpenProjectFileClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not CSharpManagerViewModel vm) return;

        var topLevel = TopLevel.GetTopLevel(this);
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

    public async void OnOpenProjectFolderClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not CSharpManagerViewModel vm) return;

        var topLevel = TopLevel.GetTopLevel(this);
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
            return;
        }

        var relative = Path.GetRelativePath(libraryRoot, pickedPath).Replace(Path.DirectorySeparatorChar, '/');
        var isOutsideRoot = relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative);

        if (isOutsideRoot)
        {
            vm.SelectedFolderPath = pickedPath;
            vm.LocationWarning = "This document will be saved outside your script library, at the exact folder you chose.";
        }
        else
        {
            vm.SelectedFolderPath = relative == "." ? null : relative;
            vm.LocationWarning = null;
        }
    }
}
