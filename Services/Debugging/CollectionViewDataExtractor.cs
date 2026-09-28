using System;
using System.Collections.Generic;
using System.Linq;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;

public static class CollectionViewDataExtractor
{
    public static (List<string> Headers, List<List<string>> Rows) Extract(DebugVariableItem item)
    {
        var headers = new List<string>();
        var allRows = new List<List<string>>();

        if (item.Children.Count == 0)
        {
            headers.Add("#");
            headers.Add("Value");
            allRows.Add(new List<string> { "0", item.ValueDisplay });
            return (headers, allRows);
        }

        // Check if 2D array items with names like "[0, 0]", "[0, 1]"
        bool is2DMatrix = item.Children.All(c => c.Name.Contains(',') && c.Name.StartsWith('[') && c.Name.EndsWith(']'));
        if (is2DMatrix)
        {
            var coords = new List<(int row, int col, string val)>();
            int maxCol = 0;
            foreach (var child in item.Children)
            {
                var clean = child.Name.Trim('[', ']');
                var parts = clean.Split(',');
                if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out int r) && int.TryParse(parts[1].Trim(), out int c))
                {
                    coords.Add((r, c, child.ValueDisplay.Trim('"')));
                    if (c > maxCol) maxCol = c;
                }
            }

            headers.Add("#");
            for (int c = 0; c <= maxCol; c++)
            {
                headers.Add(c.ToString());
            }

            var grouped = coords.GroupBy(x => x.row).OrderBy(g => g.Key);
            foreach (var group in grouped)
            {
                var rowList = new List<string> { group.Key.ToString() };
                for (int c = 0; c <= maxCol; c++)
                {
                    var match = group.FirstOrDefault(x => x.col == c);
                    rowList.Add(match.val ?? string.Empty);
                }
                allRows.Add(rowList);
            }
            return (headers, allRows);
        }

        // Check if collection of objects with property children
        var firstWithProps = item.Children.FirstOrDefault(c => c.Children.Count > 0);
        if (firstWithProps != null)
        {
            headers.Add("#");
            foreach (var prop in firstWithProps.Children)
            {
                headers.Add(prop.Name);
            }

            int rowIdx = 0;
            foreach (var child in item.Children)
            {
                var rowList = new List<string> { rowIdx.ToString() };
                for (int h = 1; h < headers.Count; h++)
                {
                    string propName = headers[h];
                    var p = child.Children.FirstOrDefault(c => string.Equals(c.Name, propName, StringComparison.Ordinal));
                    rowList.Add(p?.ValueDisplay.Trim('"') ?? string.Empty);
                }
                allRows.Add(rowList);
                rowIdx++;
            }
            return (headers, allRows);
        }

        // 1D collection of values
        headers.Add("#");
        headers.Add("Value");
        int idx = 0;
        foreach (var child in item.Children)
        {
            string rowNum = child.Name.Trim('[', ']');
            if (!int.TryParse(rowNum, out _)) rowNum = idx.ToString();
            allRows.Add(new List<string> { rowNum, child.ValueDisplay.Trim('"') });
            idx++;
        }

        return (headers, allRows);
    }
}
