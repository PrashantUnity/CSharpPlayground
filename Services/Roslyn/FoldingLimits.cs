using System.Collections.Generic;
using System.Linq;
using AvaloniaEdit.Folding;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

/// <summary>
/// Caps how many foldings a document gets. Two costs of the editor's folding grow with their number: for every visual line it
/// builds it walks every folding after that line to find the next folded one, and every edit inside a folding redraws that
/// folding's whole range. A generated file with 18,000 blocks made a keystroke cost about 12 ms with them and 1 ms without.
/// Like VS Code (<c>editor.foldingMaximumRegions</c>), the outermost blocks are kept and the deepest ones dropped first.
/// </summary>
public static class FoldingLimits
{
    public const int MaxFoldings = 2_000;

    /// <summary>The foldings to install: all of them up to the limit, otherwise the outermost ones (earliest first), sorted by start.</summary>
    public static IEnumerable<NewFolding> Cap(IEnumerable<NewFolding> foldings, int limit = MaxFoldings)
    {
        var all = foldings as IReadOnlyList<NewFolding> ?? foldings.ToList();
        if (all.Count <= limit) return all;

        var ordered = all.OrderBy(f => f.StartOffset).ThenByDescending(f => f.EndOffset).ToList();
        var depth = new int[ordered.Count];
        var openEnds = new Stack<int>();
        for (int i = 0; i < ordered.Count; i++)
        {
            while (openEnds.Count > 0 && openEnds.Peek() <= ordered[i].StartOffset) openEnds.Pop();
            depth[i] = openEnds.Count;
            openEnds.Push(ordered[i].EndOffset);
        }

        return Enumerable.Range(0, ordered.Count)
            .OrderBy(i => depth[i]).ThenBy(i => i)
            .Take(limit)
            .OrderBy(i => i)
            .Select(i => ordered[i])
            .ToList();
    }
}
