using System.Collections.ObjectModel;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

public partial class ExplorerItemViewModel : ObservableObject
{
    // Canonical Jupyter-style amber accent, shared with the notebook tab strip/selection highlight.
    private const string NotebookAmberHex = "#D97706";

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _fullPath = string.Empty;

    [ObservableProperty]
    private string? _documentId;

    [ObservableProperty]
    private bool _isDirectory;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _fileExtension = string.Empty;

    [ObservableProperty]
    private int _depth;

    // True only for the single synthetic grouping folder created for a document saved outside the
    // library (see CSharpNotebookStudioViewModel.RebuildExplorerTree) — its FullPath is a real
    // absolute filesystem path, not a library-relative one, so folder-management operations that
    // assume the latter (rename, delete, "new file/folder here") must not be offered for it: acting
    // on them could otherwise touch a real directory outside anything this app actually manages.
    [ObservableProperty]
    private bool _isExternalGroup;

    public bool IsManageableDirectory => IsDirectory && !IsExternalGroup;

    public ExplorerItemViewModel? Parent { get; set; }

    public ObservableCollection<ExplorerItemViewModel> Children { get; } = new();

    public Thickness IndentPadding => new Thickness(Math.Max(4, (Depth * 14) + 4), 0, 4, 0);

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
                ".frynb" or ".ipynb" => "NotebookOutline",
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
            if (IsDirectory) return NotebookAmberHex;

            return FileExtension.ToLowerInvariant() switch
            {
                ".frynb" or ".ipynb" => NotebookAmberHex,
                ".cs" or ".frycs" => "#58A6FF",
                ".json" => "#E5C07B",
                ".md" => "#4EC9B0",
                ".png" or ".jpg" or ".jpeg" or ".svg" => "#C586C0",
                _ => "#8B949E"
            };
        }
    }

    public string ChevronKind => IsExpanded ? "ChevronDown" : "ChevronRight";

    public string ExpansionArrow => IsDirectory ? (IsExpanded ? "⌵" : ">") : " ";

    public Action<ExplorerItemViewModel>? OnItemClicked { get; set; }
    public Action<ExplorerItemViewModel>? OnDeleteRequested { get; set; }
    public Action<ExplorerItemViewModel>? OnNewFileRequested { get; set; }
    public Action<ExplorerItemViewModel>? OnNewFolderRequested { get; set; }
    public Action<ExplorerItemViewModel>? OnRenameCommitted { get; set; }
    public Action<ExplorerItemViewModel>? OnDuplicateRequested { get; set; }
    public Action<ExplorerItemViewModel>? OnCopyPathRequested { get; set; }

    partial void OnDepthChanged(int value)
    {
        OnPropertyChanged(nameof(IndentPadding));
    }

    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(IconKind));
        OnPropertyChanged(nameof(ChevronKind));
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

    [RelayCommand]
    public void RequestDuplicate()
    {
        OnDuplicateRequested?.Invoke(this);
    }

    [RelayCommand]
    public void RequestCopyPath()
    {
        OnCopyPathRequested?.Invoke(this);
    }
}
