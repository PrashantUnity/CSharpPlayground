using System.Collections.Generic;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public partial class DocumentationService
{
    private DocArticle CreateInteractive3DVisualizationArticle()
    {
        return new DocArticle
        {
            Id = "interactive_3d_visualization",
            Title = "Interactive 3D Visualizations & Charts",
            Subtitle = "Render 3D surfaces, scatter point clouds, force-directed graphs, and chaotic trajectories with orbit camera controls.",
            ReadingTime = "4 min read",
            Summary = "C# Code Studio features a pure managed, zero-dependency 3D vector graphics engine for exploring high-dimensional data, mathematical surfaces, and graph topologies.",
            Keywords = new List<string> { "3d", "surface", "scatter", "graph3d", "lorenz", "orbit", "camera", "three.js", "display.plot3d" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Native 3D Vector Engine",
                    Content = "3D visualizations are rendered via a pure managed C# 3D pipeline on Avalonia's drawing canvas. It runs identically on macOS (Metal), Windows, and Linux without external GPU drivers, native crashes, or airspace issues.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "You can drag with the left mouse button to rotate in 3D space, Shift+drag or right-drag to pan, and scroll to zoom."
                },
                new()
                {
                    Heading = "Mathematical Surfaces: Display.Surface3D()",
                    Content = "Visualize mathematical functions z = f(x, y) with spectral color gradients (Viridis, Plasma, CoolWarm, Turbo) and optional wireframe."
                },
                new()
                {
                    Heading = "3D Force-Directed Network Graphs: Display.Graph3D()",
                    Content = "Disentangle dense 2D network tangles into 3D space using 3D Coulomb-Hooke physical simulation."
                },
                new()
                {
                    Heading = "3D Scatter & Point Clouds: Display.Scatter3D()",
                    Content = "Plot 3D coordinates, cluster embeddings, and PCA projections with depth-scaled billboard spheres and hover coordinate inspection."
                },
                new()
                {
                    Heading = "Exporting to Standalone Three.js WebGL",
                    Content = "Click the HTML button in the 3D toolbar to copy a standalone, self-contained Three.js HTML document that can be viewed in any web browser or shared with others."
                }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "doc_snippet_surface3d",
                    Title = "3D Ripple Surface",
                    Description = "Mathematical wave surface function with Viridis color map.",
                    Code = @"Display.Surface3D(
    func: (x, y) => Math.Sin(Math.Sqrt(x * x + y * y)),
    xRange: (-6.0, 6.0),
    yRange: (-6.0, 6.0),
    resolution: 32,
    title: ""Wave Ripple Surface"",
    colorMap: ColorMapPreset.Viridis
);"
                },
                new()
                {
                    Id = "doc_snippet_graph3d",
                    Title = "3D Force-Directed Network",
                    Description = "Microservice cluster layout in 3D.",
                    Code = @"var graph = new Graph3DData();
graph.AddNode(""gw"", ""API Gateway"", color: ""#e06c75"", radius: 10);
graph.AddNode(""auth"", ""Auth Service"", color: ""#61afef"", radius: 8);
graph.AddNode(""db"", ""PostgreSQL"", color: ""#98c379"", radius: 9);

graph.AddEdge(""gw"", ""auth"", isDirected: true);
graph.AddEdge(""auth"", ""db"", isDirected: true);

Display.Graph3D(graph, title: ""Microservice Architecture"");"
                },
                new()
                {
                    Id = "doc_snippet_scatter3d",
                    Title = "3D Scatter Plot",
                    Description = "Plotting 3D coordinate points with hover inspection.",
                    Code = @"var points = new List<(double x, double y, double z)>
{
    (1.0, 2.0, 3.0),
    (4.0, 5.0, 6.0),
    (2.5, 3.5, 4.5)
};

Display.Scatter3D(points, title: ""PCA Clusters"");"
                }
            }
        };
    }
}
