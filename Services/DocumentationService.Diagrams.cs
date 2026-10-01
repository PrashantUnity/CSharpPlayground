using System.Collections.Generic;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public partial class DocumentationService
{
    private DocCategory BuildDiagramsAndVisualizationsCategory()
    {
        return new DocCategory
        {
            Id = "diagrams_and_visualizations",
            Title = "Diagrams & Visualizations API",
            IconKind = MaterialIconKind.VectorPolyline,
            AccentColor = "#38BDF8",
            Badge = "Polyglot Diagrams",
            Description = "Create interactive diagrams, data structure visualizers, 2D/3D plots, and custom vector scenes across C++, Java, Python, and C#.",
            Articles = new List<DocArticle>
            {
                CreateDiagramsQuickstartArticle(),
                CreateDiagramsTreesArticle(),
                CreateDiagramsGraphsArticle(),
                CreateDiagramsMatricesArticle(),
                CreateDiagrams3DMatrixArticle(),
                CreateDiagramsVectorCanvasArticle(),
                CreateDiagrams3DSurfacesArticle()
            }
        };
    }

    private DocArticle CreateDiagramsQuickstartArticle()
    {
        return new DocArticle
        {
            Id = "diagrams_quickstart",
            Title = "Diagrams & Visualizations API Quickstart",
            Subtitle = "Call our canonical API to emit interactive diagrams, charts, and models across all languages.",
            ReadingTime = "4 min read",
            Summary = "Our unified API allows you to create interactive visual diagrams directly from code in C++, Java, Python, and C#. Every diagram supports pan, zoom, fullscreen popouts, vector PNG export, and CSV data copying.",
            Keywords = new List<string> { "diagram", "diagrams", "visualize", "visualization", "quickstart", "cpp", "java", "python", "csharp", "api" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "One Unified Diagram Runtime",
                    Content = "Regardless of the programming language, calling our API emits rich interactive controls in the Results dock tab and notebook cells. Diagrams are rendered using hardware-accelerated vectors with responsive pan/zoom gestures.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "Click any language tab in the code snippet below to see how to call our API in C++, Java, Python, or C#."
                },
                new()
                {
                    Heading = "Diagram Capabilities",
                    Content = "Our visualization API provides first-class support for:\n• Hierarchical Tree Diagrams (BSTs, N-ary trees, Tries)\n• Graph Network Topologies (Directed, Undirected, Weighted)\n• 2D Grids, Matrices & Island Traversal Diagrams\n• Custom 2D Vector Canvas (Rectangles, Arrows, Labels)\n• 2D Series Charts & 3D Interactive Surfaces"
                }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new DocCodeSnippet
                {
                    Id = "snip_diagram_quickstart",
                    Title = "Codes [C++ | Java | Python3 | C#] : Quickstart Diagram with Comments",
                    Description = "Call our API to emit an interactive visual diagram with customized titles and data series.",
                    TargetKind = WorkspaceItemKind.Script
                }
                .AddVariant("cpp", "C++", """
                    // C++: Quickstart Visual Diagram with our API
                    #include <fry_display.hpp>
                    #include <vector>

                    int main() {
                        // 1. Prepare data points for the diagram
                        std::vector<int> numbers = {10, 25, 45, 30, 60, 85};

                        // 2. Call our API to emit an interactive bar chart diagram
                        fry::Display::chart(numbers, "Quarterly Trend Analysis");
                        return 0;
                    }
                    """)
                .AddVariant("java", "Java", """
                    // Java: Quickstart Visual Diagram with our API
                    import com.frypdf.display.Display;

                    public class Solution {
                        public static void main(String[] args) {
                            // 1. Prepare data points for the diagram
                            int[] numbers = {10, 25, 45, 30, 60, 85};

                            // 2. Call our API to emit an interactive bar chart diagram
                            Display.chart(numbers, "Quarterly Trend Analysis");
                        }
                    }
                    """)
                .AddVariant("python", "Python3", """
                    # Python3: Quickstart Visual Diagram with our API
                    from fry_display import Display

                    # 1. Prepare data points for the diagram
                    numbers = [10, 25, 45, 30, 60, 85]

                    # 2. Call our API to emit an interactive bar chart diagram
                    Display.chart(numbers, title="Quarterly Trend Analysis", chart_type="bar")
                    """)
                .AddVariant("csharp", "C#", """
                    // C#: Quickstart Visual Diagram with our API
                    var numbers = new[] { 10, 25, 45, 30, 60, 85 };

                    // Call our API to emit an interactive bar chart diagram
                    Display.Chart(numbers, title: "Quarterly Trend Analysis", type: ChartType.Bar);
                    """)
                .AddVariant("rust", "Rust", """
                    // Rust: Quickstart Visual Diagram with our API
                    use fry::*;

                    fn main() {
                        // 1. Prepare data points for the diagram
                        let numbers = vec![10, 25, 45, 30, 60, 85];

                        // 2. Call our API to emit an interactive bar chart diagram
                        bar_chart(&numbers).title("Quarterly Trend Analysis").show();
                    }
                    """)
                .AddVariant("go", "Go", """
                    // Go: Quickstart Visual Diagram with our API
                    package main

                    import "fry"

                    func main() {
                        // 1. Prepare data points for the diagram
                        numbers := []int{10, 25, 45, 30, 60, 85}

                        // 2. Call our API to emit an interactive bar chart diagram
                        fry.BarChart(numbers, fry.Title("Quarterly Trend Analysis"))
                    }
                    """)
                .AddVariant("javascript", "JavaScript", """
                    // JavaScript: Quickstart Visual Diagram with our API
                    const { Display } = require('fry');

                    // 1. Prepare data points for the diagram
                    const numbers = [10, 25, 45, 30, 60, 85];

                    // 2. Call our API to emit an interactive bar chart diagram
                    Display.barChart(numbers, "Quarterly Trend Analysis");
                    """)
                .AddVariant("fsharp", "F#", """
                    // F#: Quickstart Visual Diagram with our API
                    open Fry

                    // 1. Prepare data points for the diagram
                    let numbers = [ 10; 25; 45; 30; 60; 85 ]

                    // 2. Call our API to emit an interactive bar chart diagram
                    Display.BarChart(numbers, title = "Quarterly Trend Analysis") |> ignore
                    """)
            }
        };
    }

    private DocArticle CreateDiagramsTreesArticle()
    {
        return new DocArticle
        {
            Id = "diagrams_trees",
            Title = "Binary Trees & Hierarchy Diagrams",
            Subtitle = "Render binary search trees, N-ary trees, and tries with Buchheim automated layout.",
            ReadingTime = "5 min read",
            Summary = "Display clean, non-overlapping hierarchical tree diagrams. Our API automatically discovers tree pointers (left/right, children) and renders interactive nodes with traversal highlighting.",
            Keywords = new List<string> { "tree", "bst", "diagram", "buchheim", "hierarchy", "traversal", "nodes" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Automated Tree Layout (Buchheim Algorithm)",
                    Content = "Our API implements the Buchheim tree layout algorithm to ensure parent nodes are perfectly centered above their children with uniform horizontal spacing. Nodes never overlap, even on deep or unbalanced trees."
                },
                new()
                {
                    Heading = "Step-by-Step Traversal Highlighting",
                    Content = "When recording traversals (In-Order, Pre-Order, BFS), pass the active node identifier to light up nodes step-by-step as your algorithm executes.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "You can zoom and pan tree diagrams using the mouse wheel and drag gestures, or click 'Fit to View' to keep the whole tree visible."
                }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new DocCodeSnippet
                {
                    Id = "snip_diagram_trees",
                    Title = "Codes [C++ | Java | Python3 | C#] : Binary Search Tree Diagram",
                    Description = "Build and visualize a binary search tree step-by-step using our API.",
                    TargetKind = WorkspaceItemKind.Script
                }
                .AddVariant("cpp", "C++", """
                    // C++: Hierarchical Binary Search Tree Diagram
                    #include <fry_display.hpp>

                    int main() {
                        // 1. Create a tree visualizer using our API
                        fry::TreeVisualizer tree("Binary Search Tree Diagram");

                        // 2. Insert values to build the tree diagram
                        tree.insert(50);
                        tree.insert(30);
                        tree.insert(70);
                        tree.insert(20);
                        tree.insert(40);

                        // 3. Call our API to render the interactive diagram
                        tree.show();
                        return 0;
                    }
                    """)
                .AddVariant("java", "Java", """
                    // Java: Hierarchical Binary Search Tree Diagram
                    import com.frypdf.display.Visualizer;

                    public class Solution {
                        static class TreeNode {
                            int val;
                            TreeNode left, right;
                            TreeNode(int v) { this.val = v; }
                        }

                        public static void main(String[] args) {
                            // 1. Construct binary tree nodes
                            TreeNode root = new TreeNode(50);
                            root.left = new TreeNode(30);
                            root.right = new TreeNode(70);
                            root.left.left = new TreeNode(20);
                            root.left.right = new TreeNode(40);

                            // 2. Call our API to display the interactive tree diagram
                            Visualizer.tree(root).title("Binary Search Tree Diagram").show();
                        }
                    }
                    """)
                .AddVariant("python", "Python3", """
                    # Python3: Hierarchical Binary Search Tree Diagram
                    from fry_display import Display

                    class TreeNode:
                        def __init__(self, val, left=None, right=None):
                            self.val = val
                            self.left = left
                            self.right = right

                    # 1. Construct binary tree nodes
                    root = TreeNode(50,
                        left=TreeNode(30, left=TreeNode(20), right=TreeNode(40)),
                        right=TreeNode(70))

                    # 2. Call our API to display the interactive tree diagram
                    Display.tree(root, title="Binary Search Tree Diagram")
                    """)
                .AddVariant("csharp", "C#", """
                    // C#: Hierarchical Binary Search Tree Diagram
                    public class TreeNode
                    {
                        public int Val { get; set; }
                        public TreeNode? Left { get; set; }
                        public TreeNode? Right { get; set; }
                        public TreeNode(int val) => Val = val;
                    }

                    // 1. Construct binary tree nodes
                    var root = new TreeNode(50)
                    {
                        Left = new TreeNode(30) { Left = new TreeNode(20), Right = new TreeNode(40) },
                        Right = new TreeNode(70)
                    };

                    // 2. Call our API to display the interactive tree diagram
                    Display.Tree(root, title: "Binary Search Tree Diagram");
                    """)
                .AddVariant("rust", "Rust", """
                    // Rust: Hierarchical Binary Search Tree Diagram
                    use fry::*;

                    fn main() {
                        // 1. Construct binary tree nodes
                        let left = TreeNode::with_children(30, TreeNode::new(20), TreeNode::new(40));
                        let right = TreeNode::new(70);
                        let root = TreeNode::with_children(50, left, right);

                        // 2. Call our API to display the interactive tree diagram
                        tree(&root).title("Binary Search Tree Diagram").show();
                    }
                    """)
                .AddVariant("go", "Go", """
                    // Go: Hierarchical Binary Search Tree Diagram
                    package main

                    import "fry"

                    type TreeNode struct {
                        Val   int
                        Left  *TreeNode
                        Right *TreeNode
                    }

                    func main() {
                        // 1. Construct binary tree nodes
                        root := &TreeNode{
                            Val: 50,
                            Left: &TreeNode{
                                Val:   30,
                                Left:  &TreeNode{Val: 20},
                                Right: &TreeNode{Val: 40},
                            },
                            Right: &TreeNode{Val: 70},
                        }

                        // 2. Call our API to display the interactive tree diagram
                        fry.Tree(root, fry.Title("Binary Search Tree Diagram"))
                    }
                    """)
                .AddVariant("javascript", "JavaScript", """
                    // JavaScript: Hierarchical Binary Search Tree Diagram
                    const { Display } = require('fry');

                    class TreeNode {
                        constructor(val, left = null, right = null) {
                            this.val = val;
                            this.left = left;
                            this.right = right;
                        }
                    }

                    // 1. Construct binary tree nodes
                    const root = new TreeNode(50,
                        new TreeNode(30, new TreeNode(20), new TreeNode(40)),
                        new TreeNode(70)
                    );

                    // 2. Call our API to display the interactive tree diagram
                    Display.tree(root, "Binary Search Tree Diagram");
                    """)
                .AddVariant("fsharp", "F#", """
                    // F#: Hierarchical Binary Search Tree Diagram
                    open Fry

                    // 1. Construct binary tree nodes
                    let root = TreeNode(50,
                        TreeNode(30, TreeNode(20), TreeNode(40)),
                        TreeNode(70))

                    // 2. Call our API to display the interactive tree diagram
                    Display.Tree(root, title = "Binary Search Tree Diagram") |> ignore
                    """)
            }
        };
    }

    private DocArticle CreateDiagramsGraphsArticle()
    {
        return new DocArticle
        {
            Id = "diagrams_graphs",
            Title = "Graph Network & Topology Diagrams",
            Subtitle = "Render directed/undirected graphs, circular topologies, and weighted edges.",
            ReadingTime = "5 min read",
            Summary = "Draw service architectures, dependency graphs, and state machines. Features circular auto-layout, arrow markers for directed edges, edge weight badges, and component clusters.",
            Keywords = new List<string> { "graph", "network", "diagram", "topology", "directed", "edges", "dijkstra", "bfs" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Circular & Adjacency List Topologies",
                    Content = "Pass adjacency maps, edge pairs, or custom node structures. Our API organizes nodes along circular or spring-loaded coordinates, drawing connection arrows and weight badges automatically."
                },
                new()
                {
                    Heading = "Path & Component Coloring",
                    Content = "Highlight active traversals (e.g. shortest path, visited set) by passing node IDs and edge tuples. Nodes glow with distinct component colors.",
                    CalloutType = DocCalloutType.Info,
                    CalloutText = "Hover over any node in the diagram to inspect its incident edges, in-degree, and connected neighbors."
                }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new DocCodeSnippet
                {
                    Id = "snip_diagram_graphs",
                    Title = "Codes [C++ | Java | Python3 | C#] : Directed Network Flow Diagram",
                    Description = "Define nodes, connection arrows, and display an architecture flow diagram.",
                    TargetKind = WorkspaceItemKind.Script
                }
                .AddVariant("cpp", "C++", """
                    // C++: Directed Network Graph Diagram
                    #include <fry_display.hpp>

                    int main() {
                        // 1. Initialize graph diagram using our API
                        fry::GraphVisualizer graph("Microservice Architecture Flow", /*directed=*/true);

                        // 2. Add connection edges between services
                        graph.add_edge("API Gateway", "Auth Service");
                        graph.add_edge("API Gateway", "Billing Service");
                        graph.add_edge("Billing Service", "Postgres DB");
                        graph.add_edge("Auth Service", "Redis Cache");

                        // 3. Call our API to display the interactive diagram
                        graph.show();
                        return 0;
                    }
                    """)
                .AddVariant("java", "Java", """
                    // Java: Directed Network Graph Diagram
                    import com.frypdf.display.Visualizer;
                    import java.util.*;

                    public class Solution {
                        public static void main(String[] args) {
                            // 1. Define network adjacency list
                            Map<String, List<String>> network = new LinkedHashMap<>();
                            network.put("API Gateway", List.of("Auth Service", "Billing Service"));
                            network.put("Auth Service", List.of("Redis Cache"));
                            network.put("Billing Service", List.of("Postgres DB"));

                            // 2. Call our API to display the interactive graph diagram
                            Visualizer.graph(network).directed(true).title("Microservice Architecture Flow").show();
                        }
                    }
                    """)
                .AddVariant("python", "Python3", """
                    # Python3: Directed Network Graph Diagram
                    from fry_display import Display

                    # 1. Define network adjacency dictionary
                    network = {
                        "API Gateway": ["Auth Service", "Billing Service"],
                        "Auth Service": ["Redis Cache"],
                        "Billing Service": ["Postgres DB"]
                    }

                    # 2. Call our API to display the interactive graph diagram
                    Display.graph(network, directed=True, title="Microservice Architecture Flow")
                    """)
                .AddVariant("csharp", "C#", """
                    // C#: Directed Network Graph Diagram
                    var network = new Dictionary<string, List<string>>
                    {
                        ["API Gateway"] = new() { "Auth Service", "Billing Service" },
                        ["Auth Service"] = new() { "Redis Cache" },
                        ["Billing Service"] = new() { "Postgres DB" }
                    };

                    // Call our API to display the interactive graph diagram
                    Display.Graph(network, directed: true, title: "Microservice Architecture Flow");
                    """)
                .AddVariant("rust", "Rust", """
                    // Rust: Directed Network Graph Diagram
                    use fry::*;
                    use std::collections::BTreeMap;

                    fn main() {
                        // 1. Define network adjacency map
                        let mut network = BTreeMap::new();
                        network.insert("API Gateway", vec!["Auth Service", "Billing Service"]);
                        network.insert("Auth Service", vec!["Redis Cache"]);
                        network.insert("Billing Service", vec!["Postgres DB"]);

                        // 2. Call our API to display the interactive graph diagram
                        graph(&network).title("Microservice Architecture Flow").show();
                    }
                    """)
                .AddVariant("go", "Go", """
                    // Go: Directed Network Graph Diagram
                    package main

                    import "fry"

                    func main() {
                        // 1. Define network adjacency map
                        network := map[string][]string{
                            "API Gateway":     {"Auth Service", "Billing Service"},
                            "Auth Service":    {"Redis Cache"},
                            "Billing Service": {"Postgres DB"},
                        }

                        // 2. Call our API to display the interactive graph diagram
                        fry.Graph(network, fry.Title("Microservice Architecture Flow"))
                    }
                    """)
                .AddVariant("javascript", "JavaScript", """
                    // JavaScript: Directed Network Graph Diagram
                    const { Display } = require('fry');

                    // 1. Define network adjacency object
                    const network = {
                        "API Gateway": ["Auth Service", "Billing Service"],
                        "Auth Service": ["Redis Cache"],
                        "Billing Service": ["Postgres DB"]
                    };

                    // 2. Call our API to display the interactive graph diagram
                    Display.graph(network, "Microservice Architecture Flow");
                    """)
                .AddVariant("fsharp", "F#", """
                    // F#: Directed Network Graph Diagram
                    open Fry

                    // 1. Define network adjacency list
                    let network = [
                        "API Gateway", box [ "Auth Service"; "Billing Service" ]
                        "Auth Service", box [ "Redis Cache" ]
                        "Billing Service", box [ "Postgres DB" ]
                    ]

                    // 2. Call our API to display the interactive graph diagram
                    Display.Graph(network, title = "Microservice Architecture Flow") |> ignore
                    """)
            }
        };
    }
}
