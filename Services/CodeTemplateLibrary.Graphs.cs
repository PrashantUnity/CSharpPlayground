using System.Collections.Generic;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static partial class CodeTemplateLibrary
{
    private static IEnumerable<CodeTemplate> GetGraphTemplates() => new List<CodeTemplate>
    {
        new()
        {
            Id = "dijkstra_shortest_path",
            Title = "Dijkstra's Shortest Path Algorithm",
            Category = "Algorithms",
            Kind = WorkspaceItemKind.Notebook,
            Description = "Finds the shortest path on a weighted directed graph using priority queue relaxation and path reconstruction.",
            IconKind = MaterialIconKind.RayStartArrow,
            AccentColor = "#38bdf8",
            AccentBackground = "#082f49",
            AccentBorder = "#0284c7",
            CategoryBadge = "Graphs • Shortest Path",
            Tags = new List<string> { "Graphs", "Dijkstra", "ShortestPath", "PriorityQueue" },
            Notes = @"# Dijkstra's Shortest Path Algorithm

Visualizes single-source shortest paths on a weighted directed graph.
- Uses `GraphTracker` to record node visits, edge relaxations, and distance updates.
- Drag the scrubber to observe edge relaxations and final shortest path reconstruction.",
            InitialCode = @"// Weighted directed graph: [[u, v, weight], ...]
var edges = ""[[0,1,4],[0,2,2],[1,2,1],[1,3,5],[2,3,8],[2,4,10],[3,4,2],[3,5,6],[4,5,3]]"";
var tracker = GraphTracker.Create(edges, ""Dijkstra's Shortest Path (Source: 0)"", isDirected: true);

// Set source pointer
tracker.SetPointer(""0"", ""src"");
tracker.SetPointer(""5"", ""dest"");

// Adjacency list from graph edges
var adj = new Dictionary<string, List<(string to, double weight)>>();
foreach (var edge in tracker.Graph.Edges)
{
    if (!adj.ContainsKey(edge.FromId)) adj[edge.FromId] = new();
    adj[edge.FromId].Add((edge.ToId, edge.Weight ?? 1.0));
}

// Distance map and previous node for path reconstruction
var dist = new Dictionary<string, double>();
var prev = new Dictionary<string, string>();
foreach (var node in tracker.Graph.Nodes)
{
    dist[node.Id] = double.PositiveInfinity;
    tracker.Annotate(node.Id, ""d=∞"");
}

dist[""0""] = 0;
tracker.Annotate(""0"", ""d=0"");
tracker.Snapshot(""Initialized source vertex 0 with distance 0"");

var pq = new PriorityQueue<string, double>();
tracker.Watch(pq); // shows the queue in dequeue order: vertex (distance)
pq.Enqueue(""0"", 0);

while (pq.TryDequeue(out var u, out var d))
{
    // A vertex can be queued several times; only its smallest entry is current.
    if (d > dist[u]) continue;

    tracker.Visit(u, $""Extracted vertex [{u}] with current shortest distance {d}"", subLabel: $""d={d}"", pointer: ""curr"");

    if (adj.TryGetValue(u, out var neighbors))
    {
        foreach (var (v, w) in neighbors)
        {
            if (d + w < dist[v])
            {
                dist[v] = d + w;
                prev[v] = u;
                pq.Enqueue(v, dist[v]);

                // Record edge relaxation
                tracker.RelaxEdge(u, v, w, dist[v], $""Relaxed edge ({u} -> {v}, weight {w}) • New distance = {dist[v]}"");
            }
        }
    }
}

// Reconstruct path from 0 to 5
var path = new List<string>();
string curr = ""5"";
while (curr != null)
{
    path.Add(curr);
    if (curr == ""0"") break;
    prev.TryGetValue(curr, out curr);
}
path.Reverse();

tracker.ClearPointers();
tracker.MarkPath(path, $""Shortest Path Found: [{string.Join("" -> "", path)}] (Total Cost: {dist[""5""]})"");

Display.Visualizer(tracker);
Console.WriteLine($""Dijkstra complete! Shortest distance to vertex 5 = {dist[""5""]}"");"
        },
        new()
        {
            Id = "leetcode_207_course_schedule",
            Title = "207. Course Schedule (Topological Sort / Kahn's)",
            Category = "Algorithms",
            Kind = WorkspaceItemKind.Notebook,
            Description = "Detects cycles and finds valid course completion order using Kahn's in-degree queue algorithm.",
            IconKind = MaterialIconKind.OrderAlphabeticalAscending,
            AccentColor = "#10b981",
            AccentBackground = "#064e3b",
            AccentBorder = "#059669",
            CategoryBadge = "LeetCode 207 • Medium",
            Tags = new List<string> { "Graphs", "TopologicalSort", "DAG", "Kahns", "CourseSchedule" },
            Notes = @"# 207. Course Schedule

There are `numCourses` courses you have to take, labeled from `0` to `numCourses - 1`.
Some courses have prerequisites. Return whether all courses can be finished.
- Uses `GraphTracker` to visualize in-degree calculation and queue-based topological sort.",
            InitialCode = @"// LeetCode input: prerequisites[i] = [course, prerequisite]
// [1, 0] means ""take course 0 before course 1"", i.e. the directed edge 0 -> 1
int[][] prerequisites = { new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 1 }, new[] { 3, 2 }, new[] { 4, 3 } };
var edges = prerequisites.Select(p => (p[1], p[0]));
var tracker = GraphTracker.Create(edges, ""207. Course Schedule (Topological Sort)"", isDirected: true);

// 1. Calculate in-degrees
var inDegree = new Dictionary<string, int>();
foreach (var node in tracker.Graph.Nodes) inDegree[node.Id] = 0;
foreach (var edge in tracker.Graph.Edges) inDegree[edge.ToId]++;

foreach (var kvp in inDegree)
{
    tracker.Annotate(kvp.Key, $""in={kvp.Value}"");
}
tracker.Snapshot(""Calculated in-degrees for all course vertices"");

// 2. Queue courses with 0 prerequisites
var queue = new Queue<string>();
var order = new List<string>();
tracker.Watch(queue); // both show up under the graph at every step
tracker.Watch(order);
foreach (var kvp in inDegree)
{
    if (kvp.Value == 0)
    {
        queue.Enqueue(kvp.Key);
        tracker.Enqueue(kvp.Key, $""Course [{kvp.Key}] has no prerequisites (in-degree 0)"");
    }
}

while (queue.Count > 0)
{
    var course = queue.Dequeue();
    order.Add(course);
    tracker.Visit(course, $""Taking course [{course}] (Prerequisites fulfilled)"", pointer: ""curr"");

    // Reduce in-degree of outgoing neighbors
    foreach (var edge in tracker.Graph.Edges.Where(e => e.FromId == course))
    {
        inDegree[edge.ToId]--;
        tracker.Annotate(edge.ToId, $""in={inDegree[edge.ToId]}"");

        if (inDegree[edge.ToId] == 0)
        {
            queue.Enqueue(edge.ToId);
            tracker.Enqueue(edge.ToId, $""Course [{edge.ToId}] in-degree dropped to 0 • Ready to take"");
        }
    }
}

bool canFinish = order.Count == tracker.Graph.Nodes.Count;
tracker.ClearPointers();
tracker.Snapshot(canFinish
    ? $""Schedule Valid! Valid order: [{string.Join("", "", order)}]""
    : ""Cycle detected! Impossible to complete all courses."");

Display.Visualizer(tracker);
Console.WriteLine($""Can finish all courses: {canFinish}"");"
        },
        new()
        {
            Id = "leetcode_785_is_graph_bipartite",
            Title = "785. Is Graph Bipartite? (2-Coloring BFS)",
            Category = "Algorithms",
            Kind = WorkspaceItemKind.Notebook,
            Description = "Determines whether an undirected graph can be 2-colored such that no two adjacent vertices share the same color.",
            IconKind = MaterialIconKind.CircleOpacity,
            AccentColor = "#ec4899",
            AccentBackground = "#3b0724",
            AccentBorder = "#be185d",
            CategoryBadge = "LeetCode 785 • Medium",
            Tags = new List<string> { "Graphs", "Bipartite", "BFS", "2Coloring" },
            Notes = @"# 785. Is Graph Bipartite?

Given an undirected graph, return true if and only if it is bipartite.
- Alternates colors (Cyan / Pink) for adjacent vertices.
- Detects odd-length cycles if two neighbors share the same color.",
            InitialCode = @"// Undirected graph edges: [[0,1],[0,3],[1,2],[2,3]]
var edges = ""[[0,1],[0,3],[1,2],[2,3]]"";
var tracker = GraphTracker.Create(edges, ""785. Is Graph Bipartite? (2-Coloring)"", isDirected: false);

var colors = new Dictionary<string, int>(); // 0: Uncolored, 1: Color A (Cyan), 2: Color B (Pink)
foreach (var node in tracker.Graph.Nodes) colors[node.Id] = 0;

bool isBipartite = true;
foreach (var startNode in tracker.Graph.Nodes)
{
    if (colors[startNode.Id] != 0) continue;

    var queue = new Queue<string>();
    queue.Enqueue(startNode.Id);
    colors[startNode.Id] = 1;
    tracker.HighlightNode(startNode.Id, GraphNodeState.Relaxed, $""Colored vertex [{startNode.Label}] with Color A (Cyan)"", subLabel: ""Color: A"");

    while (queue.Count > 0 && isBipartite)
    {
        var u = queue.Dequeue();
        int currColor = colors[u];
        int nextColor = currColor == 1 ? 2 : 1;

        // Find all undirected neighbors
        var neighbors = tracker.Graph.Edges
            .Where(e => e.FromId == u || e.ToId == u)
            .Select(e => e.FromId == u ? e.ToId : e.FromId)
            .Distinct();

        foreach (var v in neighbors)
        {
            if (colors[v] == 0)
            {
                colors[v] = nextColor;
                var state = nextColor == 1 ? GraphNodeState.Relaxed : GraphNodeState.Target;
                string colName = nextColor == 1 ? ""Color A (Cyan)"" : ""Color B (Pink)"";
                tracker.HighlightNode(v, state, $""Colored neighbor [{v}] with {colName}"", subLabel: $""Color: {(nextColor == 1 ? ""A"" : ""B"")}"");
                queue.Enqueue(v);
            }
            else if (colors[v] == currColor)
            {
                tracker.HighlightNode(v, GraphNodeState.Cycle, $""Conflict! Adjacent vertices [{u}] and [{v}] have same color!"", subLabel: ""Conflict!"");
                tracker.HighlightEdge(u, v, GraphEdgeState.Rejected, $""Conflicting edge ({u} - {v})"");
                isBipartite = false;
                break;
            }
        }
    }
}

tracker.ClearPointers();
tracker.Snapshot(isBipartite ? ""Graph is Bipartite (2-colorable)!"" : ""Graph is NOT Bipartite (Odd cycle detected)!"");

Display.Visualizer(tracker);
Console.WriteLine($""Is Bipartite: {isBipartite}"");"
        }
    };
}
