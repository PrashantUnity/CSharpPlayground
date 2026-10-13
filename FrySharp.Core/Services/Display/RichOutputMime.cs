using System.Text.Json.Nodes;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

/// <summary>
/// A display as a MIME bundle, the way kernels send them (the reverse of <see cref="Kernels.MimeOutputMapper"/>): what a
/// host without the desktop's controls hands to its own renderer. A table is <see cref="VisualMimeTypes.Table"/>, a chart,
/// 3D plot or visualizer its spec, an image base64. Every bundle also has <c>text/plain</c>, so any client can show
/// something. A live desktop control has no bundle.
/// </summary>
public static class RichOutputMime
{
    /// <summary>Rows of a table sent at most; the bundle says how many there are.</summary>
    public const int MaxTableRows = 1000;

    /// <summary>An object inspector's tree: <c>{title, properties: [{name, value, isNull, isString, child}]}</c>.</summary>
    public const string InspectorMime = "application/vnd.fry.inspector+json";

    // Levels of an inspector tree sent: deeper objects are listed by their value text only.
    private const int MaxInspectorDepth = 6;

    /// <param name="output">The display.</param>
    /// <param name="metadata">Per-type metadata, e.g. an image's size: <c>{"image/png": {"width": 640, "height": 480}}</c>.</param>
    /// <returns>The bundle, or null for a display that only exists in the desktop's UI.</returns>
    public static JsonObject? ToBundle(RichCellOutput output, out JsonObject metadata)
    {
        ArgumentNullException.ThrowIfNull(output);
        metadata = new JsonObject();

        if (output.Visual is { } visual && visual.SavedJson is { } spec)
        {
            var bundle = new JsonObject();
            if (VisualSvgRenderer.TryRender(visual.Spec, out var html))
            {
                bundle["text/html"] = html;
            }
            bundle[visual.MimeType] = JsonNode.Parse(spec.GetRawText());
            bundle["text/plain"] = $"<{visual.Family}>";
            return bundle;
        }

        switch (output.Kind)
        {
            case CellOutputKind.Image when output.ImageBytes is { Length: > 0 } bytes:
            {
                var mime = (output.ImageFormat ?? "PNG").ToUpperInvariant() switch
                {
                    "JPEG" or "JPG" => "image/jpeg",
                    "GIF" => "image/gif",
                    "SVG" => "image/svg+xml",
                    _ => "image/png"
                };
                if (output.ImageWidth is { } width && output.ImageHeight is { } height)
                {
                    metadata[mime] = new JsonObject { ["width"] = width, ["height"] = height };
                }
                var base64 = Convert.ToBase64String(bytes);
                var html = $$"""
                <div class="fry-img-card" style="margin:8px 0;max-width:840px;">
                  <img src="data:{{mime}};base64,{{base64}}" style="max-width:100%;height:auto;border-radius:6px;border:1px solid #30363d;box-shadow:0 4px 16px rgba(0,0,0,0.35);display:block;" alt="Synthesized Image" />
                </div>
                """;
                return new JsonObject
                {
                    ["text/html"] = html,
                    [mime] = base64,
                    ["text/plain"] = "<image>"
                };
            }

            case CellOutputKind.Html when output.MarkdownSource is { } markdown:
                return new JsonObject { ["text/markdown"] = markdown, ["text/plain"] = markdown };

            case CellOutputKind.Html when output.HtmlContent is { Length: > 0 } html:
                return new JsonObject { ["text/html"] = html, ["text/plain"] = "<HTML>" };

            case CellOutputKind.Table when output.TableResult is { } table:
                return new JsonObject
                {
                    ["text/html"] = InteractiveTableHtmlRenderer.Render(table),
                    [VisualMimeTypes.Table] = Table(table),
                    ["text/plain"] = table.FullHeaderTitle
                };

            case CellOutputKind.ObjectInspector when output.InspectorNode is { } node:
                return new JsonObject
                {
                    ["text/html"] = InteractiveTableHtmlRenderer.RenderInspector(node),
                    [InspectorMime] = Inspector(node, 0),
                    ["text/plain"] = InspectorText(node)
                };

            case CellOutputKind.Text or CellOutputKind.Error when output.Text is { Length: > 0 } text:
                return new JsonObject { ["text/plain"] = text };

            default:
                return null;
        }
    }

    // {title, columns, numeric, rows, totalRows, totalColumns}: what the Python and JavaScript runtimes send too.
    private static JsonObject Table(DumpTableResult table)
    {
        var columns = new JsonArray();
        var numeric = new JsonArray();
        foreach (var column in table.Columns)
        {
            columns.Add(column.Header);
            numeric.Add(column.IsNumeric);
        }

        var rows = new JsonArray();
        foreach (var row in table.Rows.Take(MaxTableRows))
        {
            var cells = new JsonArray();
            foreach (var cell in row.Cells) cells.Add(cell.IsNull ? null : cell.DisplayText);
            rows.Add(cells);
        }

        return new JsonObject
        {
            ["title"] = table.Title,
            ["columns"] = columns,
            ["numeric"] = numeric,
            ["rows"] = rows,
            ["totalRows"] = table.Rows.Count,
            ["totalColumns"] = table.Columns.Count,
        };
    }

    private static string TableHtml(DumpTableResult table) => InteractiveTableHtmlRenderer.Render(table);

    private static JsonObject Inspector(ObjectInspectorNode node, int depth)
    {
        var properties = new JsonArray();
        foreach (var row in node.Properties)
        {
            properties.Add(new JsonObject
            {
                ["name"] = row.Name,
                ["value"] = row.SimpleValueText,
                ["isNull"] = row.IsNull,
                ["isString"] = row.IsString,
                ["child"] = row.ChildNode is { } child && depth + 1 < MaxInspectorDepth ? Inspector(child, depth + 1) : null,
            });
        }

        return new JsonObject { ["title"] = node.HeaderTitle, ["properties"] = properties };
    }

    private static string InspectorText(ObjectInspectorNode node) =>
        node.HeaderTitle + "\n" + string.Join("\n", node.Properties.Select(p => $"  {p.Name} = {p.SimpleValueText}"));
}
