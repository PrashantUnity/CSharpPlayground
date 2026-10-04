using CommunityToolkit.Mvvm.ComponentModel;
using PdfEditorApp.Plugins.CSharpEditor.Models.Git;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio.Git;

public partial class GitFileChangeItemViewModel : ObservableObject
{
    public GitFileChange Model { get; }

    public string RelativePath => Model.RelativePath;
    public string FileName => Model.FileName;
    public string DirectoryDisplay => Model.DirectoryPath;
    public GitFileStatusKind StatusKind => Model.StatusKind;
    public bool IsStaged => Model.IsStaged;
    public string? OldRelativePath => Model.OldRelativePath;

    public string StatusLetter => Model.StatusLetter;
    public string TooltipText => Model.StatusTooltip;

    public string StatusColorHex => StatusKind switch
    {
        GitFileStatusKind.Modified => "#F59E0B",   // Orange / Amber
        GitFileStatusKind.Added => "#22C55E",      // Green
        GitFileStatusKind.Untracked => "#22C55E",  // Green
        GitFileStatusKind.Renamed => "#06B6D4",    // Cyan
        GitFileStatusKind.Deleted => "#EF4444",    // Red
        GitFileStatusKind.Conflicted => "#A855F7", // Purple
        _ => "#94A3B8"
    };

    [ObservableProperty]
    private bool _isSelected;

    public GitFileChangeItemViewModel(GitFileChange model)
    {
        Model = model;
    }
}
