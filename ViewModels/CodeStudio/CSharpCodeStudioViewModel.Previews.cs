using System.Globalization;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio;

public partial class CSharpCodeStudioViewModel
{
    private static readonly HashSet<string> MarkdownExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".markdown", ".mdown", ".mkd"
    };

    private static readonly HashSet<string> CsvExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csv", ".tsv"
    };

    public static bool IsMarkdownExtension(string? ext) =>
        !string.IsNullOrEmpty(ext) && MarkdownExtensions.Contains(ext);

    public static bool IsCsvExtension(string? ext) =>
        !string.IsNullOrEmpty(ext) && CsvExtensions.Contains(ext);

    public bool IsActiveDocumentMarkdown
    {
        get
        {
            var path = Script?.SourceFilePath ?? Script?.Title;
            return IsMarkdownExtension(Path.GetExtension(path));
        }
    }

    public bool IsActiveDocumentCsv
    {
        get
        {
            var path = Script?.SourceFilePath ?? Script?.Title;
            return IsCsvExtension(Path.GetExtension(path));
        }
    }

    public bool HasPreviewMode => IsActiveDocumentMarkdown || IsActiveDocumentCsv;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMarkdownPreview))]
    [NotifyPropertyChangedFor(nameof(ShowCsvPreview))]
    [NotifyPropertyChangedFor(nameof(ShowTextEditor))]
    [NotifyPropertyChangedFor(nameof(DocumentPreviewToggleIconKind))]
    [NotifyPropertyChangedFor(nameof(DocumentPreviewToggleText))]
    [NotifyPropertyChangedFor(nameof(DocumentPreviewModeTooltip))]
    [NotifyPropertyChangedFor(nameof(RuntimeLabel))]
    private bool _isDocumentPreviewMode;

    public bool ShowMarkdownPreview => IsActiveDocumentMarkdown && IsDocumentPreviewMode;

    public bool ShowCsvPreview => IsActiveDocumentCsv && IsDocumentPreviewMode;

    public bool ShowTextEditor => !IsActiveDocumentImage && !ShowMarkdownPreview && !ShowCsvPreview && !ShowDiffViewer;

    [ObservableProperty]
    private DumpTableResult? _activeCsvTable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RuntimeLabel))]
    private string _csvDimensionsSummary = string.Empty;

    [ObservableProperty]
    private string _csvDelimiterSummary = "Comma (,)";

    public string DocumentPreviewToggleIconKind
    {
        get
        {
            if (IsDocumentPreviewMode) return "CodeTags";
            if (IsActiveDocumentMarkdown) return "BookOpenPageVariantOutline";
            if (IsActiveDocumentCsv) return "TableEye";
            return "EyeOutline";
        }
    }

    public string DocumentPreviewToggleText
    {
        get
        {
            if (IsDocumentPreviewMode) return IsActiveDocumentCsv ? "Edit Raw CSV" : "Edit Raw";
            if (IsActiveDocumentMarkdown) return "Preview";
            if (IsActiveDocumentCsv) return "Preview Table";
            return "Preview";
        }
    }

    public string DocumentPreviewModeTooltip
    {
        get
        {
            if (IsDocumentPreviewMode) return "Switch to Source Text Editor";
            if (IsActiveDocumentMarkdown) return "Open Markdown Preview (Ctrl+Shift+V)";
            if (IsActiveDocumentCsv) return "Open Interactive Data Table Preview (Ctrl+Shift+V)";
            return "Toggle Preview Mode";
        }
    }

    [RelayCommand]
    public void ToggleDocumentPreviewMode()
    {
        if (!HasPreviewMode) return;
        IsDocumentPreviewMode = !IsDocumentPreviewMode;

        if (IsDocumentPreviewMode)
        {
            RefreshActiveDocumentPreview();
        }

        var activeTab = OpenTabs.FirstOrDefault(t => t.Id == Script.Id);
        if (activeTab != null)
        {
            activeTab.IsDocumentPreviewMode = IsDocumentPreviewMode;
            activeTab.CsvTable = ActiveCsvTable;
            activeTab.CsvDimensionsSummary = CsvDimensionsSummary;
            activeTab.CsvDelimiterSummary = CsvDelimiterSummary;
        }

        OnPropertyChanged(nameof(ShowMarkdownPreview));
        OnPropertyChanged(nameof(ShowCsvPreview));
        OnPropertyChanged(nameof(ShowTextEditor));
        OnPropertyChanged(nameof(DocumentPreviewToggleIconKind));
        OnPropertyChanged(nameof(DocumentPreviewToggleText));
        OnPropertyChanged(nameof(DocumentPreviewModeTooltip));
        OnPropertyChanged(nameof(RuntimeLabel));
    }

    public void RefreshActiveDocumentPreview()
    {
        if (IsActiveDocumentCsv)
        {
            var path = Script?.SourceFilePath ?? Script?.Title ?? "data.csv";
            var text = Code ?? string.Empty;
            char delimiter = DetectDelimiter(path, text);
            CsvDelimiterSummary = delimiter switch
            {
                '\t' => "Tab (TSV)",
                ';' => "Semicolon (;)",
                _ => "Comma (,)"
            };

            var records = ParseCsvRecords(text, delimiter);
            ActiveCsvTable = BuildDumpTableFromRecords(records, Path.GetFileName(path), delimiter);
            int rows = ActiveCsvTable.Rows.Count;
            int cols = ActiveCsvTable.Columns.Count;
            CsvDimensionsSummary = $"{rows} {(rows == 1 ? "row" : "rows")} × {cols} {(cols == 1 ? "col" : "cols")}";
        }
        else if (IsActiveDocumentMarkdown)
        {
            OnPropertyChanged(nameof(Code));
        }

        OnPropertyChanged(nameof(RuntimeLabel));
    }

    public void UpdatePreviewStateForDocument(ScriptDocumentItem document, bool? tabPreviewMode = null)
    {
        var path = document.SourceFilePath ?? document.Title;
        var ext = Path.GetExtension(path);

        if (!IsMarkdownExtension(ext) && !IsCsvExtension(ext))
        {
            IsDocumentPreviewMode = false;
            ActiveCsvTable = null;
            CsvDimensionsSummary = string.Empty;
            CsvDelimiterSummary = "Comma (,)";
            NotifyPreviewProperties();
            return;
        }

        // Active tab cached state check
        var activeTab = OpenTabs.FirstOrDefault(t => t.Id == document.Id);
        if (activeTab?.IsDocumentPreviewMode != null)
        {
            IsDocumentPreviewMode = activeTab.IsDocumentPreviewMode.Value;
            ActiveCsvTable = activeTab.CsvTable;
            CsvDimensionsSummary = activeTab.CsvDimensionsSummary;
            CsvDelimiterSummary = activeTab.CsvDelimiterSummary;
        }
        else
        {
            // Default to preview mode for markdown and CSV documents
            IsDocumentPreviewMode = tabPreviewMode ?? true;
        }

        if (IsCsvExtension(ext))
        {
            RefreshActiveDocumentPreview();
            if (activeTab != null)
            {
                activeTab.IsDocumentPreviewMode = IsDocumentPreviewMode;
                activeTab.CsvTable = ActiveCsvTable;
                activeTab.CsvDimensionsSummary = CsvDimensionsSummary;
                activeTab.CsvDelimiterSummary = CsvDelimiterSummary;
            }
        }

        NotifyPreviewProperties();
    }

    private void NotifyPreviewProperties()
    {
        OnPropertyChanged(nameof(IsActiveDocumentMarkdown));
        OnPropertyChanged(nameof(IsActiveDocumentCsv));
        OnPropertyChanged(nameof(HasPreviewMode));
        OnPropertyChanged(nameof(ShowMarkdownPreview));
        OnPropertyChanged(nameof(ShowCsvPreview));
        OnPropertyChanged(nameof(ShowTextEditor));
        OnPropertyChanged(nameof(DocumentPreviewToggleIconKind));
        OnPropertyChanged(nameof(DocumentPreviewToggleText));
        OnPropertyChanged(nameof(DocumentPreviewModeTooltip));
        OnPropertyChanged(nameof(RuntimeLabel));
    }

    [RelayCommand]
    public async Task CopyCsvAsMarkdownAsync()
    {
        if (ActiveCsvTable == null || ActiveCsvTable.Columns.Count == 0) return;

        var sb = new StringBuilder();
        sb.Append("| ");
        foreach (var col in ActiveCsvTable.Columns)
        {
            sb.Append(col.Header).Append(" | ");
        }
        sb.AppendLine();

        sb.Append("| ");
        foreach (var col in ActiveCsvTable.Columns)
        {
            sb.Append(col.IsNumeric ? "---: | " : "--- | ");
        }
        sb.AppendLine();

        foreach (var row in ActiveCsvTable.Rows)
        {
            sb.Append("| ");
            foreach (var cell in row.Cells)
            {
                var clean = (cell.DisplayText ?? string.Empty).Replace("|", "\\|").Replace("\r", "").Replace("\n", " ");
                sb.Append(clean).Append(" | ");
            }
            sb.AppendLine();
        }

        await CopyTextToClipboardAsync(sb.ToString());
    }

    [RelayCommand]
    public async Task CopyCsvAsJsonAsync()
    {
        if (ActiveCsvTable == null || ActiveCsvTable.Columns.Count == 0) return;

        var list = new List<Dictionary<string, object?>>();
        foreach (var row in ActiveCsvTable.Rows)
        {
            var dict = new Dictionary<string, object?>();
            for (int i = 0; i < ActiveCsvTable.Columns.Count; i++)
            {
                var col = ActiveCsvTable.Columns[i].Header;
                object? val = (i < row.Cells.Count) ? (row.Cells[i].RawValue ?? row.Cells[i].DisplayText) : null;
                dict[col] = val;
            }
            list.Add(dict);
        }

        var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
        await CopyTextToClipboardAsync(json);
    }

    [RelayCommand]
    public async Task CopyMarkdownSourceAsync()
    {
        if (!string.IsNullOrEmpty(Code))
        {
            await CopyTextToClipboardAsync(Code);
        }
    }

    public static char DetectDelimiter(string path, string text)
    {
        var ext = Path.GetExtension(path);
        if (ext.Equals(".tsv", StringComparison.OrdinalIgnoreCase)) return '\t';

        var firstLine = text.Split('\n', 2)[0];
        int tabs = firstLine.Count(c => c == '\t');
        int commas = firstLine.Count(c => c == ',');
        int semicolons = firstLine.Count(c => c == ';');

        if (tabs > commas && tabs > semicolons) return '\t';
        if (semicolons > commas && semicolons > tabs) return ';';
        return ',';
    }

    public static List<List<string>> ParseCsvRecords(string text, char delimiter)
    {
        var records = new List<List<string>>();
        if (string.IsNullOrEmpty(text)) return records;

        var currentRecord = new List<string>();
        var currentField = new StringBuilder();
        bool inQuotes = false;
        int i = 0;

        while (i < text.Length)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        currentField.Append('"');
                        i += 2;
                        continue;
                    }
                    else
                    {
                        inQuotes = false;
                        i++;
                        continue;
                    }
                }
                else
                {
                    currentField.Append(c);
                    i++;
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                    i++;
                }
                else if (c == delimiter)
                {
                    currentRecord.Add(currentField.ToString());
                    currentField.Clear();
                    i++;
                }
                else if (c == '\r')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                    currentRecord.Add(currentField.ToString());
                    currentField.Clear();
                    records.Add(currentRecord);
                    currentRecord = new List<string>();
                    i++;
                }
                else if (c == '\n')
                {
                    currentRecord.Add(currentField.ToString());
                    currentField.Clear();
                    records.Add(currentRecord);
                    currentRecord = new List<string>();
                    i++;
                }
                else
                {
                    currentField.Append(c);
                    i++;
                }
            }
        }

        if (currentField.Length > 0 || currentRecord.Count > 0)
        {
            currentRecord.Add(currentField.ToString());
            records.Add(currentRecord);
        }

        return records;
    }

    public static DumpTableResult BuildDumpTableFromRecords(List<List<string>> records, string title, char delimiter)
    {
        var result = new DumpTableResult(title, delimiter == '\t' ? "TSV Table" : "CSV Table");
        if (records.Count == 0) return result;

        var headerRow = records[0];
        int colCount = headerRow.Count;

        var dataRows = records.Skip(1).Where(r => r.Count > 1 || (r.Count == 1 && !string.IsNullOrWhiteSpace(r[0]))).ToList();

        var isNumeric = new bool[colCount];
        for (int c = 0; c < colCount; c++)
        {
            int numericCount = 0;
            int nonNullCount = 0;
            foreach (var row in dataRows)
            {
                if (c < row.Count && !string.IsNullOrWhiteSpace(row[c]))
                {
                    nonNullCount++;
                    if (double.TryParse(row[c].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                    {
                        numericCount++;
                    }
                }
            }
            isNumeric[c] = nonNullCount > 0 && numericCount == nonNullCount;
        }

        for (int c = 0; c < colCount; c++)
        {
            var name = string.IsNullOrWhiteSpace(headerRow[c]) ? $"Column {c + 1}" : headerRow[c].Trim();
            result.Columns.Add(new DumpTableColumn
            {
                Header = name,
                IsNumeric = isNumeric[c]
            });
        }

        for (int r = 0; r < dataRows.Count; r++)
        {
            var rowList = dataRows[r];
            var cells = new List<DumpTableCell>(colCount);
            for (int c = 0; c < colCount; c++)
            {
                var text = (c < rowList.Count) ? rowList[c].Trim() : string.Empty;
                var cell = CreateCellFromText(text, isNumeric[c]);
                cells.Add(cell);
            }
            result.Rows.Add(new DumpTableRow(r, cells));
        }

        return result;
    }

    private static DumpTableCell CreateCellFromText(string text, bool isColNumeric)
    {
        if (string.IsNullOrEmpty(text) || text.Equals("NULL", StringComparison.OrdinalIgnoreCase))
        {
            return new DumpTableCell
            {
                DisplayText = text.Equals("NULL", StringComparison.OrdinalIgnoreCase) ? "<null>" : "",
                RawValue = null,
                IsNull = true,
                IsNumeric = isColNumeric
            };
        }

        if (bool.TryParse(text, out var b))
        {
            return new DumpTableCell
            {
                DisplayText = text,
                RawValue = b,
                IsBoolean = true,
                IsNumeric = false
            };
        }

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
        {
            return new DumpTableCell
            {
                DisplayText = text,
                RawValue = l,
                IsNumeric = true
            };
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
        {
            return new DumpTableCell
            {
                DisplayText = text,
                RawValue = d,
                IsNumeric = true
            };
        }

        return new DumpTableCell
        {
            DisplayText = text,
            RawValue = text,
            IsNumeric = isColNumeric
        };
    }
}
