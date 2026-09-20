using System;
using System.Text;
using Avalonia;
using Avalonia.Input.Platform;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

public static class VisualizerExportService
{
    public static string ToCsv(GridMatrixData grid)
    {
        var sb = new StringBuilder();
        for (int r = 0; r < grid.Rows; r++)
        {
            for (int c = 0; c < grid.Columns; c++)
            {
                if (c > 0) sb.Append(',');
                string val = grid[r, c].DisplayValue;
                if (val.Contains(',') || val.Contains('"'))
                {
                    sb.Append('"').Append(val.Replace("\"", "\"\"")).Append('"');
                }
                else
                {
                    sb.Append(val);
                }
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public static string ToTsv(GridMatrixData grid)
    {
        var sb = new StringBuilder();
        for (int r = 0; r < grid.Rows; r++)
        {
            for (int c = 0; c < grid.Columns; c++)
            {
                if (c > 0) sb.Append('\t');
                sb.Append(grid[r, c].DisplayValue);
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public static string GenerateIslandsReport(GridMatrixData grid)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Island Analysis Report");
        sb.AppendLine($"Grid Size: {grid.Rows} × {grid.Columns}");
        sb.AppendLine($"Total Islands Found: {grid.Islands.Count}");
        sb.AppendLine();
        sb.AppendLine("| Island | Area (cells) | Perimeter | Bounding Box | Color |");
        sb.AppendLine("|---|---|---|---|---|");

        foreach (var island in grid.Islands)
        {
            var bb = island.BoundingBox;
            sb.AppendLine($"| {island.Label} | {island.Area} | {island.Perimeter} | ({bb.MinRow},{bb.MinCol}) to ({bb.MaxRow},{bb.MaxCol}) | `{island.Color}` |");
        }

        return sb.ToString();
    }

    public static async void CopyToClipboard(string text)
    {
        try
        {
            var topLevel = Application.Current?.ApplicationLifetime switch
            {
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop => desktop.MainWindow,
                _ => null
            };

            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(text);
            }
        }
        catch
        {
            // Headless unit tests or clipboard access limitation
        }
    }
}
