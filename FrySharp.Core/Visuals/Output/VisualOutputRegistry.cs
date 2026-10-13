using System.Collections.Concurrent;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Output;

/// <summary>
/// The visuals one program showed, by display id, so a later <c>update_display</c> redraws the one it names. It holds them
/// weakly: an output cleared from its cell isn't kept alive for an update that may never come.
/// </summary>
public sealed class VisualOutputRegistry
{
    private readonly ConcurrentDictionary<string, WeakReference<VisualOutput>> _outputs = new(StringComparer.Ordinal);

    /// <summary>Keeps a displayed visual, when it has a display id, for later updates.</summary>
    public void Register(RichCellOutput output)
    {
        if (output.Visual is { } visual) _outputs[visual.DisplayId] = new WeakReference<VisualOutput>(visual);
    }

    public bool TryGet(string displayId, out VisualOutput output)
    {
        output = null!;
        if (!_outputs.TryGetValue(displayId, out var weak)) return false;
        if (weak.TryGetTarget(out var target))
        {
            output = target;
            return true;
        }

        _outputs.TryRemove(displayId, out _);
        return false;
    }

    /// <summary>
    /// An update from the program: the visual it names redraws with <paramref name="update"/>'s spec. Returns what else
    /// to show, if anything: an error when the new spec can't be drawn, or the update itself when no visual has that id
    /// (a program may update what another cell showed, or what it never showed), as a new output.
    /// </summary>
    public RichCellOutput? Apply(string? displayId, RichCellOutput update)
    {
        if (update.Visual is not { } visual) return update;
        if (displayId == null || !TryGet(displayId, out var shown)) return update;
        if (shown.Family != visual.Family)
        {
            return VisualOutputs.Error($"Display {displayId} is a {shown.Family} visual; an update can't make it a {visual.Family}.");
        }

        shown.Replace(visual.Spec);
        return null;
    }
}
