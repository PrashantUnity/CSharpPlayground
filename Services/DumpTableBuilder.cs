using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

/// <summary>
/// Reflective analyzer that converts any .NET object, collection, array, dictionary,
/// or scalar value into a structured DumpTableResult with rich tabular presentation.
/// </summary>
public static class DumpTableBuilder
{
    public static DumpTableResult Create(object? obj, string? label = null)
    {
        if (obj == null)
        {
            var nullTable = new DumpTableResult("null", label);
            nullTable.Columns.Add(new DumpTableColumn { Header = "Value", IsNumeric = false });
            nullTable.Rows.Add(new DumpTableRow(0, new[] { new DumpTableCell { DisplayText = "<null>", IsNull = true } }));
            return nullTable;
        }

        if (obj is DumpTableResult dtr)
        {
            return dtr;
        }

        // Dedicated Tabular Data Formats (Microsoft.Data.Analysis.DataFrame, System.Data.DataTable, System.Data.DataView)
        if (IsTabularObject(obj, out var tabularResult, label))
        {
            return tabularResult!;
        }

        var type = obj.GetType();

        // 1. IDictionary (e.g. Dictionary<string, int>, Hashtable, etc.)
        if (obj is IDictionary dict)
        {
            var dictTitle = GetFriendlyTypeName(type) + $"[{dict.Count}]";
            var table = new DumpTableResult(dictTitle, label);
            table.Columns.Add(new DumpTableColumn { Header = "Key", IsNumeric = false });
            table.Columns.Add(new DumpTableColumn { Header = "Value", IsNumeric = false });

            int idx = 0;
            foreach (DictionaryEntry entry in dict)
            {
                var keyCell = CreateCell(entry.Key);
                var valCell = CreateCell(entry.Value);
                table.Rows.Add(new DumpTableRow(idx++, new[] { keyCell, valCell }));
            }
            return table;
        }

        // 2. 1D Array or IEnumerable (except string)
        if (obj is IEnumerable enumerable and not string)
        {
            var itemsList = new List<object?>();
            foreach (var item in enumerable)
            {
                itemsList.Add(item);
            }

            // Determine element type
            Type? elemType = null;
            if (type.IsArray)
            {
                elemType = type.GetElementType();
            }
            else
            {
                var ienumInterface = type.GetInterfaces().Concat(new[] { type })
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
                if (ienumInterface != null)
                {
                    elemType = ienumInterface.GetGenericArguments()[0];
                }
            }

            if (elemType == null && itemsList.Count > 0)
            {
                elemType = itemsList.FirstOrDefault(x => x != null)?.GetType() ?? typeof(object);
            }
            elemType ??= typeof(object);

            var titleName = GetFriendlyTypeName(elemType) + $"[{itemsList.Count}]";
            var table = new DumpTableResult(titleName, label);

            // Is it primitive, enum, string, or scalar?
            if (IsScalarType(elemType))
            {
                bool isNumeric = IsNumericType(elemType);
                table.Columns.Add(new DumpTableColumn { Header = "Item", IsNumeric = isNumeric });

                for (int i = 0; i < itemsList.Count; i++)
                {
                    var cell = CreateCell(itemsList[i], isNumeric);
                    table.Rows.Add(new DumpTableRow(i, new[] { cell }));
                }
                return table;
            }

            // It's a collection of complex objects / anonymous types / records
            // Inspect public properties
            var properties = elemType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                .ToList();

            // If elemType is object or has no readable properties, check first non-null item
            if (properties.Count == 0 && itemsList.Count > 0 && itemsList[0] != null)
            {
                properties = itemsList[0]!.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                    .ToList();
            }

            if (properties.Count > 0)
            {
                foreach (var prop in properties)
                {
                    table.Columns.Add(new DumpTableColumn
                    {
                        Header = prop.Name,
                        IsNumeric = IsNumericType(prop.PropertyType)
                    });
                }

                for (int i = 0; i < itemsList.Count; i++)
                {
                    var item = itemsList[i];
                    var cells = new List<DumpTableCell>();
                    foreach (var prop in properties)
                    {
                        object? val = null;
                        if (item != null)
                        {
                            try { val = prop.GetValue(item); } catch { }
                        }
                        cells.Add(CreateCell(val, IsNumericType(prop.PropertyType)));
                    }
                    table.Rows.Add(new DumpTableRow(i, cells));
                }
                return table;
            }

            // Fallback: single column with ToString()
            table.Columns.Add(new DumpTableColumn { Header = "Item", IsNumeric = false });
            for (int i = 0; i < itemsList.Count; i++)
            {
                table.Rows.Add(new DumpTableRow(i, new[] { CreateCell(itemsList[i]) }));
            }
            return table;
        }

        // 3. Single Complex Object (Not IEnumerable and not scalar)
        if (!IsScalarType(type))
        {
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                .ToList();

            if (props.Count > 0)
            {
                var table = new DumpTableResult(GetFriendlyTypeName(type), label);
                table.Columns.Add(new DumpTableColumn { Header = "Property", IsNumeric = false });
                table.Columns.Add(new DumpTableColumn { Header = "Value", IsNumeric = false });

                int idx = 0;
                foreach (var prop in props)
                {
                    object? val = null;
                    try { val = prop.GetValue(obj); } catch { }
                    var propCell = new DumpTableCell { DisplayText = prop.Name, RawValue = prop.Name };
                    var valCell = CreateCell(val, IsNumericType(prop.PropertyType));
                    table.Rows.Add(new DumpTableRow(idx++, new[] { propCell, valCell }));
                }
                return table;
            }
        }

        // 4. Scalar Primitive / String
        {
            var isNumeric = IsNumericType(type);
            var table = new DumpTableResult(GetFriendlyTypeName(type), label);
            table.Columns.Add(new DumpTableColumn { Header = "Value", IsNumeric = isNumeric });
            table.Rows.Add(new DumpTableRow(0, new[] { CreateCell(obj, isNumeric) }));
            return table;
        }
    }

