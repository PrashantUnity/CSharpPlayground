using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;

public sealed partial class JavaDebugSession
{
    public async Task<IReadOnlyList<DebugVariableItem>> GetVariableChildrenAsync(DebugVariableItem parent, CancellationToken ct = default)
    {
        if (parent == null) return Array.Empty<DebugVariableItem>();

        if (parent.ChildrenLoaded && parent.Children.Count > 0)
        {
            return parent.Children;
        }

        string target = !string.IsNullOrEmpty(parent.PathExpression) ? parent.PathExpression : parent.Name;
        if (string.IsNullOrWhiteSpace(target)) return Array.Empty<DebugVariableItem>();

        parent.IsLoadingChildren = true;
        try
        {
            var output = await SendCommandWaitPromptAsync($"dump {target}").ConfigureAwait(false);
            var children = ParseDumpOutput(output, parent);

            parent.Children.Clear();
            foreach (var child in children)
            {
                parent.Children.Add(child);
            }
            parent.HasChildren = parent.Children.Count > 0;
            parent.ChildrenLoaded = true;
            return parent.Children;
        }
        catch
        {
            parent.HasChildren = false;
            parent.ChildrenLoaded = true;
            return Array.Empty<DebugVariableItem>();
        }
        finally
        {
            parent.IsLoadingChildren = false;
        }
    }

    private static IReadOnlyList<DebugVariableItem> ParseDumpOutput(string text, DebugVariableItem parent)
    {
        var children = new List<DebugVariableItem>();
        if (string.IsNullOrWhiteSpace(text)) return children;

        int openBrace = text.IndexOf('{');
        int closeBrace = text.LastIndexOf('}');
        if (openBrace >= 0 && closeBrace > openBrace)
        {
            var inner = text.Substring(openBrace + 1, closeBrace - openBrace - 1).Trim();
            if (parent.IsCollection || parent.TypeName.EndsWith("[]") || parent.TypeName.Contains("["))
            {
                // Parse comma or newline separated array items
                var rawItems = inner.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                for (int i = 0; i < rawItems.Length; i++)
                {
                    var itemText = rawItems[i].Trim();
                    if (string.IsNullOrEmpty(itemText)) continue;
                    var itemVar = CreateJavaVariableItem($"[{i}]", itemText, $"{parent.PathExpression}[{i}]", "CollectionItem");
                    children.Add(itemVar);
                }
                return children;
            }

            // Parse key-value fields: fieldName: fieldValue
            var lines = inner.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var line in lines)
            {
                int colonIdx = line.IndexOf(':');
                if (colonIdx <= 0) continue;

                var fieldName = line.Substring(0, colonIdx).Trim();
                var fieldValue = line.Substring(colonIdx + 1).Trim();

                if (string.IsNullOrEmpty(fieldName)) continue;

                var childPath = $"{parent.PathExpression}.{fieldName}";
                var childVar = CreateJavaVariableItem(fieldName, fieldValue, childPath, "Property");
                children.Add(childVar);
            }
        }

        return children;
    }

    private static IReadOnlyList<DebugVariableItem> ParseLocals(string text)
    {
        var locals = new List<DebugVariableItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var matches = VarRegex().Matches(text);
        foreach (Match m in matches)
        {
            var name = m.Groups["name"].Value.Trim();
            var val = m.Groups["val"].Value.Trim();

            if (name.Equals("main", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("thread", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("[cmd_", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Method", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Local", StringComparison.OrdinalIgnoreCase) ||
                !seen.Add(name))
            {
                continue;
            }

            locals.Add(CreateJavaVariableItem(name, val, name, "Local"));
        }
        return locals;
    }

    private static DebugVariableItem CreateJavaVariableItem(string name, string val, string path, string nodeKind)
    {
        var typeName = "object";
        bool isCollection = false;
        int? collectionCount = null;
        bool hasChildren = false;
        string displayVal = val;

        if (val.StartsWith("instance of ", StringComparison.OrdinalIgnoreCase))
        {
            var rawType = val.Substring("instance of ".Length).Split(' ')[0];
            typeName = rawType.Replace('$', '.');
            hasChildren = true;

            var arrayMatch = Regex.Match(typeName, @"\[(?<count>\d+)\]");
            if (arrayMatch.Success)
            {
                isCollection = true;
                if (int.TryParse(arrayMatch.Groups["count"].Value, out int count))
                {
                    collectionCount = count;
                    displayVal = count == 0 ? "[]" : $"{typeName} (len: {count})";
                }
            }
            else if (typeName.Contains("List") || typeName.Contains("Map") || typeName.Contains("Set"))
            {
                isCollection = true;
            }
        }
        else if (int.TryParse(val, out _))
        {
            typeName = "int";
        }
        else if (double.TryParse(val, out _))
        {
            typeName = "double";
        }
        else if (bool.TryParse(val, out _))
        {
            typeName = "boolean";
        }
        else if (val.StartsWith("\"") && val.EndsWith("\""))
        {
            typeName = "String";
        }

        bool isText = typeName.Equals("String", StringComparison.OrdinalIgnoreCase)
                      || (!string.IsNullOrEmpty(val) && (val.Length > 20 || val.Contains('\n') || val.StartsWith('{')));

        return new DebugVariableItem
        {
            Name = name,
            TypeName = typeName,
            ValueDisplay = displayVal,
            PathExpression = path,
            Kind = "Local",
            NodeKind = nodeKind,
            HasChildren = hasChildren,
            ChildrenLoaded = false,
            IsCollection = isCollection,
            CollectionItemCount = collectionCount,
            IsTextOrStructured = isText
        };
    }
}
