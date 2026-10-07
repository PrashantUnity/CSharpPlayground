using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Services;

public static class ChartExportService
{
    /// <summary>Every value as CSV (RFC 4180): one row per value, numbers as any program reads them, a gap as an empty field.</summary>
    public static string ToCsv(ChartOptions options)
    {
        var sb = new StringBuilder();

        // Columns only a chart that uses them has: where floating bars start, and the sizes of bubbles.
        var hasFrom = options.Series.Any(s => s.Points.Any(p => p.From != null));
        var hasSize = options.Series.Any(s => s.Points.Any(p => p.Size != null));
        sb.Append("Series,Index,Label,X,Y");
        if (hasFrom) sb.Append(",From");
        if (hasSize) sb.Append(",Size");
        sb.AppendLine();

        foreach (var s in options.Series)
        {
            for (int i = 0; i < s.Points.Count; i++)
            {
                var p = s.Points[i];
                sb.Append(Quoted(s.Name)).Append(',')
                  .Append(i.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(Quoted(p.Label)).Append(',')
                  .Append(Number(p.X)).Append(',')
                  .Append(Number(p.Y));
                if (hasFrom) sb.Append(',').Append(p.From is { } from ? Number(from) : string.Empty);
                if (hasSize) sb.Append(',').Append(p.Size is { } size ? Number(size) : string.Empty);
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    // A field in quotes, its own quotes doubled.
    private static string Quoted(string? text) => $"\"{(text ?? string.Empty).Replace("\"", "\"\"")}\"";

    // With a decimal point whatever the locale (a comma would split the field); a missing value is left empty.
    private static string Number(double value) => double.IsFinite(value) ? value.ToString("R", CultureInfo.InvariantCulture) : string.Empty;

    public static async Task CopyCsvToClipboardAsync(ChartOptions options)
    {
        var topLevel = Application.Current?.ApplicationLifetime switch
        {
            IClassicDesktopStyleApplicationLifetime desktop => desktop.MainWindow,
            _ => null
        };

        if (topLevel?.Clipboard != null)
        {
            var csv = ToCsv(options);
            await topLevel.Clipboard.SetTextAsync(csv);
        }
    }

    public static async Task<string> SaveCsvToFileAsync(ChartOptions options, string? directory = null)
    {
        var targetDir = directory;
        if (string.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir))
        {
            targetDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        var fileName = $"chart_{options.Title.Replace(' ', '_')}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        var fullPath = Path.Combine(targetDir, fileName);

        var csv = ToCsv(options);
        await File.WriteAllTextAsync(fullPath, csv, Encoding.UTF8);
        return fullPath;
    }
}
