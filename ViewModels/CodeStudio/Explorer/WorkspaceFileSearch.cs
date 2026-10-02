using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Workspace;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio.Explorer;

/// <summary>
/// "Go to File" over the whole workspace: turns what the workspace index finds into Quick Open results that open the file when
/// chosen. Quick Open on its own only knows the tabs that are already open.
/// </summary>
public sealed class WorkspaceFileSearch
{
    private const int MaxResults = 30;

    private readonly Func<WorkspaceFileIndex> _index;
    private readonly Func<string> _root;
    private readonly Func<string, Task> _open;

    /// <param name="index">The index to search (asking for it may start its background walk).</param>
    /// <param name="root">The workspace folder the index's relative paths are relative to.</param>
    /// <param name="open">Opens a file, given its full path.</param>
    public WorkspaceFileSearch(Func<WorkspaceFileIndex> index, Func<string> root, Func<string, Task> open)
    {
        _index = index;
        _root = root;
        _open = open;
    }

    public IReadOnlyList<QuickOpenItem> Search(string query)
    {
        var index = _index();
        var matches = index.Find(query, MaxResults);
        if (matches.Count == 0) return [];

        var root = _root();
        var category = index.IsBuilding ? "Files (still indexing)" : "Files";
        var items = new List<QuickOpenItem>(matches.Count);
        foreach (var match in matches)
        {
            var folder = match.RelativePath.Length > match.Name.Length ? match.RelativePath[..^(match.Name.Length + 1)] : string.Empty;
            var fullPath = Path.Combine(root, match.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            items.Add(new QuickOpenItem
            {
                Title = match.Name,
                Subtitle = folder.Length == 0 ? "Workspace root" : folder,
                Category = category,
                IconKind = "FileCodeOutline",
                IconColorHex = "#8B949E",
                Kind = QuickOpenItemKind.Document,
                Tag = fullPath,
                ExecuteAsyncAction = () => _open(fullPath)
            });
        }

        return items;
    }
}
