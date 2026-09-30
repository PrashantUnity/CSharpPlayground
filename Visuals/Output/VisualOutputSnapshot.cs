using System.Runtime.InteropServices;
using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Output;

/// <summary>
/// A visual as a notebook saves it: its MIME type and spec, as in a Jupyter output. One too large to save keeps its
/// title, and reopens as an empty frame that says how to redraw it.
/// </summary>
public sealed class VisualOutputSnapshot
{
    // A snapshot of a visual on show reads it when the notebook is saved: its latest spec (after any updates), written
    // out by then in the background rather than when it was shown.
    private VisualOutput? _live;
    private JsonElement? _data;
    private string? _title;

    public string MimeType { get; set; } = string.Empty;

    public JsonElement? Data
    {
        get => _live != null ? _live.SavedJson : _data;
        set
        {
            _data = value;
            _live = null;
        }
    }

    public string? Title
    {
        get => _live != null ? _live.Spec.Title : _title;
        set => _title = value;
    }

    /// <summary>What a notebook saves of a visual it shows.</summary>
    public static VisualOutputSnapshot From(VisualOutput output) => new() { MimeType = output.MimeType, _live = output };

    /// <summary>
    /// The visual again. One this version can't draw (too large to have been saved, saved by a newer version, or damaged)
    /// comes back as an empty frame with its title that says why; the snapshot itself stays in the notebook unchanged.
    /// Null only for a MIME type that isn't a visual's.
    /// </summary>
    public VisualOutput? ToOutput()
    {
        if (!VisualMimeTypes.TryParse(MimeType, out var family, out var version)) return null;
        if (version != VisualMimeTypes.CurrentVersion) return Unreadable(family, "Saved by a newer version of the studio: run the cell again to redraw it.");
        if (Data is not { } data) return Unreadable(family, "Too large to save with the notebook: run the cell again to redraw it.");

        try
        {
            var spec = VisualJson.Deserialize(family, data);
            var issues = VisualSpecValidator.Validate(spec);
            return issues.Count == 0
                ? VisualOutput.FromJson(spec, data, JsonMarshal.GetRawUtf8Value(data).Length)
                : Unreadable(family, Damaged(issues[0].ToString()));
        }
        catch (VisualSpecException ex)
        {
            return Unreadable(family, Damaged(ex.Problem));
        }
    }

    private VisualOutput Unreadable(VisualFamily family, string why) => VisualOutput.Create(Placeholder(family, Title, why));

    private static string Damaged(string problem) => $"This saved visual can't be read ({problem}): run the cell again to redraw it.";

    /// <summary>An empty visual of the family, titled, whose subtitle explains why it is empty.</summary>
    public static VisualSpec Placeholder(VisualFamily family, string? title, string why) => family switch
    {
        VisualFamily.Chart => new ChartSpec { Title = title, Subtitle = why },
        VisualFamily.Plot3D => new Plot3DSpec { Title = title, Subtitle = why },
        _ => new VisualizerSpec { Title = title, Subtitle = why, Kind = VisualizerKind.ArrayPointers, State = { Array = new ArrayState() } }
    };
}
