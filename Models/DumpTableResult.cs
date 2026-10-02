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

    /// <summary>
    /// If this cell contains nested tabular data (e.g. JSON array of objects, child table,
    /// sub-collection, or key-value dictionary), this holds the structured DumpTableResult.
    /// </summary>
    public DumpTableResult? NestedTable { get; set; }

    public bool HasNestedTable => NestedTable != null;

    /// <summary>
    /// Whether the nested table is currently expanded inline beneath this row.
    /// </summary>
    public bool IsNestedExpanded { get; set; }

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

    public bool HasNestedTable => Cells.Any(c => c.HasNestedTable);
    public bool IsExpanded => Cells.Any(c => c.IsNestedExpanded);

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

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private int? _sortColumnIndex;

    [ObservableProperty]
    private bool _isSortDescending;

    [ObservableProperty]
    private int _selectedRowIndex = -1;

    public ObservableCollection<DumpTableColumn> Columns { get; } = new();
    public ObservableCollection<DumpTableRow> Rows { get; } = new();
    public ObservableCollection<DumpTableRow> FilteredRows { get; } = new();

    public event Action? RowsViewChanged;

    public int TotalCount => Rows.Count;

    public bool IsFiltered => !string.IsNullOrWhiteSpace(FilterText);
    public bool IsSorted => SortColumnIndex.HasValue;

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

    public string SummaryText
    {
        get
        {
            if (IsCollapsed) return $"({TotalCount} items - collapsed)";
            if (IsFiltered) return $"({FilteredRows.Count} of {TotalCount} rows)";
            return $"({TotalCount} items)";
        }
    }

    public DumpTableResult()
    {
        Rows.CollectionChanged += (s, e) => ApplyFilterAndSort();
    }

    public DumpTableResult(string title, string? label = null) : this()
    {
        _title = title;
        _label = label;
    }

    partial void OnIsCollapsedChanged(bool value)
    {
        OnPropertyChanged(nameof(CollapseIcon));
        OnPropertyChanged(nameof(SummaryText));
    }

    partial void OnFilterTextChanged(string value)
    {
        ApplyFilterAndSort();
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(IsFiltered));
    }

    partial void OnSortColumnIndexChanged(int? value)
    {
        ApplyFilterAndSort();
        OnPropertyChanged(nameof(IsSorted));
        OnPropertyChanged(nameof(SummaryText));
    }

    partial void OnIsSortDescendingChanged(bool value)
    {
        ApplyFilterAndSort();
    }

    [RelayCommand]
    public void ToggleCollapse()
    {
        IsCollapsed = !IsCollapsed;
    }

    [RelayCommand]
    public void SortByColumn(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= Columns.Count) return;

        if (SortColumnIndex != columnIndex)
        {
            SortColumnIndex = columnIndex;
            IsSortDescending = false;
        }
        else if (!IsSortDescending)
        {
            IsSortDescending = true;
        }
        else
        {
            SortColumnIndex = null;
            IsSortDescending = false;
        }
    }

    [RelayCommand]
    public void ClearFilter()
    {
        FilterText = string.Empty;
    }

    [RelayCommand]
    public void ClearSort()
    {
        SortColumnIndex = null;
        IsSortDescending = false;
    }

    [RelayCommand]
    public void ResetAll()
    {
        FilterText = string.Empty;
        SortColumnIndex = null;
        IsSortDescending = false;
        SelectedRowIndex = -1;
    }

    public void ApplyFilterAndSort()
    {
        IEnumerable<DumpTableRow> rows = Rows;

        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            var filter = FilterText.Trim();
            rows = rows.Where(r => r.Cells.Any(c =>
                c.DisplayText != null &&
                c.DisplayText.Contains(filter, StringComparison.OrdinalIgnoreCase)));
        }

        if (SortColumnIndex.HasValue && SortColumnIndex.Value >= 0 && SortColumnIndex.Value < Columns.Count)
        {
            int col = SortColumnIndex.Value;
            bool isDesc = IsSortDescending;
            bool colNumeric = Columns[col].IsNumeric;

            rows = isDesc
                ? rows.OrderByDescending(r => GetSortKey(r, col, colNumeric), RowSortComparer.Instance)
                : rows.OrderBy(r => GetSortKey(r, col, colNumeric), RowSortComparer.Instance);
        }

        FilteredRows.Clear();
        foreach (var r in rows)
        {
            FilteredRows.Add(r);
        }

        RowsViewChanged?.Invoke();
    }

    private static object? GetSortKey(DumpTableRow row, int colIndex, bool colNumeric)
    {
        if (colIndex >= row.Cells.Count) return null;
        var cell = row.Cells[colIndex];
        if (cell.IsNull || string.IsNullOrEmpty(cell.DisplayText)) return null;

        if (cell.RawValue != null)
        {
            if (cell.RawValue is long or int or short or byte) return Convert.ToInt64(cell.RawValue);
            if (cell.RawValue is double or float or decimal) return Convert.ToDouble(cell.RawValue);
            if (cell.RawValue is bool b) return b ? 1 : 0;
            if (cell.RawValue is DateTime dt) return dt;
        }

        if (colNumeric || cell.IsNumeric)
        {
            if (double.TryParse(cell.DisplayText, System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands, System.Globalization.CultureInfo.InvariantCulture, out var d))
                return d;
        }

        return cell.DisplayText;
    }

    private sealed class RowSortComparer : IComparer<object?>
    {
        public static readonly RowSortComparer Instance = new();

        public int Compare(object? x, object? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return 1;
            if (y == null) return -1;

            if (x is double dx && y is double dy) return dx.CompareTo(dy);
            if (x is long lx && y is long ly) return lx.CompareTo(ly);
            if (x is IComparable cx && y is IComparable cy && x.GetType() == y.GetType())
            {
                return cx.CompareTo(cy);
            }

            return string.Compare(x.ToString(), y.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }

    private IEnumerable<DumpTableRow> CurrentEffectiveRows =>
        (FilteredRows.Count > 0 || IsFiltered) ? FilteredRows : Rows;

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
        foreach (var row in CurrentEffectiveRows)
        {
            for (int i = 0; i < row.Cells.Count; i++)
            {
                if (i > 0) sb.Append('\t');
                var text = row.Cells[i].DisplayText;
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
        foreach (var row in CurrentEffectiveRows)
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

        foreach (var row in CurrentEffectiveRows)
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

    /// <summary>
    /// Exports the table as standard GitHub Flavored Markdown table.
    /// </summary>
    public string ToMarkdown()
    {
        var sb = new StringBuilder();

        sb.Append("| ");
        for (int i = 0; i < Columns.Count; i++)
        {
            if (i > 0) sb.Append(" | ");
            sb.Append(Columns[i].Header);
        }
        sb.AppendLine(" |");

        sb.Append("|");
        for (int i = 0; i < Columns.Count; i++)
        {
            sb.Append(Columns[i].IsNumeric ? " ---: |" : " --- |");
        }
        sb.AppendLine();

        foreach (var row in CurrentEffectiveRows)
        {
            sb.Append("| ");
            for (int i = 0; i < row.Cells.Count; i++)
            {
                if (i > 0) sb.Append(" | ");
                sb.Append(row.Cells[i].DisplayText.Replace("|", "\\|"));
            }
            sb.AppendLine(" |");
        }

        return sb.ToString();
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

    [RelayCommand]
    public async Task CopyMarkdownAsync()
    {
        await SetClipboardTextAsync(ToMarkdown());
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
