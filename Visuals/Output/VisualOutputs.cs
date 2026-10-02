using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Output;

/// <summary>
/// The output for a visual, whatever produced it: a spec from C#, JSON from another program's display bundle, or one of
/// the C# models that describe a visual. A spec that can't be drawn becomes an error output that says why.
/// </summary>
public static class VisualOutputs
{
    public static CellOutputKind KindOf(VisualFamily family) => family switch
    {
        VisualFamily.Chart => CellOutputKind.Chart,
        VisualFamily.Plot3D => CellOutputKind.Plot3D,
        _ => CellOutputKind.Visualizer
    };

    /// <param name="origin">The C# model the spec was made from, for a <see cref="VisualOriginCapture"/> (never kept otherwise).</param>
    public static RichCellOutput FromSpec(VisualSpec spec, string? displayId = null, object? origin = null)
    {
        var issues = VisualSpecValidator.Validate(spec);
        if (issues.Count > 0) return Error(new VisualSpecException(spec.Family, string.Join("; ", issues)).Message);

        var visual = VisualOutput.Create(spec, displayId);
        if (origin != null) VisualOriginCapture.Record(visual, origin);
        return new RichCellOutput { Kind = KindOf(spec.Family), Visual = visual };
    }

    /// <summary>
    /// The output for a spec another program sent (at most <see cref="VisualLimits.MaxPayloadBytes"/>); its JSON is kept
    /// as the saved form, not written out again.
    /// </summary>
    public static RichCellOutput FromJson(VisualFamily family, JsonElement json, string? displayId = null)
    {
        var size = JsonMarshal.GetRawUtf8Value(json).Length;
        if (size > VisualLimits.MaxPayloadBytes)
        {
            return Error(new VisualSpecException(family, string.Create(CultureInfo.InvariantCulture,
                $"at {size / Megabyte:0.#} MB it is larger than the {VisualLimits.MaxPayloadBytes / Megabyte:0} MB one visual can be; send less data, or a sample of it")).Message);
        }

        try
        {
            var spec = VisualJson.Deserialize(family, json);
            var issues = VisualSpecValidator.Validate(spec);
            if (issues.Count > 0) return Error(new VisualSpecException(family, string.Join("; ", issues)).Message);
            return new RichCellOutput { Kind = KindOf(family), Visual = VisualOutput.FromJson(spec, json, size, displayId) };
        }
        catch (VisualSpecException ex)
        {
            return Error(ex.Message);
        }
    }

    private const double Megabyte = 1024 * 1024;

    /// <summary>A value the C# side can show as a visual: a spec, anything that describes itself as one, or an older model.</summary>
    public static bool TryFromModel(object? value, [NotNullWhen(true)] out RichCellOutput? output)
    {
        VisualSpec? spec = value switch
        {
            VisualSpec s => s,
            IVisualSource source => source.ToVisualSpec(),
            ChartOptions chart => ChartOptionsConverter.ToSpec(chart),
            Plot3DOptions plot => Plot3DOptionsConverter.ToSpec(plot),
            VisualizerOptions visualizer => VisualizerOptionsConverter.ToSpec(visualizer),
            _ => null
        };

        output = spec == null ? null : FromSpec(spec, origin: value);
        return output != null;
    }

    public static RichCellOutput Error(string message) => new() { Kind = CellOutputKind.Error, Text = message };
}
