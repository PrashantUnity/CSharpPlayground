namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

// A divider row the Manager's workspace list injects above documents that share one external
// .frynbproj/.frycsproj project file, so the list reads as "workspace, then its documents" instead of
// a flat pile of unrelated-looking files. Purely a label — the real WorkspaceItemSummary rows beneath
// it are still individually visible and clickable exactly as before; nothing is hidden behind this.
public class WorkspaceGroupHeaderViewModel
{
    public string Title { get; init; } = string.Empty;
    public string FolderPath { get; init; } = string.Empty;
    public int ItemCount { get; init; }
    public string SubtitleText => ItemCount == 1 ? "1 item · External workspace" : $"{ItemCount} items · External workspace";
}
