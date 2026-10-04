# Diagrams & Visualizations API: Polyglot Guide

FrySharp provides a unified, polyglot visual runtime for interactive diagrams, data structure visualizers, 2D/3D plots, and custom vector scenes. Regardless of your programming language (**C++**, **Java**, **Python**, or **C#**), programs generate rich interactive output through canonical APIs, standardized MIME bundles (`application/vnd.fry.*.v1+json`), and universal interactive visual controls.

---

## 1. Core Paradigm: Multi-Language Grouped Code Architecture

Every diagram type exposes a canonical API across all supported toolchains. In our documentation and UI, examples are grouped with multi-language tabs so you can instantly switch between languages to see how to call our API:

```
┌────────────────────────────────────────────────────────────────────────┐
│ Codes [C++ | Java | Python3 | C#] : Interactive Diagram with Comments  │
├────────────────────────────────────────────────────────────────────────┤
│  C++  |  Java  |  Python3  |  C#                                       │
├────────────────────────────────────────────────────────────────────────┤
│  // Code for the selected language renders here                        │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Quickstart Diagram API

### Codes [C++ | Java | Python3 | C#] : Quickstart Diagram

#### C++
```cpp
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
```

#### Java
```java
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
```

#### Python3
```python
# Python3: Quickstart Visual Diagram with our API
from fry_display import Display

# 1. Prepare data points for the diagram
numbers = [10, 25, 45, 30, 60, 85]

# 2. Call our API to emit an interactive bar chart diagram
Display.chart(numbers, title="Quarterly Trend Analysis", chart_type="bar")
```

#### C#
```csharp
// C#: Quickstart Visual Diagram with our API
var numbers = new[] { 10, 25, 45, 30, 60, 85 };

// Call our API to emit an interactive bar chart diagram
Display.Chart(numbers, title: "Quarterly Trend Analysis", type: ChartType.Bar);
```

---

## 3. Hierarchical Binary Trees & Prefix Tree (Trie) Diagrams

Our Tree Visualizer uses the **Buchheim layout algorithm** to position nodes automatically without line collisions or overlapping branches.

### Codes [C++ | Java | Python3 | C#] : Binary Search Tree Diagram

#### C++
```cpp
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
```

#### Java
```java
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
```

#### Python3
```python
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
```

#### C#
```csharp
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
```

---

## 4. Graph Networks & Architecture Flow Diagrams

Model microservices, network flow, state machines, and dependency graphs with circular auto-layout and directional arrows.

### Codes [C++ | Java | Python3 | C#] : Directed Network Flow Diagram

#### C++
```cpp
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
```

#### Java
```java
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
```

#### Python3
```python
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
```

#### C#
```csharp
// C#: Directed Network Graph Diagram
var network = new Dictionary<string, List<string>>
{
    ["API Gateway"] = new() { "Auth Service", "Billing Service" },
    ["Auth Service"] = new() { "Redis Cache" },
    ["Billing Service"] = new() { "Postgres DB" }
};

// Call our API to display the interactive graph diagram
Display.Graph(network, directed: true, title: "Microservice Architecture Flow");
```

---

## 5. 2D Grids, Matrices & Island Traversal Diagrams

Render 2D arrays, dynamic programming memoization matrices, and automated flood-fill island detection.

### Codes [C++ | Java | Python3 | C#] : Grid & Island Traversal Diagram

#### C++
```cpp
// C++: 2D Grid & Island Diagram
#include <fry_display.hpp>
#include <vector>

int main() {
    std::vector<std::vector<int>> grid = {
        {1, 1, 0, 0},
        {1, 1, 0, 1},
        {0, 0, 1, 1}
    };

    fry::Display::islands(grid, "Archipelago Island Traversal");
    return 0;
}
```

#### Java
```java
// Java: 2D Grid & Island Diagram
import com.frypdf.display.Visualizer;

public class Solution {
    public static void main(String[] args) {
        int[][] grid = {
            {1, 1, 0, 0},
            {1, 1, 0, 1},
            {0, 0, 1, 1}
        };

        Visualizer.grid(grid).title("Archipelago Island Traversal").show();
    }
}
```

#### Python3
```python
# Python3: 2D Grid & Island Diagram
from fry_display import Display

grid = [
    [1, 1, 0, 0],
    [1, 1, 0, 1],
    [0, 0, 1, 1]
]

Display.islands(grid, title="Archipelago Island Traversal")
```

#### C#
```csharp
// C#: 2D Grid & Island Diagram
int[,] grid = {
    { 1, 1, 0, 0 },
    { 1, 1, 0, 1 },
    { 0, 0, 1, 1 }
};

Display.Islands(grid, title: "Archipelago Island Traversal");
```

---

## 6. 3D Matrix & Voxel Grid Diagrams

Visualize 3D matrices and voxel grids directly from code. Our 3D visual runtime supports:
- **Voxel Bar Columns (3D Height Matrix)**: Pass a 2D matrix of values or heights into our 3D VoxelBar API. Each cell rises as an extruded 3D voxel bar with automated Viridis or Plasma colormap shading and interactive turntable orbiting.
- **3D Spatial Coordinate Grids `(x, y, z)`**: For true 3D spatial algorithms (3D mazes, 3D Game of Life, voxel worlds, or octrees), pass a collection of `(x, y, z)` coordinates to light up active voxel cells in 3D coordinate space.
- **Multi-Dimensional Tensor Inspection**: Inspect higher-dimensional tensors `(C, H, W)` or `int[,,]` slice-by-slice with interactive table inspection.

### Codes [C++ | Java | Python3 | C#] : 3D Voxel Matrix Topography

#### C++
```cpp
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
```

#### Java
```java
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
```

#### Python3
```python
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
```

#### C#
```csharp
// C#: 3D Voxel Matrix Diagram
int[,] matrix3d = {
    { 10, 20, 15, 5 },
    { 25, 40, 30, 12 },
    { 18, 35, 50, 22 },
    { 8,  14, 28, 45 }
};

// Call our API to render an interactive 3D voxel bar matrix
Display.VoxelBar3D(matrix3d, title: "3D Voxel Matrix Topography", autoRotate: true);
```

---

### Codes [C++ | Java | Python3 | C#] : 3D Spatial Grid & Coordinate Points `(x, y, z)`

For 3D pathfinding (e.g. 3D maze BFS/A*, 3D Game of Life, or octree spatial partitioning), pass explicit `(x, y, z)` coordinates:

#### C++
```cpp
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
```

#### Java
```java
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
```

#### Python3
```python
# Python3: 3D Spatial Coordinate Points
from fry_display import Display

points = [
    (0, 0, 0), (1, 0, 0), (1, 1, 0),
    (1, 1, 1), (2, 1, 1), (2, 2, 2)
]
Display.scatter3d(points, title="3D BFS Shortest Path Trajectory")
```

#### C#
```csharp
// C#: 3D Spatial Coordinate Points
var pathPoints = new[]
{
    (0, 0, 0), (1, 0, 0), (1, 1, 0),
    (1, 1, 1), (2, 1, 1), (2, 2, 2)
};
Display.Scatter3D(pathPoints, title: "3D BFS Shortest Path Trajectory");
```

---

## 7. Custom 2D Vector Canvas & Shape Diagrams

Draw custom geometry, flowcharts, queues, and memory layouts with primitives (`AddRect`, `AddCircle`, `AddArrow`, `AddText`).

### Codes [C++ | Java | Python3 | C#] : Custom Canvas Diagram

#### C++
```cpp
// C++: Freeform Vector Canvas Diagram
#include <fry_display.hpp>

int main() {
    fry::CanvasVisualizer canvas("Pipeline Architecture", 420, 240);

    canvas.add_rect(30, 80, 100, 45, "Ingestion", "#0284c7", "#38bdf8");
    canvas.add_arrow(135, 102, 175, 102, "JSON", "#94a3b8");
    canvas.add_rect(180, 80, 100, 45, "Transform", "#7c3aed", "#a78bfa");
    canvas.add_arrow(285, 102, 325, 102, "Parquet", "#94a3b8");
    canvas.add_rect(330, 80, 100, 45, "Storage", "#10b981", "#34d399");

    canvas.show();
    return 0;
}
```

#### Java
```java
// Java: Freeform Vector Canvas Diagram
import com.frypdf.display.Visualizer;

public class Solution {
    public static void main(String[] args) {
        var canvas = Visualizer.canvas("Pipeline Architecture", 420, 240);

        canvas.addRect(30, 80, 100, 45, "Ingestion", "#0284c7", "#38bdf8");
        canvas.addArrow(135, 102, 175, 102, "JSON", "#94a3b8");
        canvas.addRect(180, 80, 100, 45, "Transform", "#7c3aed", "#a78bfa");
        canvas.addArrow(285, 102, 325, 102, "Parquet", "#94a3b8");
        canvas.addRect(330, 80, 100, 45, "Storage", "#10b981", "#34d399");

        canvas.show();
    }
}
```

#### Python3
```python
# Python3: Freeform Vector Canvas Diagram
from fry_display import Display

canvas = Display.canvas("Pipeline Architecture", width=420, height=240)

canvas.add_rect(30, 80, 100, 45, label="Ingestion", fill="#0284c7", stroke="#38bdf8")
canvas.add_arrow(135, 102, 175, 102, label="JSON", color="#94a3b8")
canvas.add_rect(180, 80, 100, 45, label="Transform", fill="#7c3aed", stroke="#a78bfa")
canvas.add_arrow(285, 102, 325, 102, label="Parquet", color="#94a3b8")
canvas.add_rect(330, 80, 100, 45, label="Storage", fill="#10b981", stroke="#34d399")

canvas.show()
```

#### C#
```csharp
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

Display.Visualizer(recorder);
```

---

## 8. Interactive 3D Surfaces & Mathematical Function Plots

Plot mathematical functions `z = f(x, y)` with real-time 3D camera controls, colormaps, and turntable orbiting.

### Codes [C++ | Java | Python3 | C#] : 3D Surface & Function Diagram

#### C++
```cpp
// C++: 3D Surface Function Diagram
#include <fry_display.hpp>
#include <cmath>

int main() {
    fry::Surface3D surface("Hyperbolic Paraboloid",
        [](double x, double y) { return x * x - y * y; },
        -3.0, 3.0, -3.0, 3.0, /*resolution=*/30);

    surface.show();
    return 0;
}
```

#### Java
```java
// Java: 3D Surface Function Diagram
import com.frypdf.display.Display;

public class Solution {
    public static void main(String[] args) {
        Display.plot3d("Hyperbolic Paraboloid",
            (x, y) -> x * x - y * y,
            -3.0, 3.0, -3.0, 3.0, 30);
    }
}
```

#### Python3
```python
# Python3: 3D Surface Function Diagram
from fry_display import Display

def saddle(x, y):
    return x**2 - y**2

Display.plot3d_surface(saddle, x_range=(-3, 3), y_range=(-3, 3), res=30, colormap="viridis")
```

#### C#
```csharp
// C#: 3D Surface Function Diagram
Display.Plot3D(
    (x, y) => x * x - y * y,
    xRange: (-3, 3),
    yRange: (-3, 3),
    resolution: 30,
    title: "Hyperbolic Paraboloid Saddle");
```

---

## 9. Summary of Display & Visualizer Methods

| Visual Kind | Canonical Method | Primary Inputs | Interactive Controls |
|---|---|---|---|
| **Tree Diagram** | `Display.tree(root)` | Node object with left/right or children | Buchheim layout, traversal stepper, zoom, pan |
| **Graph Diagram** | `Display.graph(adjList)` | Adjacency dictionary / edge list | Circular layout, directed arrows, weights, node glow |
| **Grid / Matrix** | `Display.matrix(grid)` | 2D array / matrix | Cell coordinate tiles, values, DP headers, zoom |
| **Island Traversal** | `Display.islands(grid)` | 2D binary / weighted array | Automated BFS/DFS scrubber, archipelago count |
| **3D Voxel Matrix** | `Display.voxel_bars(matrix)` / `Display.VoxelBar3D(matrix)` | 2D height matrix / 3D points | 3D voxel bar columns, Viridis/Plasma shading, turntable rotation |
| **3D Spatial Path** | `Display.scatter3d(points)` / `Display.Scatter3D(points)` | `(x, y, z)` tuples / points | 3D BFS shortest path, octrees, spatial orbit/pan/zoom |
| **Vector Canvas** | `Display.canvas(title, w, h)` | Vector scene lambda | Rectangles, circles, arrows, text, custom layout |
| **2D Series Chart** | `Display.chart(data)` | 1D/2D arrays, dicts, DataFrames | Type cycler (Line/Bar/Area/Scatter/Pie), legend |
| **3D Surface Plot** | `Display.plot3d(func)` | Lambda `(x, y) => z`, ranges | Turntable rotate, camera presets, colormaps |
