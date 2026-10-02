using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Documentation;

public partial class DocumentationService
{
    private DocArticle CreateDiagramsMatricesArticle()
    {
        return new DocArticle
        {
            Id = "diagrams_matrices",
            Title = "2D Grid, Matrix & Island Diagrams",
            Subtitle = "Step through 2D grids, flood-fills, and dynamic programming tables.",
            ReadingTime = "5 min read",
            Summary = "Render 2D arrays as interactive coordinate grids with value tiles, row/column labels, and cell status highlights (Active, Visited, Blocked, Target).",
            Keywords = new List<string> { "grid", "matrix", "diagram", "islands", "dp", "bfs", "flood fill" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Interactive Grid Coordinates & Values",
                    Content = "Each cell displays its value and (row, col) coordinates. Cells support color-coded states: water vs land, obstacle walls vs passable paths, or DP memoization weights."
                },
                new()
                {
                    Heading = "One-Line Island Traversal",
                    Content = "Calling Display.islands(...) or Display.Islands(...) automatically executes BFS flood-fill and generates an interactive playback scrubber stepping through each discovered archipelago.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "Double-click any grid diagram to toggle between fitted view and 100% actual-size resolution."
                }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new DocCodeSnippet
                {
                    Id = "snip_diagram_matrices",
                    Title = "Codes [C++ | Java | Python3 | C#] : Grid & Island Diagram",
                    Description = "Visualize 2D grids, matrix traversals, and flood-fills with our API.",
                    TargetKind = WorkspaceItemKind.Script
                }
                .AddVariant("cpp", "C++", """
                    // C++: 2D Grid & Island Diagram
                    #include <fry_display.hpp>
                    #include <vector>

                    int main() {
                        // 1. Define 2D matrix map
                        std::vector<std::vector<int>> grid = {
                            {1, 1, 0, 0},
                            {1, 1, 0, 1},
                            {0, 0, 1, 1}
                        };

                        // 2. Call our API to display interactive island diagram
                        fry::Display::islands(grid, "Archipelago Island Traversal");
                        return 0;
                    }
                    """)
                .AddVariant("java", "Java", """
                    // Java: 2D Grid & Island Diagram
                    import com.frypdf.display.Visualizer;

                    public class Solution {
                        public static void main(String[] args) {
                            // 1. Define 2D matrix map
                            int[][] grid = {
                                {1, 1, 0, 0},
                                {1, 1, 0, 1},
                                {0, 0, 1, 1}
                            };

                            // 2. Call our API to display interactive grid diagram
                            Visualizer.grid(grid).title("Archipelago Island Traversal").show();
                        }
                    }
                    """)
                .AddVariant("python", "Python3", """
                    # Python3: 2D Grid & Island Diagram
                    from fry_display import Display

                    # 1. Define 2D matrix map
                    grid = [
                        [1, 1, 0, 0],
                        [1, 1, 0, 1],
                        [0, 0, 1, 1]
                    ]

                    # 2. Call our API to display interactive island diagram
                    Display.islands(grid, title="Archipelago Island Traversal")
                    """)
                .AddVariant("csharp", "C#", """
                    // C#: 2D Grid & Island Diagram
                    int[,] grid = {
                        { 1, 1, 0, 0 },
                        { 1, 1, 0, 1 },
                        { 0, 0, 1, 1 }
                    };

                    // Call our API to display interactive island diagram
                    Display.Islands(grid, title: "Archipelago Island Traversal");
                    """)
                .AddVariant("rust", "Rust", """
                    // Rust: 2D Grid & Island Diagram
                    use fry::*;

                    fn main() {
                        // 1. Define 2D matrix map
                        let grid = vec![
                            vec![1, 1, 0, 0],
                            vec![1, 1, 0, 1],
                            vec![0, 0, 1, 1],
                        ];

                        // 2. Call our API to display interactive island diagram
                        islands(&grid).title("Archipelago Island Traversal").show();
                    }
                    """)
                .AddVariant("go", "Go", """
                    // Go: 2D Grid & Island Diagram
                    package main

                    import "fry"

                    func main() {
                        // 1. Define 2D matrix map
                        grid := [][]int{
                            {1, 1, 0, 0},
                            {1, 1, 0, 1},
                            {0, 0, 1, 1},
                        }

                        // 2. Call our API to display interactive island diagram
                        fry.Islands(grid, fry.Title("Archipelago Island Traversal"))
                    }
                    """)
                .AddVariant("javascript", "JavaScript", """
                    // JavaScript: 2D Grid & Island Diagram
                    const { Display } = require('fry');

                    // 1. Define 2D matrix map
                    const grid = [
                        [1, 1, 0, 0],
                        [1, 1, 0, 1],
                        [0, 0, 1, 1]
                    ];

                    // 2. Call our API to display interactive island diagram
                    Display.islands(grid, "Archipelago Island Traversal");
                    """)
                .AddVariant("fsharp", "F#", """
                    // F#: 2D Grid & Island Diagram
                    open Fry

                    // 1. Define 2D matrix map
                    let grid = [
                        [ 1; 1; 0; 0 ]
                        [ 1; 1; 0; 1 ]
                        [ 0; 0; 1; 1 ]
                    ]

                    // 2. Call our API to display interactive island diagram
                    Display.Islands(grid, title = "Archipelago Island Traversal") |> ignore
                    """)
            }
        };
    }

    private DocArticle CreateDiagrams3DMatrixArticle()
    {
        return new DocArticle
        {
            Id = "diagrams_3d_matrices",
            Title = "3D Matrix & Voxel Grid Diagrams",
            Subtitle = "Render 3D matrices, voxel elevation columns, and 3D spatial coordinate grids.",
            ReadingTime = "5 min read",
            Summary = "Visualize 3D matrices and voxel grids using our API. Supports voxel columns (height matrices), 3D spatial points (x, y, z) for 3D pathfinding/BFS, and multi-dimensional tensor inspection across C++, Java, Python, and C#.",
            Keywords = new List<string> { "3d matrix", "matrix3d", "voxel", "voxelbar", "tensor", "grid3d", "spatial" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Voxel Bar Columns (3D Height Matrix)",
                    Content = "Pass a 2D matrix of values or heights into our 3D VoxelBar API. Each cell rises as an extruded 3D voxel bar with automated Viridis or Plasma colormap shading and interactive turntable orbiting."
                },
                new()
                {
                    Heading = "3D Spatial Grid & BFS Pathfinding",
                    Content = "For true 3D spatial algorithms (3D mazes, 3D Game of Life, or octrees), pass a collection of (x, y, z) coordinates to light up active voxel cells in 3D coordinate space.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "Hold Shift while dragging to pan the 3D camera, or use the mouse wheel to zoom in on individual voxel coordinates."
                }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new DocCodeSnippet
                {
                    Id = "snip_diagram_3d_matrix",
                    Title = "Codes [C++ | Java | Python3 | C#] : 3D Voxel Matrix Topography",
                    Description = "Render a 3D matrix as an interactive voxel bar grid with turntable rotation.",
                    TargetKind = WorkspaceItemKind.Script
                }
                .AddVariant("cpp", "C++", """
                    // C++: 3D Voxel Matrix Diagram
                    #include <fry_display.hpp>
                    #include <vector>

                    int main() {
                        // 1. Define 3D height matrix values
                        std::vector<std::vector<int>> matrix3d = {
                            {10, 20, 15, 5},
                            {25, 40, 30, 12},
                            {18, 35, 50, 22},
                            {8,  14, 28, 45}
                        };

                        // 2. Call our API to render an interactive 3D voxel bar matrix
                        fry::Display::voxel_bars(matrix3d, "3D Voxel Matrix Topography");
                        return 0;
                    }
                    """)
                .AddVariant("java", "Java", """
                    // Java: 3D Voxel Matrix Diagram
                    import com.frypdf.display.Display;

                    public class Solution {
                        public static void main(String[] args) {
                            // 1. Define 3D height matrix values
                            int[][] matrix3d = {
                                {10, 20, 15, 5},
                                {25, 40, 30, 12},
                                {18, 35, 50, 22},
                                {8,  14, 28, 45}
                            };

                            // 2. Call our API to render an interactive 3D voxel bar matrix
                            Display.voxelBars(matrix3d, "3D Voxel Matrix Topography");
                        }
                    }
                    """)
                .AddVariant("python", "Python3", """
                    # Python3: 3D Voxel Matrix Diagram
                    from fry_display import Display

                    # 1. Define 3D height matrix values (or numpy array)
                    matrix3d = [
                        [10, 20, 15, 5],
                        [25, 40, 30, 12],
                        [18, 35, 50, 22],
                        [8,  14, 28, 45]
                    ]

                    # 2. Call our API to render an interactive 3D voxel bar matrix
                    Display.plot3d(matrix3d, plot_type="voxelBar", title="3D Voxel Matrix Topography")
                    """)
                .AddVariant("csharp", "C#", """
                    // C#: 3D Voxel Matrix Diagram
                    int[,] matrix3d = {
                        { 10, 20, 15, 5 },
                        { 25, 40, 30, 12 },
                        { 18, 35, 50, 22 },
                        { 8,  14, 28, 45 }
                    };

                    // Call our API to render an interactive 3D voxel bar matrix
                    Display.VoxelBar3D(matrix3d, title: "3D Voxel Matrix Topography", autoRotate: true);
                    """)
                .AddVariant("rust", "Rust", """
                    // Rust: 3D Voxel Matrix Diagram
                    use fry::*;

                    fn main() {
                        // 1. Define 3D height matrix values
                        let matrix3d = vec![
                            vec![10, 20, 15, 5],
                            vec![25, 40, 30, 12],
                            vec![18, 35, 50, 22],
                            vec![8,  14, 28, 45],
                        ];

                        // 2. Call our API to render an interactive 3D voxel bar matrix
                        voxel_bars(&matrix3d).title("3D Voxel Matrix Topography").show();
                    }
                    """)
                .AddVariant("go", "Go", """
                    // Go: 3D Voxel Matrix Diagram
                    package main

                    import "fry"

                    func main() {
                        // 1. Define 3D height matrix values
                        matrix3d := [][]int{
                            {10, 20, 15, 5},
                            {25, 40, 30, 12},
                            {18, 35, 50, 22},
                            {8,  14, 28, 45},
                        }

                        // 2. Call our API to render an interactive 3D voxel bar matrix
                        fry.VoxelBars(matrix3d, fry.Title("3D Voxel Matrix Topography"))
                    }
                    """)
                .AddVariant("javascript", "JavaScript", """
                    // JavaScript: 3D Voxel Matrix Diagram
                    const { Display } = require('fry');

                    // 1. Define 3D height matrix values
                    const matrix3d = [
                        [10, 20, 15, 5],
                        [25, 40, 30, 12],
                        [18, 35, 50, 22],
                        [8,  14, 28, 45]
                    ];

                    // 2. Call our API to render an interactive 3D voxel bar matrix
                    Display.voxelBar3d(matrix3d, "3D Voxel Matrix Topography");
                    """)
                .AddVariant("fsharp", "F#", """
                    // F#: 3D Voxel Matrix Diagram
                    open Fry

                    // 1. Define 3D height matrix values
                    let matrix3d = [
                        [ 10; 20; 15; 5 ]
                        [ 25; 40; 30; 12 ]
                        [ 18; 35; 50; 22 ]
                        [ 8;  14; 28; 45 ]
                    ]

                    // 2. Call our API to render an interactive 3D voxel bar matrix
                    Display.VoxelBars(matrix3d, title = "3D Voxel Matrix Topography") |> ignore
                    """),
                new DocCodeSnippet
                {
                    Id = "snip_diagram_3d_spatial_grid",
                    Title = "Codes [C++ | Java | Python3 | C#] : 3D Spatial Grid & BFS Trajectory",
                    Description = "Render active 3D coordinates (x, y, z) for 3D maze pathfinding, octrees, and voxel algorithms.",
                    TargetKind = WorkspaceItemKind.Notebook
                }
                .AddVariant("cpp", "C++", """
                    // C++: 3D Spatial Coordinate Points
                    #include <fry_display.hpp>
                    #include <vector>

                    int main() {
                        std::vector<fry::Point3D> points = {
                            {0, 0, 0, "Start"}, {1, 0, 0}, {1, 1, 0},
                            {1, 1, 1}, {2, 1, 1}, {2, 2, 2, "Goal"}
                        };
                        fry::Display::scatter3d(points, "3D BFS Shortest Path Trajectory");
                        return 0;
                    }
                    """)
                .AddVariant("java", "Java", """
                    // Java: 3D Spatial Coordinate Points
                    import com.frypdf.display.Display;
                    import java.util.List;

                    public class Solution {
                        public static void main(String[] args) {
                            var points = List.of(
                                new double[]{0, 0, 0}, new double[]{1, 0, 0}, new double[]{1, 1, 0},
                                new double[]{1, 1, 1}, new double[]{2, 1, 1}, new double[]{2, 2, 2}
                            );
                            Display.scatter3d(points, "3D BFS Shortest Path Trajectory");
                        }
                    }
                    """)
                .AddVariant("python", "Python3", """
                    # Python3: 3D Spatial Coordinate Points
                    from fry_display import Display

                    points = [
                        (0, 0, 0), (1, 0, 0), (1, 1, 0),
                        (1, 1, 1), (2, 1, 1), (2, 2, 2)
                    ]
                    Display.scatter3d(points, title="3D BFS Shortest Path Trajectory")
                    """)
                .AddVariant("csharp", "C#", """
                    // C#: 3D Spatial Coordinate Points
                    var pathPoints = new[]
                    {
                        (0, 0, 0), (1, 0, 0), (1, 1, 0),
                        (1, 1, 1), (2, 1, 1), (2, 2, 2)
                    };
                    Display.Scatter3D(pathPoints, title: "3D BFS Shortest Path Trajectory");
                    """)
                .AddVariant("rust", "Rust", """
                    // Rust: 3D Spatial Coordinate Points
                    use fry::scatter3d;

                    fn main() {
                        let points = vec![
                            (0.0, 0.0, 0.0), (1.0, 0.0, 0.0), (1.0, 1.0, 0.0),
                            (1.0, 1.0, 1.0), (2.0, 1.0, 1.0), (2.0, 2.0, 2.0),
                        ];
                        scatter3d(&points).title("3D BFS Shortest Path Trajectory").show();
                    }
                    """)
                .AddVariant("go", "Go", """
                    // Go: 3D Spatial Coordinate Points
                    package main

                    import "fry"

                    func main() {
                        points := [][]float64{
                            {0, 0, 0}, {1, 0, 0}, {1, 1, 0},
                            {1, 1, 1}, {2, 1, 1}, {2, 2, 2},
                        }
                        fry.Scatter3D(points, fry.Title("3D BFS Shortest Path Trajectory"))
                    }
                    """)
                .AddVariant("javascript", "JavaScript", """
                    // JavaScript: 3D Spatial Coordinate Points
                    const { Display } = require('fry');

                    const points = [
                        [0, 0, 0], [1, 0, 0], [1, 1, 0],
                        [1, 1, 1], [2, 1, 1], [2, 2, 2]
                    ];
                    Display.scatter3d(points, "3D BFS Shortest Path Trajectory");
                    """)
                .AddVariant("fsharp", "F#", """
                    // F#: 3D Spatial Coordinate Points
                    open Fry

                    let points = [
                        (0.0, 0.0, 0.0); (1.0, 0.0, 0.0); (1.0, 1.0, 0.0)
                        (1.0, 1.0, 1.0); (2.0, 1.0, 1.0); (2.0, 2.0, 2.0)
                    ]
                    Display.Scatter3D(points, title = "3D BFS Shortest Path Trajectory") |> ignore
                    """)
            }
        };
    }

    private DocArticle CreateDiagramsVectorCanvasArticle()
    {
        return new DocArticle
        {
            Id = "diagrams_vector_canvas",
            Title = "Custom 2D Vector Canvas & Shape Diagrams",
            Subtitle = "Build custom architecture diagrams, stacks, queues, and geometric scenes.",
            ReadingTime = "4 min read",
            Summary = "When data structures don't fit standard trees or grids, our Vector Canvas API lets you draw arbitrary rectangles, circles, connecting arrows, and labels.",
            Keywords = new List<string> { "canvas", "vector", "shapes", "diagram", "flowchart", "stack", "rectangles" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Freeform Vector Scenes",
                    Content = "Build flowcharts, memory layouts, and data pipeline diagrams. Vector shapes retain crisp rendering at any zoom level, and can be exported directly to PNG snapshots."
                },
                new()
                {
                    Heading = "Primitive Shapes",
                    Content = "Our Canvas API provides easy helpers for:\n• AddRect(x, y, w, h, label, fill, stroke)\n• AddCircle(cx, cy, r, label, fill, stroke)\n• AddArrow(x1, y1, x2, y2, label, color)\n• AddText(x, y, text, fontSize, color)"
                }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new DocCodeSnippet
                {
                    Id = "snip_diagram_canvas",
                    Title = "Codes [C++ | Java | Python3 | C#] : Custom Canvas Diagram",
                    Description = "Draw rectangles, connection arrows, and text labels to build custom diagrams.",
                    TargetKind = WorkspaceItemKind.Script
                }
                .AddVariant("cpp", "C++", """
                    // C++: Freeform Vector Canvas Diagram
                    #include <fry_display.hpp>

                    int main() {
                        // 1. Initialize vector canvas diagram
                        fry::CanvasVisualizer canvas("Pipeline Architecture", 420, 240);

                        // 2. Draw pipeline stages and connecting arrows
                        canvas.add_rect(30, 80, 100, 45, "Ingestion", "#0284c7", "#38bdf8");
                        canvas.add_arrow(135, 102, 175, 102, "JSON", "#94a3b8");
                        canvas.add_rect(180, 80, 100, 45, "Transform", "#7c3aed", "#a78bfa");
                        canvas.add_arrow(285, 102, 325, 102, "Parquet", "#94a3b8");
                        canvas.add_rect(330, 80, 100, 45, "Storage", "#10b981", "#34d399");

                        // 3. Call our API to display the diagram
                        canvas.show();
                        return 0;
                    }
                    """)
                .AddVariant("java", "Java", """
                    // Java: Freeform Vector Canvas Diagram
                    import com.frypdf.display.Visualizer;

                    public class Solution {
                        public static void main(String[] args) {
                            // 1. Initialize vector canvas diagram
                            var canvas = Visualizer.canvas("Pipeline Architecture", 420, 240);

                            // 2. Draw pipeline stages and connecting arrows
                            canvas.addRect(30, 80, 100, 45, "Ingestion", "#0284c7", "#38bdf8");
                            canvas.addArrow(135, 102, 175, 102, "JSON", "#94a3b8");
                            canvas.addRect(180, 80, 100, 45, "Transform", "#7c3aed", "#a78bfa");
                            canvas.addArrow(285, 102, 325, 102, "Parquet", "#94a3b8");
                            canvas.addRect(330, 80, 100, 45, "Storage", "#10b981", "#34d399");

                            // 3. Call our API to display the diagram
                            canvas.show();
                        }
                    }
                    """)
                .AddVariant("python", "Python3", """
                    # Python3: Freeform Vector Canvas Diagram
                    from fry_display import Display

                    # 1. Initialize vector canvas diagram
                    canvas = Display.canvas("Pipeline Architecture", width=420, height=240)

                    # 2. Draw pipeline stages and connecting arrows
                    canvas.add_rect(30, 80, 100, 45, label="Ingestion", fill="#0284c7", stroke="#38bdf8")
                    canvas.add_arrow(135, 102, 175, 102, label="JSON", color="#94a3b8")
                    canvas.add_rect(180, 80, 100, 45, label="Transform", fill="#7c3aed", stroke="#a78bfa")
                    canvas.add_arrow(285, 102, 325, 102, label="Parquet", color="#94a3b8")
                    canvas.add_rect(330, 80, 100, 45, label="Storage", fill="#10b981", stroke="#34d399")

                    # 3. Call our API to display the diagram
                    canvas.show()
                    """)
                .AddVariant("csharp", "C#", """
                    // C#: Freeform Vector Canvas Diagram
                    var recorder = VisualizerRecorder.CreateCanvas("Pipeline Architecture", width: 420, height: 240);

                    recorder.Step("Architecture Overview", scene =>
                    {
                        scene.AddRect(30, 80, 100, 45, label: "Ingestion", fill: "#0284c7", stroke: "#38bdf8");
                        scene.AddArrow(135, 102, 175, 102, label: "JSON", stroke: "#94a3b8");
                        scene.AddRect(180, 80, 100, 45, label: "Transform", fill: "#7c3aed", stroke: "#a78bfa");
                        scene.AddArrow(285, 102, 325, 102, label: "Parquet", stroke: "#94a3b8");
                        scene.AddRect(330, 80, 100, 45, label: "Storage", fill: "#10b981", stroke: "#34d399");
                    });

                    // Call our API to display the diagram
                    Display.Visualizer(recorder);
                    """)
                .AddVariant("rust", "Rust", """
                    // Rust: Freeform Vector Canvas Diagram
                    use fry::canvas;

                    fn main() {
                        // 1. Initialize vector canvas diagram
                        let mut c = canvas("Pipeline Architecture", 420, 240);

                        // 2. Draw pipeline stages and connecting arrows
                        c.add_rect(30.0, 80.0, 100.0, 45.0, "Ingestion", "#0284c7", "#38bdf8");
                        c.add_arrow(135.0, 102.0, 175.0, 102.0, "JSON", "#94a3b8");
                        c.add_rect(180.0, 80.0, 100.0, 45.0, "Transform", "#7c3aed", "#a78bfa");
                        c.add_arrow(285.0, 102.0, 325.0, 102.0, "Parquet", "#94a3b8");
                        c.add_rect(330.0, 80.0, 100.0, 45.0, "Storage", "#10b981", "#34d399");

                        // 3. Call our API to display the diagram
                        c.show();
                    }
                    """)
                .AddVariant("go", "Go", """
                    // Go: Freeform Vector Canvas Diagram
                    package main

                    import "fry"

                    func main() {
                        // 1. Initialize vector canvas diagram
                        c := fry.Canvas("Pipeline Architecture", 420, 240)

                        // 2. Draw pipeline stages and connecting arrows
                        c.AddRect(30, 80, 100, 45, "Ingestion", "#0284c7", "#38bdf8")
                        c.AddArrow(135, 102, 175, 102, "JSON", "#94a3b8")
                        c.AddRect(180, 80, 100, 45, "Transform", "#7c3aed", "#a78bfa")
                        c.AddArrow(285, 102, 325, 102, "Parquet", "#94a3b8")
                        c.AddRect(330, 80, 100, 45, "Storage", "#10b981", "#34d399")

                        // 3. Call our API to display the diagram
                        c.Show()
                    }
                    """)
                .AddVariant("javascript", "JavaScript", """
                    // JavaScript: Freeform Vector Canvas Diagram
                    const { Display } = require('fry');

                    // 1. Initialize vector canvas diagram
                    const c = Display.canvas("Pipeline Architecture", 420, 240);

                    // 2. Draw pipeline stages and connecting arrows
                    c.addRect(30, 80, 100, 45, "Ingestion", "#0284c7", "#38bdf8");
                    c.addArrow(135, 102, 175, 102, "JSON", "#94a3b8");
                    c.addRect(180, 80, 100, 45, "Transform", "#7c3aed", "#a78bfa");
                    c.addArrow(285, 102, 325, 102, "Parquet", "#94a3b8");
                    c.addRect(330, 80, 100, 45, "Storage", "#10b981", "#34d399");

                    // 3. Call our API to display the diagram
                    c.show();
                    """)
                .AddVariant("fsharp", "F#", """
                    // F#: Freeform Vector Canvas Diagram
                    open Fry

                    // 1. Initialize vector canvas diagram
                    let canvas = Display.Canvas("Pipeline Architecture", width = 420, height = 240)

                    // 2. Draw pipeline stages and connecting arrows
                    canvas.AddRect(30.0, 80.0, 100.0, 45.0, label = "Ingestion", fill = "#0284c7", stroke = "#38bdf8") |> ignore
                    canvas.AddArrow(135.0, 102.0, 175.0, 102.0, label = "JSON", stroke = "#94a3b8") |> ignore
                    canvas.AddRect(180.0, 80.0, 100.0, 45.0, label = "Transform", fill = "#7c3aed", stroke = "#a78bfa") |> ignore
                    canvas.AddArrow(285.0, 102.0, 325.0, 102.0, label = "Parquet", stroke = "#94a3b8") |> ignore
                    canvas.AddRect(330.0, 80.0, 100.0, 45.0, label = "Storage", fill = "#10b981", stroke = "#34d399") |> ignore

                    // 3. Call our API to display the diagram
                    canvas.Show() |> ignore
                    """)
            }
        };
    }

    private DocArticle CreateDiagrams3DSurfacesArticle()
    {
        return new DocArticle
        {
            Id = "diagrams_3d_surfaces",
            Title = "3D Interactive Surface Diagrams",
            Subtitle = "Render 3D mathematical surfaces, trajectories, wireframes, and vector fields.",
            ReadingTime = "5 min read",
            Summary = "Plot mathematical functions z = f(x, y) with 3D turntable orbiting, camera view presets, and colormaps (Viridis, Plasma, Magma, CoolWarm).",
            Keywords = new List<string> { "3d", "surface", "plot3d", "diagram", "math", "turntable", "orbit" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Hardware-Accelerated 3D Plots",
                    Content = "Our 3D plotting API provides smooth interactive turntable rotation, camera presets (ISO, TOP, FRONT, SIDE), perspective vs orthographic projections, and floor grid shadows."
                },
                new()
                {
                    Heading = "Colormaps & Shading",
                    Content = "Surfaces support real-time colormap cycling (Viridis, Plasma, Magma, CoolWarm, Turbo, Rainbow), wireframe overlays, and Three.js HTML export.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "Hold Shift while dragging to pan the 3D camera across the canvas."
                }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new DocCodeSnippet
                {
                    Id = "snip_diagram_3d",
                    Title = "Codes [C++ | Java | Python3 | C#] : 3D Surface & Function Diagram",
                    Description = "Plot a 3D mathematical surface function with interactive camera controls.",
                    TargetKind = WorkspaceItemKind.Script
                }
                .AddVariant("cpp", "C++", """
                    // C++: 3D Surface Function Diagram
                    #include <fry_display.hpp>
                    #include <cmath>

                    int main() {
                        // 1. Define mathematical surface function z = f(x, y)
                        fry::Surface3D surface("Hyperbolic Paraboloid",
                            [](double x, double y) { return x * x - y * y; },
                            -3.0, 3.0, -3.0, 3.0, /*resolution=*/30);

                        // 2. Call our API to display the interactive 3D diagram
                        surface.show();
                        return 0;
                    }
                    """)
                .AddVariant("java", "Java", """
                    // Java: 3D Surface Function Diagram
                    import com.frypdf.display.Display;

                    public class Solution {
                        public static void main(String[] args) {
                            // 1. Call our API to display a 3D mathematical surface
                            Display.plot3d("Hyperbolic Paraboloid",
                                (x, y) -> x * x - y * y,
                                -3.0, 3.0, -3.0, 3.0, 30);
                        }
                    }
                    """)
                .AddVariant("python", "Python3", """
                    # Python3: 3D Surface Function Diagram
                    from fry_display import Display

                    # 1. Define mathematical surface function z = f(x, y)
                    def saddle(x, y):
                        return x**2 - y**2

                    # 2. Call our API to display the interactive 3D diagram
                    Display.plot3d_surface(saddle, x_range=(-3, 3), y_range=(-3, 3), res=30, colormap="viridis")
                    """)
                .AddVariant("csharp", "C#", """
                    // C#: 3D Surface Function Diagram
                    Display.Plot3D(
                        (x, y) => x * x - y * y,
                        xRange: (-3, 3),
                        yRange: (-3, 3),
                        resolution: 30,
                        title: "Hyperbolic Paraboloid Saddle");
                    """)
                .AddVariant("rust", "Rust", """
                    // Rust: 3D Surface Function Diagram
                    use fry::Surface3D;

                    fn main() {
                        // 1. Define mathematical surface function z = f(x, y)
                        let surface = Surface3D::new("Hyperbolic Paraboloid",
                            |x, y| x * x - y * y,
                            -3.0, 3.0, -3.0, 3.0, 30);

                        // 2. Call our API to display the interactive 3D diagram
                        surface.show();
                    }
                    """)
                .AddVariant("go", "Go", """
                    // Go: 3D Surface Function Diagram
                    package main

                    import "fry"

                    func main() {
                        // 1. Call our API to display a 3D mathematical surface
                        fry.Surface3DFunc("Hyperbolic Paraboloid",
                            func(x, y float64) float64 { return x*x - y*y },
                            -3.0, 3.0, -3.0, 3.0, 30)
                    }
                    """)
                .AddVariant("javascript", "JavaScript", """
                    // JavaScript: 3D Surface Function Diagram
                    const { Display } = require('fry');

                    // 1. Call our API to display a 3D mathematical surface
                    Display.surface3dFunc("Hyperbolic Paraboloid",
                        (x, y) => x * x - y * y,
                        -3.0, 3.0, -3.0, 3.0, 30);
                    """)
                .AddVariant("fsharp", "F#", """
                    // F#: 3D Surface Function Diagram
                    open Fry

                    // 1. Call our API to display a 3D mathematical surface
                    Display.Surface3D("Hyperbolic Paraboloid",
                        (fun x y -> x * x - y * y),
                        -3.0, 3.0, -3.0, 3.0, resolution = 30) |> ignore
                    """)
            }
        };
    }
}
