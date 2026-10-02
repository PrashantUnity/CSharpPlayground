using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Output;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;

/// <summary>A piece of a cell's output from a kernel: text, or a rich output (image, HTML, table).</summary>
public sealed record KernelOutput(string? Text, RichCellOutput? Rich);

/// <summary>
/// Turns a kernel's MIME bundle (Jupyter's way of saying "here is this value as PNG, as HTML and as text") into the
/// studio's outputs, choosing the richest kind it can show: the studio's table, a chart, 3D plot or visualizer spec,
/// PNG/JPEG, SVG, HTML, then text.
/// </summary>
public static class MimeOutputMapper
{
    public const string TableMime = VisualMimeTypes.Table;
    public const string Plot3DMime = VisualMimeTypes.LegacyPlot3D;

    /// <param name="displayId">The id the program gave the display, so a later update can find it.</param>
    public static KernelOutput Map(JsonElement data, JsonElement metadata, string? displayId = null)
    {
        if (data.ValueKind != JsonValueKind.Object) return new KernelOutput(null, null);

        if (data.TryGetProperty(TableMime, out var table) && table.ValueKind == JsonValueKind.Object)
        {
            return new KernelOutput(null, new RichCellOutput { Kind = CellOutputKind.Table, TableResult = Table(table) });
        }

        foreach (var mime in VisualMimeTypes.VisualTypes)
        {
            if (data.TryGetProperty(mime, out var spec) && VisualMimeTypes.TryGetFamily(mime, out var family))
            {
                return new KernelOutput(null, VisualOutputs.FromJson(family, spec, displayId));
            }
        }

        if (data.TryGetProperty(Plot3DMime, out var plot3d) && plot3d.ValueKind == JsonValueKind.Object)
        {
            return new KernelOutput(null, LegacyPlot3D(plot3d, displayId));
        }

        foreach (var (mime, format) in new[] { ("image/png", "PNG"), ("image/jpeg", "JPEG") })
        {
            if (!data.TryGetProperty(mime, out var encoded) || encoded.GetString() is not { Length: > 0 } base64) continue;
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(base64);
            }
            catch (FormatException)
            {
                continue;
            }

            var (width, height) = Size(metadata, mime);
            return new KernelOutput(null, new RichCellOutput
            {
                Kind = CellOutputKind.Image,
                ImageBytes = bytes,
                ImageFormat = format,
                ImageWidth = width,
                ImageHeight = height
            });
        }

        if (data.TryGetProperty("image/svg+xml", out var svg) && svg.GetString() is { Length: > 0 } svgText)
        {
            // Drawn by the HTML view's browser, which shows SVG natively.
            return new KernelOutput(null, new RichCellOutput
            {
                Kind = CellOutputKind.Html,
                HtmlContent = $"<!DOCTYPE html><html><body style=\"margin:0\">{svgText}</body></html>"
            });
        }

        if (data.TryGetProperty("text/html", out var html) && html.GetString() is { Length: > 0 } htmlText)
        {
            return new KernelOutput(null, new RichCellOutput { Kind = CellOutputKind.Html, HtmlContent = htmlText });
        }

        foreach (var mime in new[] { "text/markdown", "text/plain" })
        {
            if (data.TryGetProperty(mime, out var text) && text.GetString() is { } plain)
            {
                return new KernelOutput(plain.EndsWith('\n') ? plain : plain + "\n", null);
            }
        }

