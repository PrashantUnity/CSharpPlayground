using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

public static class GraphDataParser
{
    public static GraphData Parse(object? input, bool isDirected = true)
    {
        var graph = new GraphData { IsDirected = isDirected };
        if (input == null) return graph;

        if (input is GraphData alreadyGraph) return alreadyGraph;

        // Case 0: String representation (LeetCode array format, dash format, tuple format)
        if (input is string str)
        {
            return ParseString(str, isDirected);
        }

        var nodeMap = new Dictionary<string, GraphNodeData>();
        var seen = new HashSet<(string, string, double?)>();

        GraphNodeData GetOrAddNode(string id, string? label = null)
        {
            if (!nodeMap.TryGetValue(id, out var node))
            {
                node = new GraphNodeData(id, label ?? id);
                nodeMap[id] = node;
                graph.Nodes.Add(node);
            }
            return node;
        }

        // Case 1: IDictionary (Adjacency List e.g. Dictionary<string, List<string>>)
        if (input is IDictionary dict)
        {
            foreach (DictionaryEntry entry in dict)
            {
                string u = entry.Key?.ToString() ?? string.Empty;
                GetOrAddNode(u);

                if (entry.Value is IEnumerable neighbors)
                {
                    foreach (var vObj in neighbors)
                    {
                        string v = vObj?.ToString() ?? string.Empty;
                        GetOrAddNode(v);
                        AddEdge(graph, seen, u, v, null);
                    }
                }
            }
            return graph;
        }

        // Case 2: 2D Matrix (Adjacency Matrix)
        if (input is Array array2D && array2D.Rank == 2)
        {
            int n = array2D.GetLength(0);
            int m = array2D.GetLength(1);
            for (int i = 0; i < n; i++) GetOrAddNode(i.ToString());

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < m; j++)
                {
                    object? val = array2D.GetValue(i, j);
                    if (val is int weightInt && weightInt != 0)
                    {
                        GetOrAddNode(j.ToString());
                        AddEdge(graph, seen, i.ToString(), j.ToString(), weightInt);
                    }
                    else if (val is double weightDbl && Math.Abs(weightDbl) > 1e-9)
                    {
                        GetOrAddNode(j.ToString());
                        AddEdge(graph, seen, i.ToString(), j.ToString(), weightDbl);
                    }
                }
            }
            return graph;
        }

        // Case 3: IEnumerable of Edge Tuples or arrays
        if (input is IEnumerable edgeCollection)
        {
            foreach (var item in edgeCollection)
            {
                if (item == null) continue;
                var type = item.GetType();

                // Check for ValueTuple (Item1, Item2, [Item3])
                var f1 = type.GetField("Item1");
                var f2 = type.GetField("Item2");
                var f3 = type.GetField("Item3");

                if (f1 != null && f2 != null)
                {
                    string u = f1.GetValue(item)?.ToString() ?? string.Empty;
                    string v = f2.GetValue(item)?.ToString() ?? string.Empty;
                    double? w = null;
                    if (f3 != null)
                    {
                        var wObj = f3.GetValue(item);
                        if (wObj != null && double.TryParse(wObj.ToString(), out double parsedW))
                        {
                            w = parsedW;
                        }
                    }
                    GetOrAddNode(u);
                    GetOrAddNode(v);
                    AddEdge(graph, seen, u, v, w);
                    continue;
                }

                // Check for array like new[] { 0, 1 } or new[] { 0, 1, 5 }
                if (item is IList list && list.Count >= 2)
                {
                    string u = list[0]?.ToString() ?? string.Empty;
                    string v = list[1]?.ToString() ?? string.Empty;
                    double? w = null;
                    if (list.Count >= 3 && double.TryParse(list[2]?.ToString(), out double pw))
                    {
                        w = pw;
                    }
                    GetOrAddNode(u);
                    GetOrAddNode(v);
                    AddEdge(graph, seen, u, v, w);
                }
            }
        }

        return graph;
    }

    public static GraphData ParseString(string str, bool isDirected = true)
    {
        var graph = new GraphData { IsDirected = isDirected };
        if (string.IsNullOrWhiteSpace(str)) return graph;

        var nodeMap = new Dictionary<string, GraphNodeData>();
        var seen = new HashSet<(string, string, double?)>();
        GraphNodeData GetOrAddNode(string id)
        {
            if (!nodeMap.TryGetValue(id, out var node))
            {
                node = new GraphNodeData(id, id);
                nodeMap[id] = node;
                graph.Nodes.Add(node);
            }
            return node;
        }

        // Format 1: [[0,1,4],[0,2,2]] or [[0,1],[1,2]]
        var matches = Regex.Matches(str, @"\[([^\[\]]+)\]");
        if (matches.Count > 0)
        {
            foreach (Match m in matches)
            {
                var content = m.Groups[1].Value.Trim();
                var parts = content.Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length >= 2)
                {
                    string u = parts[0].Trim('"', '\'');
                    string v = parts[1].Trim('"', '\'');
                    double? w = null;
                    if (parts.Length >= 3 && double.TryParse(parts[2], out double parsedW))
                    {
                        w = parsedW;
                    }

                    GetOrAddNode(u);
                    GetOrAddNode(v);
                    AddEdge(graph, seen, u, v, w);
                }
            }
            return graph;
        }

        // Format 2: "0-1, 1-2, 2-0" or "A->B, B->C"
        var segments = str.Split(',', StringSplitOptions.TrimEntries);
        foreach (var seg in segments)
        {
            string clean = seg.Replace("->", "-").Trim('(', ')');
            var tokens = clean.Split(new[] { '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length >= 2)
            {
                string u = tokens[0].Trim();
                string v = tokens[1].Trim();
                double? w = null;
                if (tokens.Length >= 3 && double.TryParse(tokens[2], out double pw))
                {
                    w = pw;
                }

                GetOrAddNode(u);
                GetOrAddNode(v);
                AddEdge(graph, seen, u, v, w);
            }
        }

        return graph;
    }

    // An undirected edge is unordered: {0: [1], 1: [0]} or a symmetric matrix describes a single edge.
    private static void AddEdge(GraphData graph, HashSet<(string, string, double?)> seen, string u, string v, double? weight)
    {
        if (!graph.IsDirected)
        {
            var key = string.CompareOrdinal(u, v) <= 0 ? (u, v, weight) : (v, u, weight);
            if (!seen.Add(key)) return;
        }

        graph.Edges.Add(new GraphEdgeData(u, v, weight, graph.IsDirected));
    }

    public static string ToLeetCodeString(GraphData graph)
    {
        var items = new List<string>();
        foreach (var edge in graph.Edges)
        {
            if (edge.Weight.HasValue)
                items.Add($"[{edge.FromId},{edge.ToId},{edge.Weight.Value}]");
            else
                items.Add($"[{edge.FromId},{edge.ToId}]");
        }
        return "[" + string.Join(",", items) + "]";
    }
}
