using System.Collections.ObjectModel;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio.Explorer;

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

    // Never set true anymore: the Explorer tree now always reflects a single active workspace root
    // (the internal library or one opened folder), so there is no synthetic cross-root grouping node
    // left to represent. Kept because the context-menu XAML still binds IsManageableDirectory/!IsExternalGroup.
    [ObservableProperty]
    private bool _isExternalGroup;

    public bool IsManageableDirectory => IsDirectory && !IsExternalGroup;

    /// <summary>True for a plain source file (main.py): deleting it removes a file of the user's, so it asks first.</summary>
    [ObservableProperty]
    private bool _isSourceFile;

    /// <summary>The icon and color of the file's language, when it has one (else they follow the extension).</summary>
    public string? LanguageIconKind { get; set; }
    public string? LanguageIconColor { get; set; }

    /// <summary>True between the first and second Delete of a source file, while the row asks to confirm.</summary>
    [ObservableProperty]
    private bool _isConfirmingDelete;

    public string DeleteConfirmationText => $"Delete {Name} from disk?";

    /// <summary>The paths of the folders that are open in a tree, so a rebuilt tree can open the same ones again.</summary>
    public static HashSet<string> ExpandedFolderPaths(IEnumerable<ExplorerItemViewModel> roots)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Collect(roots);
        return paths;

        void Collect(IEnumerable<ExplorerItemViewModel> items)
        {
            foreach (var item in items.Where(i => i.IsDirectory))
            {
                if (item.IsExpanded) paths.Add(item.FullPath);
                Collect(item.Children);
            }
        }
    }

    /// <summary>
    /// False for a folder of a workspace too big to list at once, until it is opened for the first time and its contents are listed:
    /// until then it holds a single placeholder row, so it can be expanded.
    /// </summary>
    public bool ChildrenLoaded { get; set; } = true;

    /// <summary>Asked to list an unlisted folder's contents, when it is opened.</summary>
    public Action<ExplorerItemViewModel>? LoadChildrenRequested { get; set; }

    /// <summary>A row that stands for something else ("Loading...", files a folder has beyond the listing limit): it can't be opened, renamed or deleted.</summary>
    public bool IsPlaceholder { get; init; }

    public ExplorerItemViewModel? Parent { get; set; }

    public ObservableCollection<ExplorerItemViewModel> Children { get; } = new();

    public Thickness IndentPadding => new Thickness(Math.Max(4, (Depth * 14) + 4), 0, 4, 0);

    public static (string IconKind, string IconColor) IconForExtension(string ext)
    {
        return ext.ToLowerInvariant() switch
        {
            ".frynb" or ".ipynb" => ("NotebookOutline", NotebookAmberHex),
            ".cs" or ".frycs" => ("LanguageCsharp", "#58A6FF"),
            ".json" => ("CodeJson", "#E5C07B"),
            ".md" => ("FormatHeaderPound", "#4EC9B0"),
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".ico" or ".webp" or ".svg" or ".tiff" or ".tif" => ("ImageOutline", "#C586C0"),
            ".csv" or ".tsv" => ("Table", "#75D59A"),
            ".pdf" => ("FilePdfBox", "#F14C4C"),
            ".xml" or ".xaml" or ".axaml" or ".html" or ".htm" => ("Xml", "#E5C07B"),
            ".yaml" or ".yml" => ("FileCodeOutline", "#CB88FF"),
            ".sql" => ("DatabaseOutline", "#DCDCAA"),
            ".zip" or ".tar" or ".gz" or ".7z" or ".rar" => ("ZipBoxOutline", "#CE9178"),
            ".mp3" or ".wav" or ".ogg" or ".flac" => ("MusicNote", "#4EC9B0"),
            ".mp4" or ".mov" or ".avi" or ".mkv" or ".webm" => ("VideoOutline", "#4EC9B0"),
            ".txt" or ".log" or ".ini" or ".env" or ".config" => ("FileDocumentOutline", "#8B949E"),
            _ => ("FileOutline", "#8B949E")
        };
    }

    public string IconKind
    {
        get
        {
            if (IsDirectory)
            {
                return IsExpanded ? "FolderOpenOutline" : "FolderOutline";
            }

            if (LanguageIconKind != null) return LanguageIconKind;
            return IconForExtension(FileExtension).IconKind;
        }
    }

    public string IconColor
    {
        get
        {
            if (IsDirectory) return NotebookAmberHex;
            if (LanguageIconColor != null) return LanguageIconColor;
            return IconForExtension(FileExtension).IconColor;
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
    public Action<ExplorerItemViewModel>? OnCopyRelativePathRequested { get; set; }

    partial void OnDepthChanged(int value)
    {
        OnPropertyChanged(nameof(IndentPadding));
    }

    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(IconKind));
        OnPropertyChanged(nameof(ChevronKind));

        // Opening a folder that has not been listed yet lists it (its placeholder row shows meanwhile).
        if (value && IsDirectory && !ChildrenLoaded) LoadChildrenRequested?.Invoke(this);
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
                FileExtension = Path.GetExtension(trimmed) ?? string.Empty;
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
        // A .frycs/.frynb document lives in the studio's workspace; a source file may be the user's own project file.
        if (IsSourceFile && !IsConfirmingDelete)
        {
            OnPropertyChanged(nameof(DeleteConfirmationText));
            IsConfirmingDelete = true;
            return;
        }

        IsConfirmingDelete = false;
        OnDeleteRequested?.Invoke(this);
    }

    [RelayCommand]
    public void ConfirmDelete()
    {
        IsConfirmingDelete = true;
        RequestDelete();
    }

    [RelayCommand]
    public void CancelDelete() => IsConfirmingDelete = false;

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

    [RelayCommand]
    public void RequestCopyRelativePath()
    {
        OnCopyRelativePathRequested?.Invoke(this);
    }
}