    public static string GetFriendlyTypeName(Type type)
    {
        if (type == typeof(int)) return "Int32";
        if (type == typeof(long)) return "Int64";
        if (type == typeof(short)) return "Int16";
        if (type == typeof(byte)) return "Byte";
        if (type == typeof(double)) return "Double";
        if (type == typeof(float)) return "Single";
        if (type == typeof(decimal)) return "Decimal";
        if (type == typeof(bool)) return "Boolean";
        if (type == typeof(string)) return "String";
        if (type == typeof(char)) return "Char";
        if (type == typeof(object)) return "Object";
        if (type.Name.Contains("AnonymousType")) return "AnonymousType";

        // Generics: e.g. List<string> -> List<String>
        if (type.IsGenericType)
        {
            var cleanName = type.Name.Substring(0, type.Name.IndexOf('`'));
            var genericArgs = string.Join(", ", type.GetGenericArguments().Select(GetFriendlyTypeName));
            return $"{cleanName}<{genericArgs}>";
        }

        return type.Name;
    }

    public static bool IsScalarType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsPrimitive ||
               type.IsEnum ||
               type == typeof(string) ||
               type == typeof(decimal) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(TimeSpan) ||
               type == typeof(Guid);
    }

    public static bool IsNumericType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type.IsEnum) return false;
        return Type.GetTypeCode(type) switch
        {
            TypeCode.Byte or TypeCode.SByte or
            TypeCode.UInt16 or TypeCode.UInt32 or TypeCode.UInt64 or
            TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64 or
            TypeCode.Decimal or TypeCode.Double or TypeCode.Single => true,
            _ => false
        };
    }

    public static DumpTableCell CreateCell(object? val, bool? isNumeric = null)
    {
        if (val == null)
        {
            return new DumpTableCell
            {
                DisplayText = "<null>",
                RawValue = null,
                IsNull = true
            };
        }

        var t = val.GetType();
        bool num = isNumeric ?? IsNumericType(t);
        bool b = val is bool;

        string text;
        if (val is double d) text = d.ToString("G");
        else if (val is float f) text = f.ToString("G");
        else if (val is decimal m) text = m.ToString("G");
        else if (val is DateTime dt) text = dt.ToString("yyyy-MM-dd HH:mm:ss");
        else if (val is TimeSpan ts) text = ts.ToString();
        else text = val.ToString() ?? string.Empty;

        return new DumpTableCell
        {
            DisplayText = text,
            RawValue = val,
            IsNumeric = num,
            IsBoolean = b,
            IsNull = false
        };
    }

    /// <summary>
    /// Checks whether the specified object is a first-class tabular dataset (such as
    /// Microsoft.Data.Analysis.DataFrame, System.Data.DataTable, or System.Data.DataView)
    /// and constructs a structured DumpTableResult with schema and cell values.
    /// </summary>
    public static bool IsTabularObject(object? obj, out DumpTableResult? result, string? label = null)
    {
        result = null;
        if (obj == null) return false;

        // 1. ADO.NET System.Data.DataTable
        if (obj is System.Data.DataTable dataTable)
        {
            result = BuildFromDataTable(dataTable, label);
            return true;
        }

        // 2. ADO.NET System.Data.DataView
        if (obj is System.Data.DataView dataView)
        {
            result = BuildFromDataView(dataView, label);
            return true;
        }

        // 3. Microsoft.Data.Analysis.DataFrame (detected via duck-typing reflection)
        if (IsDataFrame(obj, out var dfTable, label))
        {
            result = dfTable;
            return true;
        }

        return false;
    }

    public static DumpTableResult BuildFromDataTable(System.Data.DataTable dt, string? label = null)
    {
        var title = string.IsNullOrEmpty(dt.TableName)
            ? $"DataTable [{dt.Rows.Count:N0} rows × {dt.Columns.Count} cols]"
            : $"DataTable: {dt.TableName} [{dt.Rows.Count:N0} rows × {dt.Columns.Count} cols]";

        var table = new DumpTableResult(title, label);
        foreach (System.Data.DataColumn col in dt.Columns)
        {
            table.Columns.Add(new DumpTableColumn
            {
                Header = col.ColumnName,
                IsNumeric = IsNumericType(col.DataType)
            });
        }

        const int MaxPreviewRows = 1000;
        int renderRows = Math.Min(dt.Rows.Count, MaxPreviewRows);

        for (int r = 0; r < renderRows; r++)
        {
            var row = dt.Rows[r];
            var cells = new List<DumpTableCell>(dt.Columns.Count);
            for (int c = 0; c < dt.Columns.Count; c++)
            {
                var val = row.IsNull(c) ? null : row[c];
                cells.Add(CreateCell(val, IsNumericType(dt.Columns[c].DataType)));
            }
            table.Rows.Add(new DumpTableRow(r, cells));
        }

        return table;
    }

    public static DumpTableResult BuildFromDataView(System.Data.DataView dv, string? label = null)
    {
        var dt = dv.Table;
        var tableName = dt != null && !string.IsNullOrEmpty(dt.TableName) ? dt.TableName : "View";
        var colCount = dt?.Columns.Count ?? 0;
        var title = $"DataView: {tableName} [{dv.Count:N0} rows × {colCount} cols]";

        var table = new DumpTableResult(title, label);
        if (dt != null)
        {
            foreach (System.Data.DataColumn col in dt.Columns)
            {
                table.Columns.Add(new DumpTableColumn
                {
                    Header = col.ColumnName,
                    IsNumeric = IsNumericType(col.DataType)
                });
            }
        }

        const int MaxPreviewRows = 1000;
        int renderRows = Math.Min(dv.Count, MaxPreviewRows);

        for (int r = 0; r < renderRows; r++)
        {
            var rowView = dv[r];
            var cells = new List<DumpTableCell>(colCount);
            for (int c = 0; c < colCount; c++)
            {
                var val = dt != null && rowView.Row.IsNull(c) ? null : rowView[c];
                var isNum = dt != null && IsNumericType(dt.Columns[c].DataType);
                cells.Add(CreateCell(val, isNum));
            }
            table.Rows.Add(new DumpTableRow(r, cells));
        }

        return table;
    }

    private static bool IsDataFrame(object obj, out DumpTableResult? result, string? label)
    {
        result = null;
        var type = obj.GetType();
        var typeName = type.FullName ?? type.Name;

        // Check for Microsoft.Data.Analysis.DataFrame or duck-typed DataFrame
        bool nameMatches = typeName.Contains("DataFrame") ||
                           typeName.StartsWith("Microsoft.Data.Analysis", StringComparison.OrdinalIgnoreCase);

        if (!nameMatches) return false;

        var colsProp = type.GetProperty("Columns", BindingFlags.Public | BindingFlags.Instance);
        if (colsProp == null) return false;

        var colsObj = colsProp.GetValue(obj) as IEnumerable;
        if (colsObj == null) return false;

        // Count rows
        long rowCount = 0;
        var rowCountProp = type.GetProperty("RowCount", BindingFlags.Public | BindingFlags.Instance);
        if (rowCountProp != null)
        {
            rowCount = Convert.ToInt64(rowCountProp.GetValue(obj));
        }
        else
        {
            var rowsProp = type.GetProperty("Rows", BindingFlags.Public | BindingFlags.Instance);
            var rowsObj = rowsProp?.GetValue(obj);
            if (rowsObj != null)
            {
                var countProp = rowsObj.GetType().GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);
                if (countProp != null)
                {
                    rowCount = Convert.ToInt64(countProp.GetValue(rowsObj));
                }
            }
        }

        // Extract column information
        var colList = new List<(string Name, Type DataType, object ColObj, MethodInfo? Getter, PropertyInfo? Indexer)>();
        foreach (var c in colsObj)
        {
            if (c == null) continue;
            var cType = c.GetType();
            var nameProp = cType.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
            var dtProp = cType.GetProperty("DataType", BindingFlags.Public | BindingFlags.Instance);
            var name = nameProp?.GetValue(c)?.ToString() ?? "Column";
            var colDataType = dtProp?.GetValue(c) as Type ?? typeof(object);

            // Indexer this[long] or this[int]
            var indexer = cType.GetProperties()
                .FirstOrDefault(p => p.GetIndexParameters().Length == 1 &&
                                     (p.GetIndexParameters()[0].ParameterType == typeof(long) ||
                                      p.GetIndexParameters()[0].ParameterType == typeof(int)));

            // Getter method GetValue(long) or GetValue(int)
            var getter = cType.GetMethod("GetValue", new[] { typeof(long) }) ??
                         cType.GetMethod("GetValue", new[] { typeof(int) });

            colList.Add((name, colDataType, c, getter, indexer));
        }

        if (rowCount == 0 && colList.Count > 0)
        {
            var lenProp = colList[0].ColObj.GetType().GetProperty("Length", BindingFlags.Public | BindingFlags.Instance);
            if (lenProp != null)
            {
                rowCount = Convert.ToInt64(lenProp.GetValue(colList[0].ColObj));
            }
        }

        var table = new DumpTableResult($"DataFrame [{rowCount:N0} rows × {colList.Count} cols]", label);
        foreach (var (name, dt, _, _, _) in colList)
        {
            table.Columns.Add(new DumpTableColumn
            {
                Header = name,
                IsNumeric = IsNumericType(dt)
            });
        }

        const int MaxPreviewRows = 1000;
        int renderRows = (int)Math.Min(rowCount, MaxPreviewRows);

        for (int r = 0; r < renderRows; r++)
        {
            var cells = new List<DumpTableCell>(colList.Count);
            for (int c = 0; c < colList.Count; c++)
            {
                var (_, dt, colObj, getter, indexer) = colList[c];
                object? val = null;
                try
                {
                    if (indexer != null)
                    {
                        var paramType = indexer.GetIndexParameters()[0].ParameterType;
                        object arg = paramType == typeof(long) ? (long)r : r;
                        val = indexer.GetValue(colObj, new[] { arg });
                    }
                    else if (getter != null)
                    {
                        var paramType = getter.GetParameters()[0].ParameterType;
                        object arg = paramType == typeof(long) ? (long)r : r;
                        val = getter.Invoke(colObj, new[] { arg });
                    }
                }
                catch
                {
                    // Fallback
                }
                cells.Add(CreateCell(val, IsNumericType(dt)));
            }
            table.Rows.Add(new DumpTableRow(r, cells));
        }

        result = table;
        return true;
    }
}
