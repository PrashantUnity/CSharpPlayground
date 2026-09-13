using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PdfEditorApp.Plugins.CSharpEditor.Models;

/// <summary>
/// Represents a single cell in a structured dump table.
/// </summary>
public class DumpTableCell
{
    public string DisplayText { get; set; } = string.Empty;
    public object? RawValue { get; set; }
    public bool IsNumeric { get; set; }
    public bool IsBoolean { get; set; }
    public bool IsNull { get; set; }

    public Avalonia.Media.TextAlignment Alignment => IsNumeric ? Avalonia.Media.TextAlignment.Right : Avalonia.Media.TextAlignment.Left;

    public override string ToString() => DisplayText;
}

/// <summary>
/// Represents a row in a structured dump table.
/// </summary>
public class DumpTableRow
{
    public int RowIndex { get; set; }
    public IReadOnlyList<DumpTableCell> Cells { get; set; } = Array.Empty<DumpTableCell>();

    public DumpTableRow() { }

    public DumpTableRow(int rowIndex, IReadOnlyList<DumpTableCell> cells)
    {
        RowIndex = rowIndex;
        Cells = cells;
    }
}

/// <summary>
/// Represents a column definition in a structured dump table.
/// </summary>
public class DumpTableColumn
{
    public string Header { get; set; } = string.Empty;
    public bool IsNumeric { get; set; }
    public string HeaderDisplay => $"{Header} ≡";
}

/// <summary>
/// Represents a complete structured tabular result produced by .Dump().
/// Features collapsible header banner (e.g. ▲ Int32[9] •••), column headers (Item ≡),
/// structured grid rows with right-aligned numbers, and clipboard export helpers.
/// </summary>
public partial class DumpTableResult : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string? _label;

    [ObservableProperty]
    private bool _isCollapsed;

    public ObservableCollection<DumpTableColumn> Columns { get; } = new();
    public ObservableCollection<DumpTableRow> Rows { get; } = new();

    public int TotalCount => Rows.Count;

    public string CollapseIcon => IsCollapsed ? "▼" : "▲";

    public string FullHeaderTitle
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Label))
            {
                return $"{Label}: {Title}";
            }
            return Title;
        }
    }

    public string SummaryText => IsCollapsed ? $"({TotalCount} items - collapsed)" : $"({TotalCount} items)";

    public DumpTableResult() { }

    public DumpTableResult(string title, string? label = null)
    {
        _title = title;
        _label = label;
    }

    partial void OnIsCollapsedChanged(bool value)
    {
        OnPropertyChanged(nameof(CollapseIcon));
        OnPropertyChanged(nameof(SummaryText));
    }

    [RelayCommand]
    public void ToggleCollapse()
    {
        IsCollapsed = !IsCollapsed;
    }

    /// <summary>
    /// Exports the table as Tab-Separated Values (TSV) for direct pasting into Excel or Google Sheets.
    /// </summary>
    public string ToTsv()
    {
        var sb = new StringBuilder();

        // Header row
        for (int i = 0; i < Columns.Count; i++)
        {
            if (i > 0) sb.Append('\t');
            sb.Append(Columns[i].Header);
        }
        sb.AppendLine();

        // Data rows
        foreach (var row in Rows)
        {
            for (int i = 0; i < row.Cells.Count; i++)
            {
                if (i > 0) sb.Append('\t');
                var text = row.Cells[i].DisplayText;
                // Escape tabs and newlines if present
                text = text.Replace("\t", " ").Replace("\r\n", " ").Replace("\n", " ");
                sb.Append(text);
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Exports the table as standard Comma-Separated Values (CSV).
    /// </summary>
    public string ToCsv()
    {
        var sb = new StringBuilder();

        // Header row
        for (int i = 0; i < Columns.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(EscapeCsv(Columns[i].Header));
        }
        sb.AppendLine();

        // Data rows
        foreach (var row in Rows)
        {
            for (int i = 0; i < row.Cells.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(EscapeCsv(row.Cells[i].DisplayText));
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Exports the table as formatted JSON array of objects.
    /// </summary>
    public string ToJson()
    {
        var list = new List<Dictionary<string, object?>>();

        foreach (var row in Rows)
        {
            var dict = new Dictionary<string, object?>();
            for (int i = 0; i < Columns.Count; i++)
            {
                var colName = Columns[i].Header;
                var val = (i < row.Cells.Count) ? row.Cells[i].RawValue ?? row.Cells[i].DisplayText : null;
                dict[colName] = val;
            }
            list.Add(dict);
        }

        return JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    [RelayCommand]
    public async Task CopyTsvAsync()
    {
        await SetClipboardTextAsync(ToTsv());
    }

    [RelayCommand]
    public async Task CopyCsvAsync()
    {
        await SetClipboardTextAsync(ToCsv());
    }

    [RelayCommand]
    public async Task CopyJsonAsync()
    {
        await SetClipboardTextAsync(ToJson());
    }

    private static async Task SetClipboardTextAsync(string text)
    {
        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow != null)
            {
                var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(desktop.MainWindow);
                if (topLevel?.Clipboard != null)
                {
                    await topLevel.Clipboard.SetTextAsync(text);
                }
            }
        }
        catch { }
    }
}