        return new KernelOutput(null, null);
    }

    // {"title", "columns": [..], "numeric": [..], "rows": [[..]], "totalRows", "totalColumns"}
    private static DumpTableResult Table(JsonElement table)
    {
        var title = table.TryGetProperty("title", out var t) ? t.GetString() ?? "Table" : "Table";
        var result = new DumpTableResult(title);
        var numeric = table.TryGetProperty("numeric", out var n) && n.ValueKind == JsonValueKind.Array
            ? n.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.True).ToList()
            : new List<bool>();

        if (table.TryGetProperty("columns", out var columns) && columns.ValueKind == JsonValueKind.Array)
        {
            var i = 0;
            foreach (var column in columns.EnumerateArray())
            {
                result.Columns.Add(new DumpTableColumn { Header = column.GetString() ?? string.Empty, IsNumeric = i < numeric.Count && numeric[i] });
                i++;
            }
        }

        if (table.TryGetProperty("rows", out var rows) && rows.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var row in rows.EnumerateArray())
            {
                var cells = row.ValueKind == JsonValueKind.Array
                    ? row.EnumerateArray().Select(Cell).ToList()
                    : new List<DumpTableCell>();
                result.Rows.Add(new DumpTableRow(index++, cells));
            }
        }

        var totalRows = table.TryGetProperty("totalRows", out var total) && total.TryGetInt32(out var count) ? count : result.Rows.Count;
        if (totalRows > result.Rows.Count)
        {
            result.Label = $"showing the first {result.Rows.Count:N0} of {totalRows:N0} rows";
        }

        return result;
    }

    private static DumpTableCell Cell(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => DumpTableBuilder.CreateCell(null),
        JsonValueKind.True => DumpTableBuilder.CreateCell(true),
        JsonValueKind.False => DumpTableBuilder.CreateCell(false),
        JsonValueKind.Number when value.TryGetInt64(out var whole) => DumpTableBuilder.CreateCell(whole),
        JsonValueKind.Number => DumpTableBuilder.CreateCell(value.GetDouble()),
        JsonValueKind.String => DumpTableBuilder.CreateCell(value.GetString()),
        _ => DumpTableBuilder.CreateCell(value.GetRawText())
    };

    private static (int? Width, int? Height) Size(JsonElement metadata, string mime)
    {
        if (metadata.ValueKind != JsonValueKind.Object || !metadata.TryGetProperty(mime, out var size) || size.ValueKind != JsonValueKind.Object)
        {
            return (null, null);
        }

        int? Read(string name) => size.TryGetProperty(name, out var v) && v.TryGetDouble(out var d) ? (int)Math.Round(d, MidpointRounding.AwayFromZero) : null;
        return (Read("width"), Read("height"));
    }

    // The first 3D format, {title, type, points: [[x, y, z] | {x, y, z, label}]}, read into the spec it always meant.
    private static RichCellOutput LegacyPlot3D(JsonElement plot, string? displayId)
    {
        var spec = new Plot3DSpec();
        if (plot.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String) spec.Title = title.GetString();
        if (plot.TryGetProperty("type", out var type) && type.GetString() is { } kind && Enum.TryParse<Plot3DType>(kind, true, out var parsed)) spec.Kind = parsed;
        if (!plot.TryGetProperty("points", out var points) || points.ValueKind != JsonValueKind.Array) return VisualOutputs.FromSpec(spec, displayId);

        var series = new Plot3DSeriesSpec { Name = spec.Title };
        var labelled = false;
        var index = 0;
        foreach (var point in points.EnumerateArray())
        {
            var (x, y, z, label) = point.ValueKind switch
            {
                JsonValueKind.Array when point.GetArrayLength() >= 3 => (point[0], point[1], point[2], (string?)null),
                JsonValueKind.Object => (Member(point, "x"), Member(point, "y"), Member(point, "z"),
                    point.TryGetProperty("label", out var l) && l.ValueKind == JsonValueKind.String ? l.GetString() : null),
                _ => (default, default, default, null)
            };

            if (!(x.ValueKind == JsonValueKind.Number && y.ValueKind == JsonValueKind.Number && z.ValueKind == JsonValueKind.Number))
            {
                return VisualOutputs.Error($"The 3D plot isn't valid: $.points[{index}] must be [x, y, z] or {{\"x\", \"y\", \"z\"}}, all numbers");
            }

            series.X.Add(x.GetDouble());
            series.Y.Add(y.GetDouble());
            series.Z.Add(z.GetDouble());
            (series.Labels ??= []).Add(label);
            labelled |= label != null;
            index++;
        }

        if (!labelled) series.Labels = null;
        if (series.X.Count > 0) spec.Series.Add(series);
        return VisualOutputs.FromSpec(spec, displayId);
    }

    private static JsonElement Member(JsonElement element, string name) => element.TryGetProperty(name, out var value) ? value : default;
}
