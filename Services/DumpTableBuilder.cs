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

        // Anonymous types: e.g. <>f__AnonymousType0`2
        if (type.Name.Contains("AnonymousType"))
        {
            return "AnonymousType";
        }

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
}
