using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Processes;

/// <summary>
/// Detects and parses ASCII grid tables (such as SQLite CLI <c>-header -table</c>, MySQL CLI,
/// Python tabulate, etc.) from streaming process output lines into structured <see cref="DumpTableResult"/>s.
/// </summary>
public sealed class AsciiTableDetector
{
    private enum State
    {
        Idle,
        WaitingForHeader,
        WaitingForSeparator,
        ReadingRows
    }

    private State _state = State.Idle;
    private List<int> _colOffsets = new();
    private List<string> _columnNames = new();
    private readonly List<List<string>> _rows = new();
    private int _tableCount;
    private readonly string? _defaultTitle;
    private readonly Action<DumpTableResult>? _onTableDetected;

    public AsciiTableDetector(Action<DumpTableResult>? onTableDetected = null, string? defaultTitle = null)
    {
        _onTableDetected = onTableDetected;
        _defaultTitle = defaultTitle;
    }

    public AsciiTableDetector(string? defaultTitle) : this(null, defaultTitle) { }

    public bool ProcessLine(string line)
    {
        if (ProcessLine(line, out var table) && table != null)
        {
            _onTableDetected?.Invoke(table);
            return true;
        }
        return false;
    }

    public bool Flush()
    {
        if (Flush(out var table) && table != null)
        {
            _onTableDetected?.Invoke(table);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Processes a line from stdout. Returns true if a complete table was finished on this line,
    /// with <paramref name="completedTable"/> populated.
    /// </summary>
    public bool ProcessLine(string line, out DumpTableResult? completedTable)
    {
        completedTable = null;
        var trimmed = line.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            Reset();
            return false;
        }

        switch (_state)
        {
            case State.Idle:
                if (IsBorderLine(trimmed, out var offsets))
                {
                    _colOffsets = offsets;
                    _columnNames.Clear();
                    _rows.Clear();
                    _state = State.WaitingForHeader;
                }
                return false;

            case State.WaitingForHeader:
                if (IsDataLine(trimmed))
                {
                    var cols = ExtractCells(trimmed, _colOffsets);
                    if (cols.Count == _colOffsets.Count - 1 && cols.Any(c => !string.IsNullOrEmpty(c)))
                    {
                        _columnNames = cols;
                        _state = State.WaitingForSeparator;
                        return false;
                    }
                }
                Reset();
                return false;

            case State.WaitingForSeparator:
                if (IsBorderLine(trimmed, out var sepOffsets) && SameOffsets(_colOffsets, sepOffsets))
                {
                    _state = State.ReadingRows;
                    return false;
                }
                Reset();
                return false;

            case State.ReadingRows:
                if (IsBorderLine(trimmed, out var endOffsets) && SameOffsets(_colOffsets, endOffsets))
                {
                    // Table completed!
                    _tableCount++;
                    var title = !string.IsNullOrWhiteSpace(_defaultTitle)
                        ? (_tableCount > 1 ? $"{_defaultTitle} {_tableCount}" : _defaultTitle)
                        : (_tableCount > 1 ? $"Query Result {_tableCount}" : "Query Result");

                    completedTable = BuildTableResult(title, _columnNames, _rows);
                    Reset();
                    return true;
                }

                if (IsDataLine(trimmed))
                {
                    var cells = ExtractCells(trimmed, _colOffsets);
                    if (cells.Count == _columnNames.Count)
                    {
                        _rows.Add(cells);
                        return false;
                    }
                }

                // Unexpected line while reading rows
                Reset();
                return false;
        }

        return false;
    }

    /// <summary>
    /// Flushes any pending table detection on stream completion.
    /// </summary>
    public bool Flush(out DumpTableResult? completedTable)
    {
        completedTable = null;
        Reset();
        return false;
    }

    private void Reset()
    {
        _state = State.Idle;
        _colOffsets.Clear();
        _columnNames.Clear();
        _rows.Clear();
    }

    public static bool IsBorderLine(string line, out List<int> colOffsets)
    {
        colOffsets = new List<int>();
        if (line.Length < 3 || line[0] != '+' || line[^1] != '+') return false;

        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (ch == '+')
            {
                colOffsets.Add(i);
            }
            else if (ch != '-' && ch != '=')
            {
                return false;
            }
        }

        return colOffsets.Count >= 2;
    }

    public static bool IsDataLine(string line)
    {
        return line.Length >= 3 && line[0] == '|' && line[^1] == '|';
    }

    public static List<string> ExtractCells(string line, List<int> colOffsets)
    {
        var cells = new List<string>(colOffsets.Count - 1);
        if (colOffsets.Count < 2) return cells;

        if (line.Length >= colOffsets[^1])
        {
            for (int i = 0; i < colOffsets.Count - 1; i++)
            {
                int start = colOffsets[i] + 1;
                int end = colOffsets[i + 1];
                if (start < line.Length && end <= line.Length && end > start)
                {
                    cells.Add(line.Substring(start, end - start).Trim());
                }
                else
                {
                    cells.Add(string.Empty);
                }
            }
            return cells;
        }

        // Fallback: split by '|'
        var parts = line.Substring(1, line.Length - 2).Split('|');
        foreach (var p in parts)
        {
            cells.Add(p.Trim());
        }
        return cells;
    }

    private static bool SameOffsets(List<int> a, List<int> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }

    public static DumpTableResult BuildTableResult(string title, List<string> columnNames, List<List<string>> rawRows)
    {
        var result = new DumpTableResult(title);

        var isNumeric = new bool[columnNames.Count];
        for (int c = 0; c < columnNames.Count; c++)
        {
            bool allNumeric = rawRows.Count > 0;
            int nonNullCount = 0;
            for (int r = 0; r < rawRows.Count; r++)
            {
                var val = (c < rawRows[r].Count) ? rawRows[r][c] : string.Empty;
                if (string.IsNullOrEmpty(val) || val.Equals("NULL", StringComparison.OrdinalIgnoreCase)) continue;
                nonNullCount++;
                if (!double.TryParse(val, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out _) &&
                    !long.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                {
                    allNumeric = false;
                    break;
                }
            }
            isNumeric[c] = allNumeric && nonNullCount > 0;
            result.Columns.Add(new DumpTableColumn
            {
                Header = columnNames[c],
                IsNumeric = isNumeric[c]
            });
        }

        for (int r = 0; r < rawRows.Count; r++)
        {
            var rowList = rawRows[r];
            var cells = new List<DumpTableCell>(columnNames.Count);
            for (int c = 0; c < columnNames.Count; c++)
            {
                var text = (c < rowList.Count) ? rowList[c] : string.Empty;
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

        DumpTableResult? nestedTable = null;
        if (text.Length >= 2 && (text[0] == '[' || text[0] == '{' || text.Contains('\n')))
        {
            if (Display.NestedTableDetector.TryDetectNestedTable(text, out var detected))
            {
                nestedTable = detected;
            }
        }

        return new DumpTableCell
        {
            DisplayText = text,
            RawValue = text,
            IsNumeric = isColNumeric,
            NestedTable = nestedTable
        };
    }
}
