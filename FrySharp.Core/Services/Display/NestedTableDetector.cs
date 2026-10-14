using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

/// <summary>
/// Inspects cell values, JSON payloads, collections, dictionaries, and embedded tabular strings
/// to determine if a cell contains nested table data that can be explored interactively.
/// </summary>
public static class NestedTableDetector
{
    private const int MaxNestingDepth = 3;

    public static bool TryDetectNestedTable(object? val, out DumpTableResult? table, int currentDepth = 0)
    {
        table = null;
        if (val == null || currentDepth >= MaxNestingDepth) return false;

        // 1. Direct DumpTableResult
        if (val is DumpTableResult dtr && dtr.Columns.Count > 0)
        {
            table = dtr;
            return true;
        }

        // 2. Tabular strings (JSON array/object or embedded ASCII table)
        if (val is string str)
        {
            var trimmed = str.Trim();
            if (trimmed.Length >= 2)
            {
                // JSON Array: [{"id": 1, ...}] or [1, 2, 3]
                if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
                {
                    if (TryParseJsonArray(trimmed, currentDepth, out table))
                    {
                        return true;
                    }
                }
                // JSON Object: {"key": "val", ...}
                else if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
                {
                    if (TryParseJsonObject(trimmed, currentDepth, out table))
                    {
                        return true;
                    }
                }
                // Embedded ASCII Grid Table (+---+---+)
                else if (trimmed.Contains('\n') && (trimmed.StartsWith('+') || trimmed.StartsWith('|')))
                {
                    if (TryParseAsciiTable(trimmed, out table))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // 3. IDictionary (e.g. Dictionary<string, object>, Hashtable)
        if (val is IDictionary dict && dict.Count > 0)
        {
            var dictTable = new DumpTableResult($"Dictionary [{dict.Count}]");
            dictTable.Columns.Add(new DumpTableColumn { Header = "Key", IsNumeric = false });
            dictTable.Columns.Add(new DumpTableColumn { Header = "Value", IsNumeric = false });

            int idx = 0;
            foreach (DictionaryEntry entry in dict)
            {
                var keyStr = entry.Key?.ToString() ?? "<null>";
                var keyCell = new DumpTableCell { DisplayText = keyStr, RawValue = entry.Key };
                
                DumpTableResult? childNested = null;
                if (entry.Value != null && !DumpTableBuilder.IsScalarType(entry.Value.GetType()))
                {
                    TryDetectNestedTable(entry.Value, out childNested, currentDepth + 1);
                }

                var valCell = new DumpTableCell
                {
                    DisplayText = entry.Value?.ToString() ?? "<null>",
                    RawValue = entry.Value,
                    IsNull = entry.Value == null,
                    NestedTable = childNested
                };

                dictTable.Rows.Add(new DumpTableRow(idx++, new[] { keyCell, valCell }));
            }

            table = dictTable;
            return true;
        }

        // 4. Non-string IEnumerable (List<T>, Array, etc.)
        if (val is IEnumerable seq and not string and not byte[])
        {
            var itemsList = new List<object?>();
            foreach (var item in seq)
            {
                itemsList.Add(item);
                if (itemsList.Count > 500) break; // Safety cap
            }

            if (itemsList.Count > 0)
            {
                // If items are complex objects or dictionaries or sub-collections
                bool hasComplexItems = itemsList.Any(x => x != null && !DumpTableBuilder.IsScalarType(x.GetType()));
                if (hasComplexItems || itemsList.Count > 1)
                {
                    table = DumpTableBuilder.Create(itemsList);
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryParseJsonArray(string json, int currentDepth, out DumpTableResult? table)
    {
        table = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return false;

            var array = doc.RootElement;
            int count = array.GetArrayLength();
            if (count == 0) return false;

            // Check if array of objects: [{"col1": val, "col2": val}, ...]
            bool isArrayOfObjects = true;
            var columnNames = new List<string>();
            var colSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var el in array.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object)
                {
                    isArrayOfObjects = false;
                    break;
                }

                foreach (var prop in el.EnumerateObject())
                {
                    if (colSeen.Add(prop.Name))
                    {
                        columnNames.Add(prop.Name);
                    }
                }
            }

            if (isArrayOfObjects && columnNames.Count > 0)
            {
                var result = new DumpTableResult($"JSON Table [{count} rows]");
                var colIsNumeric = new Dictionary<string, bool>();

                // Determine column data types
                foreach (var col in columnNames)
                {
                    bool allNumeric = true;
                    int checkedCount = 0;
                    foreach (var el in array.EnumerateArray())
                    {
                        if (el.TryGetProperty(col, out var p) && p.ValueKind != JsonValueKind.Null)
                        {
                            checkedCount++;
                            if (p.ValueKind != JsonValueKind.Number)
                            {
                                allNumeric = false;
                                break;
                            }
                        }
                    }
                    colIsNumeric[col] = (checkedCount > 0 && allNumeric);
                    result.Columns.Add(new DumpTableColumn
                    {
                        Header = col,
                        IsNumeric = colIsNumeric[col]
                    });
                }

                int rowIndex = 0;
                foreach (var el in array.EnumerateArray())
                {
                    var cells = new List<DumpTableCell>(columnNames.Count);
                    foreach (var col in columnNames)
                    {
                        if (el.TryGetProperty(col, out var propVal))
                        {
                            cells.Add(CreateJsonCell(propVal, colIsNumeric[col], currentDepth));
                        }
                        else
                        {
                            cells.Add(new DumpTableCell { DisplayText = "<null>", IsNull = true });
                        }
                    }
                    result.Rows.Add(new DumpTableRow(rowIndex++, cells));
                }

                table = result;
                return true;
            }
            else
            {
                // Array of primitives or mixed items: [1, 2, 3] or ["a", "b"]
                var result = new DumpTableResult($"JSON Array [{count}]");
                bool isNumeric = true;
                foreach (var el in array.EnumerateArray())
                {
                    if (el.ValueKind != JsonValueKind.Number && el.ValueKind != JsonValueKind.Null)
                    {
                        isNumeric = false;
                        break;
                    }
                }

                result.Columns.Add(new DumpTableColumn { Header = "Item", IsNumeric = isNumeric });
                int rowIndex = 0;
                foreach (var el in array.EnumerateArray())
                {
                    result.Rows.Add(new DumpTableRow(rowIndex++, new[] { CreateJsonCell(el, isNumeric, currentDepth) }));
                }

                table = result;
                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseJsonObject(string json, int currentDepth, out DumpTableResult? table)
    {
        table = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;

            var obj = doc.RootElement;
            var props = obj.EnumerateObject().ToList();
            if (props.Count == 0) return false;

            var result = new DumpTableResult($"JSON Object ({props.Count} properties)");
            result.Columns.Add(new DumpTableColumn { Header = "Property", IsNumeric = false });
            result.Columns.Add(new DumpTableColumn { Header = "Value", IsNumeric = false });

            int rowIndex = 0;
            foreach (var prop in props)
            {
                var keyCell = new DumpTableCell { DisplayText = prop.Name, RawValue = prop.Name };
                var valCell = CreateJsonCell(prop.Value, isNumeric: prop.Value.ValueKind == JsonValueKind.Number, currentDepth);
                result.Rows.Add(new DumpTableRow(rowIndex++, new[] { keyCell, valCell }));
            }

            table = result;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseAsciiTable(string text, out DumpTableResult? table)
    {
        table = null;
        try
        {
            var detected = new List<DumpTableResult>();
            var detector = new AsciiTableDetector(detected.Add);
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                detector.ProcessLine(line);
            }
            detector.Flush();

            if (detected.Count > 0)
            {
                table = detected[0];
                return true;
            }
        }
        catch { }

        return false;
    }

    private static DumpTableCell CreateJsonCell(JsonElement el, bool isNumeric, int currentDepth)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return new DumpTableCell { DisplayText = "<null>", IsNull = true };

            case JsonValueKind.True:
                return new DumpTableCell { DisplayText = "true", RawValue = true, IsBoolean = true };

            case JsonValueKind.False:
                return new DumpTableCell { DisplayText = "false", RawValue = false, IsBoolean = true };

            case JsonValueKind.Number:
                return new DumpTableCell
                {
                    DisplayText = el.GetRawText(),
                    RawValue = el.TryGetInt64(out var l) ? (object)l : (el.TryGetDouble(out var d) ? d : el.GetRawText()),
                    IsNumeric = true
                };

            case JsonValueKind.String:
                var text = el.GetString() ?? string.Empty;
                return new DumpTableCell { DisplayText = text, RawValue = text };

            case JsonValueKind.Array:
            case JsonValueKind.Object:
                var rawJson = el.GetRawText();
                DumpTableResult? nested = null;
                if (currentDepth + 1 < MaxNestingDepth)
                {
                    if (el.ValueKind == JsonValueKind.Array)
                        TryParseJsonArray(rawJson, currentDepth + 1, out nested);
                    else
                        TryParseJsonObject(rawJson, currentDepth + 1, out nested);
                }

                string summary = el.ValueKind == JsonValueKind.Array
                    ? $"[{el.GetArrayLength()} items]"
                    : "{...}";

                return new DumpTableCell
                {
                    DisplayText = summary,
                    RawValue = rawJson,
                    NestedTable = nested
                };

            default:
                return new DumpTableCell { DisplayText = el.GetRawText() };
        }
    }
}
