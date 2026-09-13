using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

/// <summary>
/// Reflective inspector that converts any .NET object into an interactive hierarchical
/// ObjectInspectorNode matching VS Code .NET Interactive / Polyglot Notebooks inspection UI.
/// </summary>
public static class ObjectInspectorBuilder
{
    private const int MaxAutoDepth = 3;

    public static ObjectInspectorNode Build(object? obj, int maxDepth = MaxAutoDepth)
    {
        if (obj == null)
        {
            var nullNode = new ObjectInspectorNode("null", isExpanded: true, isExpandable: false);
            nullNode.Properties.Add(new ObjectInspectorPropertyRow
            {
                Name = "Value",
                SimpleValueText = "<null>",
                IsNull = true
            });
            return nullNode;
        }

        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        return BuildNode(obj, depth: 0, maxDepth: maxDepth, visited: visited);
    }

    private static ObjectInspectorNode BuildNode(
        object obj,
        int depth,
        int maxDepth,
        HashSet<object> visited)
    {
        var type = obj.GetType();
        var typeTitle = GetFormattedTypeName(type);
        bool isCircular = visited.Contains(obj);

        // At depth 0, top level is always expanded. At depth 1, expand unless circular. At depth >= 2, collapse by default.
        bool isExpanded = depth == 0 || (!isCircular && depth < 2);

        var node = new ObjectInspectorNode(typeTitle, isExpanded: isExpanded, isExpandable: true)
        {
            IsCircularReference = isCircular
        };

        if (isCircular && depth >= 2)
        {
            // For deeply nested circular references, collapse by default to prevent runaway inspection
            node.IsExpanded = false;
        }

        visited.Add(obj);

        // 1. Dictionaries
        if (obj is IDictionary dict)
        {
            foreach (DictionaryEntry entry in dict)
            {
                var keyStr = entry.Key?.ToString() ?? "<null>";
                var row = CreatePropertyRow(keyStr, entry.Value, depth + 1, maxDepth, visited);
                node.Properties.Add(row);
            }
            visited.Remove(obj);
            return node;
        }

        // 2. Collections / Arrays (Non-string)
        if (obj is IEnumerable enumerable and not string)
        {
            int idx = 0;
            foreach (var item in enumerable)
            {
                var row = CreatePropertyRow($"[{idx++}]", item, depth + 1, maxDepth, visited);
                node.Properties.Add(row);
                if (idx >= 100) // safety limit for huge collections
                {
                    node.Properties.Add(new ObjectInspectorPropertyRow
                    {
                        Name = "...",
                        SimpleValueText = $"({idx}+ items truncated)"
                    });
                    break;
                }
            }
            visited.Remove(obj);
            return node;
        }

        // 3. Complex Objects: Inspect public instance properties
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToList();

        foreach (var prop in properties)
        {
            object? val = null;
            try
            {
                val = prop.GetValue(obj);
            }
            catch (Exception ex)
            {
                val = $"<Error: {ex.Message}>";
            }

            var row = CreatePropertyRow(prop.Name, val, depth + 1, maxDepth, visited);
            node.Properties.Add(row);
        }

        // Also inspect public instance fields that are not compiler-generated backing fields
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(f => !f.Name.Contains("<") && !f.Name.Contains("k__BackingField"))
            .ToList();

        foreach (var field in fields)
        {
            object? val = null;
            try
            {
                val = field.GetValue(obj);
            }
            catch (Exception ex)
            {
                val = $"<Error: {ex.Message}>";
            }

            var row = CreatePropertyRow(field.Name, val, depth + 1, maxDepth, visited);
            node.Properties.Add(row);
        }

        visited.Remove(obj);
        return node;
    }

    private static ObjectInspectorPropertyRow CreatePropertyRow(
        string name,
        object? val,
        int depth,
        int maxDepth,
        HashSet<object> visited)
    {
        if (val == null)
        {
            return new ObjectInspectorPropertyRow
            {
                Name = name,
                SimpleValueText = "<null>",
                IsNull = true
            };
        }

        var valType = val.GetType();

        // 1. Primitive / String / Scalar
        if (IsScalarType(valType))
        {
            return new ObjectInspectorPropertyRow
            {
                Name = name,
                SimpleValueText = FormatScalarValue(val),
                IsNull = false
            };
        }

        // 2. 1D Array or Collection of Scalars -> Format inline like "[ 1, 34, 45, 235, 25 ]"
        if (val is IEnumerable enumerable && IsCollectionOfScalars(val, out var inlineFormatted))
        {
            return new ObjectInspectorPropertyRow
            {
                Name = name,
                SimpleValueText = inlineFormatted,
                IsNull = false
            };
        }

        // 3. Complex Nested Object
        if (depth <= maxDepth)
        {
            var childNode = BuildNode(val, depth, maxDepth, visited);
            return new ObjectInspectorPropertyRow
            {
                Name = name,
                IsComplexChild = true,
                ChildNode = childNode
            };
        }
        else
        {
            // Max depth reached: create a collapsed placeholder node
            var collapsedNode = new ObjectInspectorNode(GetFormattedTypeName(valType), isExpanded: false, isExpandable: true);
            return new ObjectInspectorPropertyRow
            {
                Name = name,
                IsComplexChild = true,
                ChildNode = collapsedNode
            };
        }
    }

    public static string FormatScalarValue(object val)
    {
        if (val is string s)
        {
            return s;
        }

        if (val is bool b)
        {
            return b ? "true" : "false";
        }

        if (val is double d) return d.ToString("G");
        if (val is float f) return f.ToString("G");
        if (val is decimal m) return m.ToString("G");
        if (val is DateTime dt) return dt.ToString("yyyy-MM-dd HH:mm:ss");
        if (val is TimeSpan ts) return ts.ToString();

        return val.ToString() ?? string.Empty;
    }

    public static bool IsCollectionOfScalars(object obj, out string formatted)
    {
        formatted = string.Empty;
        if (obj is not IEnumerable enumerable || obj is string)
        {
            return false;
        }

        var items = new List<string>();
        int count = 0;

        foreach (var item in enumerable)
        {
            if (count > 50)
            {
                items.Add("...");
                break;
            }

            if (item == null)
            {
                items.Add("<null>");
            }
            else if (IsScalarType(item.GetType()))
            {
                items.Add(FormatScalarValue(item));
            }
            else
            {
                // Contains non-scalar elements; cannot format as simple inline list
                return false;
            }

            count++;
        }

        formatted = "[ " + string.Join(", ", items) + " ]";
        return true;
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

    public static string GetFormattedTypeName(Type type)
    {
        // For Roslyn script classes, FullName is often: "Submission#16+People"
        var fullName = type.FullName ?? type.Name;

        if (fullName.Contains("Submission#"))
        {
            // Extract e.g. "Submission#16+People"
            var parts = fullName.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            var subPart = parts.FirstOrDefault(p => p.Contains("Submission#"));
            if (!string.IsNullOrEmpty(subPart))
            {
                return subPart;
            }
        }

        if (type.IsGenericType)
        {
            var cleanName = type.Name;
            var tickIdx = cleanName.IndexOf('`');
            if (tickIdx > 0) cleanName = cleanName.Substring(0, tickIdx);

            var args = string.Join(", ", type.GetGenericArguments().Select(GetFormattedTypeName));
            return $"{cleanName}<{args}>";
        }

        if (type.IsArray)
        {
            return $"{GetFormattedTypeName(type.GetElementType() ?? typeof(object))}[]";
        }

        return type.Name;
    }
}
