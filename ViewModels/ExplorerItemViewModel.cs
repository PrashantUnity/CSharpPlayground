using System;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

public partial class ExplorerItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _fullPath = string.Empty;

    [ObservableProperty]
    private bool _isDirectory;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _fileExtension = string.Empty;

    public ExplorerItemViewModel? Parent { get; set; }

    public ObservableCollection<ExplorerItemViewModel> Children { get; } = new();

    public string IconKind
    {
        get
        {
            if (IsDirectory)
            {
                return IsExpanded ? "FolderOpenOutline" : "FolderOutline";
            }

            return FileExtension.ToLowerInvariant() switch
            {
                ".frynb" => "NotebookOutline",
                ".cs" or ".frycs" => "LanguageCsharp",
                ".json" => "CodeJson",
                ".md" => "FormatHeaderPound",
                ".png" or ".jpg" or ".jpeg" or ".svg" => "ImageOutline",
                _ => "FileOutline"
            };
        }
    }

    public string IconColor
    {
        get
        {
            if (IsDirectory) return "#DDA764";

            return FileExtension.ToLowerInvariant() switch
            {
                ".frynb" => "#D97706",
                ".cs" or ".frycs" => "#58A6FF",
                ".json" => "#E5C07B",
                ".md" => "#4EC9B0",
                ".png" or ".jpg" or ".jpeg" or ".svg" => "#C586C0",
                _ => "#94A3B8"
            };
        }
    }

    public string ExpansionArrow => IsDirectory ? (IsExpanded ? "⌵" : ">") : " ";

    public Action<ExplorerItemViewModel>? OnItemClicked { get; set; }
    public Action<ExplorerItemViewModel>? OnDeleteRequested { get; set; }
    public Action<ExplorerItemViewModel>? OnNewFileRequested { get; set; }
    public Action<ExplorerItemViewModel>? OnNewFolderRequested { get; set; }
    public Action<ExplorerItemViewModel>? OnRenameCommitted { get; set; }

    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(IconKind));
        OnPropertyChanged(nameof(ExpansionArrow));
    }

    [RelayCommand]
    public void ToggleExpand()
    {
        if (IsRenaming) return;

        if (IsDirectory)
        {
            IsExpanded = !IsExpanded;
        }
        else
        {
            OnItemClicked?.Invoke(this);
        }
    }

    [RelayCommand]
    public void Select()
    {
        if (IsRenaming) return;
        OnItemClicked?.Invoke(this);
    }

    [RelayCommand]
    public void StartRename()
    {
        EditName = Name;
        IsRenaming = true;
    }

    [RelayCommand]
    public void CommitRename()
    {
        if (!IsRenaming) return;

        if (!string.IsNullOrWhiteSpace(EditName))
        {
            var trimmed = EditName.Trim();
            if (!IsDirectory && !trimmed.Contains('.'))
            {
                trimmed += string.IsNullOrEmpty(FileExtension) ? ".frynb" : FileExtension;
            }

            Name = trimmed;
            if (!IsDirectory)
            {
                FileExtension = Path.GetExtension(trimmed);
                OnPropertyChanged(nameof(IconKind));
                OnPropertyChanged(nameof(IconColor));
            }
            OnRenameCommitted?.Invoke(this);
        }
        IsRenaming = false;
    }

    [RelayCommand]
    public void CancelRename()
    {
        IsRenaming = false;
    }

    [RelayCommand]
    public void RequestDelete()
    {
        OnDeleteRequested?.Invoke(this);
    }

    [RelayCommand]
    public void RequestNewFile()
    {
        OnNewFileRequested?.Invoke(this);
    }

    [RelayCommand]
    public void RequestNewFolder()
    {
        OnNewFolderRequested?.Invoke(this);
    }
}
