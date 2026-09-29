using Avalonia;
using Avalonia.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Services;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Spatial;

namespace PdfEditorApp.Plugins.CSharpEditor.Tools.UiSnapshots;

/// <summary><c>plot3d [surface|scatter|graph|trajectory|voxel]</c>: renders off-screen interactive 3D visualizations.</summary>
internal static class Plot3DSnapshots
{
    public static void Render(Options options)
    {
        int width = options.Int("width", 960);
        int height = options.Int("height", 640);
        string mode = options.Value("mode") ?? options.Positional(0) ?? "surface";

        switch (mode.ToLowerInvariant())
        {
            case "scatter":
                RenderScatter(width, height);
                break;
            case "graph":
                RenderGraph(width, height);
                break;
            case "trajectory":
                RenderTrajectory(width, height);
                break;
            case "voxel":
                RenderVoxel(width, height);
                break;
            case "surface":
            default:
                RenderSurface(width, height);
                break;
        }
    }

    private static void RenderSurface(int width, int height)
    {
        var surfaceData = Surface3DData.FromFunction(
            (x, y) =>
            {
                double r = System.Math.Sqrt(x * x + y * y);
                return r == 0 ? 1.0 : System.Math.Sin(r * 2.5) / (r * 2.5);
            },
            minX: -2.5, maxX: 2.5,
            minY: -2.5, maxY: 2.5,
            resX: 32, resY: 32
        );

        var plotOptions = new Plot3DOptions
        {
            Title = "3D Sinc Surface Elevation (Viridis)",
            Type = Plot3DType.Surface,
            Surface = surfaceData,
            ColorMap = ColorMapPreset.Viridis,
            Wireframe = true,
            ShowBoundingBox = true,
            ShowAxes = true
        };

        var control = new InteractivePlot3DControl(plotOptions);
        var window = Snapshot.Show(control, width, height);
        Snapshot.Save(window, "plot3d_surface_viridis");
        window.Close();
    }

    private static void RenderScatter(int width, int height)
    {
        var series = new Series3D { Name = "Clusters" };
        var random = new Random(42);

        // Generate two 3D clusters
        for (int i = 0; i < 40; i++)
        {
            series.Points.Add(new Point3D(
                random.NextDouble() * 2 - 1,
                random.NextDouble() * 2 - 1,
                random.NextDouble() * 2 - 1,
                label: $"Cluster A - {i}",
                color: "#4FC3F7",
                size: 7.0));
        }

        for (int i = 0; i < 40; i++)
        {
            series.Points.Add(new Point3D(
                random.NextDouble() * 2 + 1.5,
                random.NextDouble() * 2 + 1.5,
                random.NextDouble() * 2 + 1.5,
                label: $"Cluster B - {i}",
                color: "#FF8A65",
                size: 8.0));
        }

        var plotOptions = new Plot3DOptions
        {
            Title = "3D Point Cloud Clusters (Scatter)",
            Type = Plot3DType.Scatter,
            Series = { series },
            ShowBoundingBox = true,
            ShowAxes = true
        };

        var control = new InteractivePlot3DControl(plotOptions);
        var window = Snapshot.Show(control, width, height);
        Snapshot.Save(window, "plot3d_scatter_clusters");
        window.Close();
    }

    private static void RenderGraph(int width, int height)
    {
        var graph = new Graph3DData();
        graph.Nodes.Add(new Graph3DNode("A", "Core Gateway", color: "#4CAF50") { Radius = 12.0 });
        graph.Nodes.Add(new Graph3DNode("B", "Auth Microservice", color: "#2196F3") { Radius = 10.0 });
        graph.Nodes.Add(new Graph3DNode("C", "Billing API", color: "#9C27B0") { Radius = 10.0 });
        graph.Nodes.Add(new Graph3DNode("D", "Postgres Primary", color: "#FF9800") { Radius = 11.0 });
        graph.Nodes.Add(new Graph3DNode("E", "Redis Cache", color: "#F44336") { Radius = 9.0 });
        graph.Nodes.Add(new Graph3DNode("F", "Analytics Worker", color: "#00BCD4") { Radius = 8.0 });

        graph.Edges.Add(new Graph3DEdge("A", "B", 1.0, color: "#90CAF9", isDirected: true));
        graph.Edges.Add(new Graph3DEdge("A", "C", 1.0, color: "#CE93D8", isDirected: true));
        graph.Edges.Add(new Graph3DEdge("B", "D", 0.8, color: "#FFE082", isDirected: true));
        graph.Edges.Add(new Graph3DEdge("B", "E", 1.2, color: "#EF9A9A", isDirected: true));
        graph.Edges.Add(new Graph3DEdge("C", "D", 0.9, color: "#FFE082", isDirected: true));
        graph.Edges.Add(new Graph3DEdge("C", "E", 0.5, color: "#EF9A9A", isDirected: true));
        graph.Edges.Add(new Graph3DEdge("D", "F", 0.4, color: "#80DEEA", isDirected: true));

        var plotOptions = new Plot3DOptions
        {
            Title = "Distributed System Topology (3D Force Directed Graph)",
            Type = Plot3DType.Graph3D,
            Graph = graph,
            ShowBoundingBox = false,
            ShowAxes = false
        };

        var control = new InteractivePlot3DControl(plotOptions);
        var window = Snapshot.Show(control, width, height);
        Snapshot.Save(window, "plot3d_graph_topology");
        window.Close();
    }

    private static void RenderTrajectory(int width, int height)
    {
        var series = new Series3D { Name = "Lorenz Attractor" };
        double x = 0.1, y = 0.0, z = 0.0;
        double dt = 0.01;
        double sigma = 10.0, rho = 28.0, beta = 8.0 / 3.0;

        for (int i = 0; i < 400; i++)
        {
            double dx = sigma * (y - x);
            double dy = x * (rho - z) - y;
            double dz = x * y - beta * z;
            x += dx * dt;
            y += dy * dt;
            z += dz * dt;
            series.Points.Add(new Point3D(x, y, z));
        }

        var plotOptions = new Plot3DOptions
        {
            Title = "Lorenz Strange Attractor (3D Trajectory Ribbon)",
            Type = Plot3DType.Trajectory,
            Series = { series },
            ColorMap = ColorMapPreset.Plasma,
            ShowBoundingBox = true,
            ShowAxes = true
        };

        var control = new InteractivePlot3DControl(plotOptions);
        var window = Snapshot.Show(control, width, height);
        Snapshot.Save(window, "plot3d_trajectory_lorenz");
        window.Close();
    }

    private static void RenderVoxel(int width, int height)
    {
        var series = new Series3D { Name = "Quarterly Distribution" };
        var random = new Random(123);

        for (int x = 0; x < 4; x++)
        {
            for (int y = 0; y < 4; y++)
            {
                double z = (x + 1) * (y + 1) * 0.5 + random.NextDouble() * 0.8;
                series.Points.Add(new Point3D(x, y, z, label: $"Q{x + 1}-R{y + 1}"));
            }
        }

        var plotOptions = new Plot3DOptions
        {
            Title = "3D Voxel Bar Matrix",
            Type = Plot3DType.VoxelBar,
            Series = { series },
            ColorMap = ColorMapPreset.CoolWarm,
            ShowBoundingBox = true,
            ShowAxes = true
        };

        var control = new InteractivePlot3DControl(plotOptions);
        var window = Snapshot.Show(control, width, height);
        Snapshot.Save(window, "plot3d_voxel_matrix");
        window.Close();
    }
}
