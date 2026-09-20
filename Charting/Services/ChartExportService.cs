using System;
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
    public static string ToCsv(ChartOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Series,Index,Label,X,Y");

        foreach (var s in options.Series)
        {
            for (int i = 0; i < s.Points.Count; i++)
            {
                var p = s.Points[i];
                var cleanLabel = (p.Label ?? string.Empty).Replace("\"", "\"\"");
                sb.AppendLine($"\"{s.Name}\",{i},\"{cleanLabel}\",{p.X},{p.Y}");
            }
        }

        return sb.ToString();
    }

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
