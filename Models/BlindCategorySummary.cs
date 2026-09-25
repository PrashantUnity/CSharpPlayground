using CommunityToolkit.Mvvm.ComponentModel;
using Material.Icons;

namespace PdfEditorApp.Plugins.CSharpEditor.Models;

public partial class BlindCategorySummary : ObservableObject
{
    public string Name { get; init; } = string.Empty;
    public MaterialIconKind IconKind { get; init; } = MaterialIconKind.FolderOutline;
    public int TotalCount { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BadgeText), nameof(ProgressPercentage), nameof(ToolTipText))]
    private int _solvedCount;

    [ObservableProperty]
    private bool _isSelected;

    public string BadgeText => SolvedCount > 0 ? $"{SolvedCount}/{TotalCount}" : $"{TotalCount}";
    public double ProgressPercentage => TotalCount > 0 ? (double)SolvedCount / TotalCount * 100.0 : 0.0;
    public string ToolTipText => $"{Name}: {SolvedCount} of {TotalCount} solved";
}
