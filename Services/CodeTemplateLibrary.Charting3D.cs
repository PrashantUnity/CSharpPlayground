using System.Collections.Generic;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static partial class CodeTemplateLibrary
{
    private static IEnumerable<CodeTemplate> GetCharting3DTemplates() => new List<CodeTemplate>
    {
        new()
        {
            Id = "charting3d_mathematical_surface",
            Title = "3D Mathematical Surface (Ripple Function)",
            Category = "Data & Visuals",
            Kind = WorkspaceItemKind.Notebook,
            Description = "Interactive 3D mathematical surface mesh with spectral Viridis color grading, orbit turntable, and elevation inspection.",
            IconKind = MaterialIconKind.SineWave,
            AccentColor = "#4ec9b0",
            AccentBackground = "#0e2a24",
            AccentBorder = "#1b4d42",
            CategoryBadge = "3D • Surface Plot",
            Tags = new List<string> { "3D", "Surface", "Math", "Viridis" },
            Notes = @"# 3D Surface Visualization
Render mathematical functions $z = f(x, y)$ in interactive 3D space.

### Interaction Controls:
- **Left-Click Drag**: Orbit camera (azimuth & elevation).
- **Right-Click Drag or Shift + Drag**: Pan camera.
- **Mouse Wheel**: Zoom in/out.
- **Hover**: Inspect 3D coordinates $(x, y, z)$.
- **Toolbar Buttons**:
  - `ISO`, `TOP`, `FRT`, `SIDE`: Snap to perspective viewpoints.
  - `Turntable`: Start smooth auto-rotation.
  - `HTML`: Export to standalone Three.js WebGL.",
            InitialCode = @"// 3D Mathematical Surface Plot: Ripple / Wave Function
using System;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;

Display.Surface3D(
    func: (x, y) => {
        double r = Math.Sqrt(x * x + y * y);
        return r == 0 ? 1.0 : Math.Sin(r) / r;
    },
    xRange: (-8.0, 8.0),
    yRange: (-8.0, 8.0),
    resolution: 32,
    title: ""Sombrero / Ripple Surface: z = sin(r)/r"",
    colorMap: ColorMapPreset.Viridis
);

Console.WriteLine(""Drag with left mouse button to rotate in 3D space!"");"
        },
        new()
        {
            Id = "charting3d_lorenz_attractor",
            Title = "3D Lorenz Attractor (Chaos Theory)",
            Category = "Data & Visuals",
            Kind = WorkspaceItemKind.Script,
            Description = "Simulates the classic 3D Lorenz strange attractor system with trajectory velocity gradient and 3D camera orbit.",
            IconKind = MaterialIconKind.AxisArrow,
            AccentColor = "#e06c75",
            AccentBackground = "#2b1417",
            AccentBorder = "#4a2025",
            CategoryBadge = "3D • Chaos Theory",
            Tags = new List<string> { "3D", "Lorenz", "Chaos", "Trajectory" },
            Notes = @"# Lorenz Attractor (3D Trajectory)
The Lorenz attractor is a system of three ordinary differential equations:
$$\frac{dx}{dt} = \sigma (y - x)$$
$$\frac{dy}{dt} = x (\rho - z) - y$$
$$\frac{dz}{dt} = x y - \beta z$$

With standard parameters $\sigma = 10$, $\rho = 28$, $\beta = 8/3$.",
            InitialCode = @"using System;
using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;

// Parameters
double sigma = 10.0;
double rho = 28.0;
double beta = 8.0 / 3.0;
double dt = 0.01;

double x = 0.1, y = 0.0, z = 0.0;
var points = new List<Point3D>();

for (int i = 0; i < 2500; i++)
{
    double dx = sigma * (y - x) * dt;
    double dy = (x * (rho - z) - y) * dt;
    double dz = (x * y - beta * z) * dt;

    x += dx;
    y += dy;
    z += dz;

    points.Add(new Point3D(x, y, z));
}

Display.Trajectory3D(points, title: ""Lorenz Attractor (2,500 Steps)"", colorMap: ColorMapPreset.Plasma);
Console.WriteLine($""Computed {points.Count} 3D trajectory points."");"
        },
        new()
        {
            Id = "charting3d_network_topology",
            Title = "3D Force-Directed Network Graph",
            Category = "Data & Visuals",
            Kind = WorkspaceItemKind.Notebook,
            Description = "Disentangles complex interconnected nodes into 3D space using 3D Coulomb-Hooke spring-electrical physics.",
            IconKind = MaterialIconKind.HubOutline,
            AccentColor = "#61afef",
            AccentBackground = "#102336",
            AccentBorder = "#1c3c5c",
            CategoryBadge = "3D • Network Graph",
            Tags = new List<string> { "3D", "Graph", "Force-Directed", "Topology" },
            Notes = @"# 3D Force-Directed Graph Layout
Places nodes and edges in three-dimensional space using Coulomb-Hooke physical simulation.
This removes edge overlap and resolves 2D network tangles.",
            InitialCode = @"using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;

var graph = new Graph3DData();

// Nodes
var gw = graph.AddNode(""gw"", ""API Gateway"", color: ""#e06c75"", radius: 10);
var auth = graph.AddNode(""auth"", ""Auth Service"", color: ""#61afef"", radius: 8);
var orders = graph.AddNode(""ord"", ""Order Engine"", color: ""#98c379"", radius: 8);
var pay = graph.AddNode(""pay"", ""Payment Gateway"", color: ""#e5c07b"", radius: 8);
var db1 = graph.AddNode(""db1"", ""PostgreSQL DB"", color: ""#c678dd"", radius: 9);
var cache = graph.AddNode(""cache"", ""Redis Cache"", color: ""#d19a66"", radius: 7);

// Edges
graph.AddEdge(""gw"", ""auth"", weight: 10, isDirected: true);
graph.AddEdge(""gw"", ""ord"", weight: 25, isDirected: true);
graph.AddEdge(""ord"", ""pay"", weight: 15, isDirected: true);
graph.AddEdge(""ord"", ""db1"", weight: 30, isDirected: true);
graph.AddEdge(""ord"", ""cache"", weight: 40, isDirected: true);
graph.AddEdge(""auth"", ""cache"", weight: 20, isDirected: true);

Display.Graph3D(graph, title: ""Microservice Architecture Topology (3D)"");"
        }
    };
}
