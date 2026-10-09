using System.Text.Json;
using Avalonia;
using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Layouts;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Renderers;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Services;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Spatial;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Output;
using Xunit;
using NotebookCellViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Notebooks.NotebookCellViewModel;
using Vector3D = PdfEditorApp.Plugins.CSharpEditor.Charting3D.Spatial.Vector3D;

namespace CSharpEditorPlugin.Tests;

public class InteractivePlot3DTests
{
    [Fact]
    public void Vector3D_Operations_ShouldBeCorrect()
    {
        var v1 = new Vector3D(1, 2, 3);
        var v2 = new Vector3D(4, 5, 6);

        var add = v1 + v2;
        Assert.Equal(5, add.X);
        Assert.Equal(7, add.Y);
        Assert.Equal(9, add.Z);

        var sub = v2 - v1;
        Assert.Equal(3, sub.X);
        Assert.Equal(3, sub.Y);
        Assert.Equal(3, sub.Z);

        double dot = Vector3D.Dot(v1, v2);
        Assert.Equal(1 * 4 + 2 * 5 + 3 * 6, dot);

        var cross = Vector3D.Cross(Vector3D.UnitX, Vector3D.UnitY);
        Assert.Equal(Vector3D.UnitZ, cross);

        var norm = new Vector3D(0, 3, 4).Normalized;
        Assert.Equal(0, norm.X);
        Assert.Equal(0.6, norm.Y, 5);
        Assert.Equal(0.8, norm.Z, 5);
        Assert.Equal(1.0, norm.Length, 5);
    }

    [Fact]
    public void Matrix4x4D_TranslationAndScale_ShouldTransformVector()
    {
        var trans = Matrix4x4D.CreateTranslation(10, 20, 30);
        var p = new Vector3D(1, 2, 3);
        var transformed = trans.Transform(p);

        Assert.Equal(11, transformed.X);
        Assert.Equal(22, transformed.Y);
        Assert.Equal(33, transformed.Z);

        var scale = Matrix4x4D.CreateScale(2, 3, 4);
        var scaled = scale.Transform(p);
        Assert.Equal(2, scaled.X);
        Assert.Equal(6, scaled.Y);
        Assert.Equal(12, scaled.Z);
    }

    [Fact]
    public void Camera3D_OrbitAndPresets_ShouldUpdateEyePosition()
    {
        var camera = new Camera3D();
        camera.SetIsometric();

        var eye = camera.GetEyePosition();
        Assert.True(eye.X > 0);
        Assert.True(eye.Y > 0);
        Assert.True(eye.Z > 0);

        camera.SetFront();
        eye = camera.GetEyePosition();
        Assert.Equal(0, eye.X, 3);
        Assert.Equal(0, eye.Y, 3);
        Assert.True(eye.Z > 0);

        camera.SetTop();
        eye = camera.GetEyePosition();
        Assert.True(eye.Y > 0);
        Assert.Equal(camera.Distance, eye.Y, 1);
    }

    [Fact]
    public void Projector3D_ProjectWorld_ShouldProduceValidScreenCoordinates()
    {
        var options = new Plot3DOptions();
        options.Camera.SetIsometric();
        var viewport = new Rect(0, 0, 800, 600);
        var projector = new Projector3D(viewport, options);

        var originProj = projector.ProjectWorld(Vector3D.Zero);
        Assert.True(originProj.IsVisible);
        Assert.True(originProj.ScreenPoint.X > 100 && originProj.ScreenPoint.X < 700);
        Assert.True(originProj.ScreenPoint.Y > 100 && originProj.ScreenPoint.Y < 500);
    }

    [Fact]
    public void Ray3D_DistanceToPoint_ShouldCalculateCorrectDistance()
    {
        var ray = new Ray3D(new Vector3D(0, 0, 0), Vector3D.UnitZ);
        var ptOnRay = new Vector3D(0, 0, 10);
        Assert.Equal(0, ray.DistanceToPoint(ptOnRay), 5);

        var ptOffRay = new Vector3D(3, 4, 10);
        Assert.Equal(5.0, ray.DistanceToPoint(ptOffRay), 5);
    }

    [Fact]
    public void ColorMapService_ShouldInterpolateColorsCorrectly()
    {
        var colStart = ColorMapService.GetColor(ColorMapPreset.Viridis, 0.0);
        var colEnd = ColorMapService.GetColor(ColorMapPreset.Viridis, 1.0);
        var hex = ColorMapService.GetHexColor(ColorMapPreset.Plasma, 0.5);

        Assert.NotNull(hex);
        Assert.StartsWith("#", hex);
        Assert.NotEqual(colStart, colEnd);
    }

    [Fact]
    public void TheNamedColourMaps_AreThePublishedPalettes()
    {
        Assert.Equal("#440154", ColorMapService.GetHexColor(ColorMapPreset.Viridis, 0));
        Assert.Equal("#FDE725", ColorMapService.GetHexColor(ColorMapPreset.Viridis, 1));
        Assert.Equal("#0D0887", ColorMapService.GetHexColor(ColorMapPreset.Plasma, 0));
        Assert.Equal("#F0F921", ColorMapService.GetHexColor(ColorMapPreset.Plasma, 1));
        Assert.Equal("#30123B", ColorMapService.GetHexColor(ColorMapPreset.Turbo, 0));
        Assert.Equal("#3B4CC0", ColorMapService.GetHexColor(ColorMapPreset.CoolWarm, 0));
        Assert.Equal("#B40426", ColorMapService.GetHexColor(ColorMapPreset.CoolWarm, 1));
    }

    [Fact]
    public void Plot3DDataParser_ShouldParseTuples_IntoSeries3D()
    {
        var points = new List<(double x, double y, double z)>
        {
            (1.0, 2.0, 3.0),
            (4.0, 5.0, 6.0),
            (7.0, 8.0, 9.0)
        };

        var opts = Plot3DDataParser.Parse(points, title: "Test 3D Points", color: "#4ec9b0");
        Assert.Equal("Test 3D Points", opts.Title);
        Assert.Single(opts.Series);
        Assert.Equal(3, opts.Series[0].Points.Count);
        Assert.Equal(1.0, opts.Series[0].Points[0].X);
        Assert.Equal(2.0, opts.Series[0].Points[0].Y);
        Assert.Equal(3.0, opts.Series[0].Points[0].Z);
        Assert.Equal(1.0, opts.MinX);
        Assert.Equal(7.0, opts.MaxX);
    }

    [Fact]
    public void Plot3DDataParser_ShouldParseMathematicalFunction_IntoSurface3D()
    {
        Func<double, double, double> ripple = (x, y) => Math.Sin(Math.Sqrt(x * x + y * y));
        var opts = Plot3DDataParser.Parse(ripple, title: "Ripple Surface");

        Assert.NotNull(opts.Surface);
        Assert.Equal(Plot3DType.Surface, opts.Type);
        Assert.True(opts.Surface.ResolutionX >= 30);
        Assert.True(opts.Surface.ResolutionY >= 30);
    }

    [Fact]
    public void ForceDirected3DLayout_ShouldDisperseNodesIn3DSpace()
    {
        var graph = new Graph3DData();
        graph.AddNode("A", "Node A");
        graph.AddNode("B", "Node B");
        graph.AddNode("C", "Node C");
        graph.AddEdge("A", "B");
        graph.AddEdge("B", "C");

        ForceDirected3DLayout.ComputeLayout(graph, iterations: 30);

        Assert.Equal(3, graph.Nodes.Count);
        // Verify nodes are not all clustered at origin
        double totalDist = 0;
        foreach (var n in graph.Nodes)
        {
            totalDist += Math.Sqrt(n.X * n.X + n.Y * n.Y + n.Z * n.Z);
        }
        Assert.True(totalDist > 0.5, "Nodes should disperse in 3D space");
    }

    [Fact]
    public void Scatter3DRenderer_HitTest_ShouldFindClosestPoint()
    {
        var opts = new Plot3DOptions();
        opts.Camera.SetIsometric();
        var series = new Series3D();
        series.Points.Add(new Point3D(0, 0, 0, "Center Point"));
        opts.Series.Add(series);

        var bounds = new Rect(0, 0, 800, 600);
        var projector = new Projector3D(bounds, opts);
        var centerScreen = projector.ProjectData(0, 0, 0).ScreenPoint;

        var renderer = new Scatter3DRenderer();
        var hit = renderer.HitTest(new Point(centerScreen.X + 2, centerScreen.Y + 2), bounds, opts);

        Assert.NotNull(hit);
        Assert.Equal("Center Point", hit.Point?.Label);
    }

    [Fact]
    public void Display_Plot3DAndScatter3D_ShouldEmitRichCellOutput()
    {
        RichCellOutput? emitted = null;
        using var scope = InteractiveDisplayContext.EnterScope(output => emitted = output);

        var points = new List<(double, double, double)> { (1, 2, 3), (4, 5, 6) };
        Display.Scatter3D(points, title: "Scatter 3D Demo", color: "#569cd6");

        Assert.NotNull(emitted);
        Assert.Equal(CellOutputKind.Plot3D, emitted.Kind);
        Assert.Equal("Scatter 3D Demo", emitted.Plot3DSpec().Title);
        Assert.Equal(Plot3DType.Scatter, emitted.Plot3DSpec().Kind);
    }

    [Fact]
    public void Display_Dump3D_ShouldWorkWithSelectorDelegates()
    {
        RichCellOutput? emitted = null;
        using var scope = InteractiveDisplayContext.EnterScope(output => emitted = output);

        var data = new[]
        {
            new { X = 1.0, Y = 2.0, Z = 3.0, Name = "Item 1" },
            new { X = 4.0, Y = 5.0, Z = 6.0, Name = "Item 2" }
        };

        data.Dump3D(d => d.X, d => d.Y, d => d.Z, d => d.Name, title: "Dumped 3D");

        Assert.NotNull(emitted);
        Assert.Equal(CellOutputKind.Plot3D, emitted.Kind);
        Assert.Equal("Dumped 3D", emitted.Plot3DSpec().Title);
        Assert.Equal(2, emitted.Plot3DSpec().Series[0].X.Count);
    }

    [Fact]
    public void Plot3DHtmlExporter_ShouldGenerateValidThreeJsDocument()
    {
        var opts = new Plot3DOptions { Title = "Exported 3D View" };
        var series = new Series3D();
        series.Points.Add(new Point3D(1, 2, 3));
        opts.Series.Add(series);

        string html = Plot3DHtmlExporter.GenerateThreeJsHtml(opts);

        Assert.NotNull(html);
        Assert.Contains("<!DOCTYPE html>", html);
        // three.js r160 ships its controls as modules only; the old examples/js path answers 404, so the page broke.
        Assert.Contains("three@0.160.0/build/three.module.js", html);
        Assert.Contains("three@0.160.0/examples/jsm/controls/OrbitControls.js", html);
        Assert.Contains("camera.up.set(0, 0, 1)", html);
        Assert.Contains("Exported 3D View", html);
    }

    // A title or a node id went into the page as markup and as JavaScript names, so an odd one broke it.
    [Fact]
    public void Plot3DHtmlExporter_KeepsTextAsData()
    {
        var graph = new Graph3DData();
        graph.AddNode("a-b c", "</script><b>x</b>", 0, 0, 0);
        graph.AddNode("d", "d", 1, 1, 1);
        graph.AddEdge("a-b c", "d");
        var opts = new Plot3DOptions { Title = "<img src=x onerror=alert(1)>", Type = Plot3DType.Graph3D, Graph = graph };
        opts.RecalculateBounds();

        string html = Plot3DHtmlExporter.GenerateThreeJsHtml(opts);

        Assert.DoesNotContain("<img", html);
        Assert.DoesNotContain("</script><b>", html);
        Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", html);
    }

    [Fact]
    public void Plot3DHtmlExporter_DrawsEverySeries_AndLeavesHolesInASurface()
    {
        var points = new Plot3DOptions { Series = { new Series3D { Points = { new Point3D(0, 0, 0), new Point3D(double.NaN, 1, 1) } }, new Series3D { Points = { new Point3D(1, 1, 1) } } } };
        points.RecalculateBounds();
        var surface = new Plot3DOptions { Type = Plot3DType.Surface, Surface = Surface3DData.FromGrid(new[,] { { 1, 2, 3 }, { 4, double.NaN, 6 }, { 7, 8, 9 } }) };
        surface.RecalculateBounds();

        var cloud = Plot3DHtmlExporter.Scene(points).Clouds;
        var mesh = Plot3DHtmlExporter.Scene(surface).Surface!;

        Assert.Equal([3, 3], cloud.Select(c => c.Positions.Count));
        Assert.Equal(9, mesh.Colors.Count);
        Assert.Empty(mesh.Triangles); // every quad of a 3 × 3 grid touches its middle
    }

    [Fact]
    public void MimeOutputMapper_ShouldMapPlot3DJsonPayload()
    {
        string json = @"{
            ""application/vnd.fry.plot3d+json"": {
                ""title"": ""Polyglot 3D"",
                ""type"": ""scatter"",
                ""points"": [
                    [1.0, 2.0, 3.0],
                    [4.0, 5.0, 6.0]
                ]
            }
        }";

        using var doc = JsonDocument.Parse(json);
        using var meta = JsonDocument.Parse("{}");

        var output = MimeOutputMapper.Map(doc.RootElement, meta.RootElement);

        Assert.NotNull(output.Rich);
        Assert.Equal(CellOutputKind.Plot3D, output.Rich.Kind);
        Assert.Equal("Polyglot 3D", output.Rich.Plot3DSpec().Title);
        Assert.Equal(Plot3DType.Scatter, output.Rich.Plot3DSpec().Kind);
        Assert.Equal(2, output.Rich.Plot3DSpec().Series[0].X.Count);
    }

    [Fact]
    public void NotebookCellViewModel_ShouldSupportPlot3DOutputTab()
    {
        var item = new PdfEditorApp.Plugins.CSharpEditor.Models.NotebookCellItem { Type = PdfEditorApp.Plugins.CSharpEditor.Models.CellType.Code, Source = "Display.Plot3D(...);" };
        var vm = new NotebookCellViewModel(item);
        vm.AddVisual(VisualOutput.Create(new Plot3DSpec { Title = "Cell 3D Output" }));

        Assert.True(vm.HasPlot3DOutput);
        Assert.True(vm.IsPlot3DTabActive);
        Assert.True(vm.IsPlot3DTabSelected);
        Assert.Equal("Cell 3D Output", vm.SingleOutputTitle);
        Assert.Equal("CubeOutline", vm.SingleOutputIconKind);
    }

    [Fact]
    public void RoslynCompilerService_ShouldCompileScriptReferencingPoint3DWithoutExplicitUsings()
    {
        var compiler = new RoslynCompilerService();
        var code = "var pts = new List<Point3D>(); pts.Add(new Point3D(1, 2, 3)); Console.WriteLine(pts.Count);";
        var (success, _, diagnostics) = compiler.CompileToAssembly(code, ExecutionLanguageMode.Statements);

        Assert.True(success, string.Join("; ", diagnostics.Select(d => d.Message)));
    }

    [Fact]
    public async Task NotebookExecutionKernel_ShouldExecuteCellReferencingPoint3DWithoutExplicitUsings()
    {
        using var kernel = new NotebookExecutionKernel();
        var code = "var pt = new Point3D(10, 20, 30); pt.X + pt.Y";
        var result = await kernel.ExecuteCellAsync(code);

        Assert.True(result.Success, result.ErrorMessage);
    }

    [Fact]
    public void InteractivePlot3DControl_ShouldInitializeWithOptionsAndControls()
    {
        var opts = new Plot3DOptions { Title = "Interactive Test Plot", Height = 420 };
        var control = new PdfEditorApp.Plugins.CSharpEditor.Charting3D.Controls.InteractivePlot3DControl(opts);

        Assert.Same(opts, control.Options);
    }

    [Fact]
    public void InteractivePlot3DWindow_ShouldInitializeWithTitleAndOptions()
    {
        var opts = new Plot3DOptions { Title = "Immersive Chaos Attractor" };
        try
        {
            var win = new PdfEditorApp.Plugins.CSharpEditor.Charting3D.Controls.InteractivePlot3DWindow(opts);
            Assert.Contains("Immersive Chaos Attractor", win.Title);
            Assert.Equal(Avalonia.Controls.WindowState.Normal, win.WindowState);
        }
        catch (Exception ex) when (ex is InvalidOperationException or NullReferenceException)
        {
            // Expected in headless test runner without OS windowing platform registered
        }
    }

    [Fact]
    public void Plot3DOptions_ColorMapPresets_AllResolveCorrectly()
    {
        foreach (ColorMapPreset preset in Enum.GetValues<ColorMapPreset>())
        {
            var midColor = ColorMapService.GetColor(preset, 0.5);
            Assert.True(midColor.A > 0);
        }
    }

    [Fact]
    public void Display_Surface3D_WithDiscreteMinMaxParameters_ShouldWorkAndCompile()
    {
        var handle = Display.Surface3D(
            (x, y) => {
                double r = Math.Sqrt(x * x + y * y);
                return r == 0 ? 1.0 : Math.Sin(r * 2.5) / (r * 2.5);
            },
            minX: -3, maxX: 3, minY: -3, maxY: 3, resX: 35, resY: 35,
            colorMap: ColorMapPreset.Viridis, wireframe: true, title: "3D Sinc Surface");

        var spec = Assert.IsType<Plot3DSpec>(handle.Spec);
        Assert.Equal("3D Sinc Surface", spec.Title);
        Assert.Equal(Plot3DType.Wireframe, spec.Kind);
        Assert.Equal(ColorMapPreset.Viridis, spec.ColorMap);
        Assert.NotNull(spec.Surface);
        Assert.Equal(35, spec.Surface.Z.Count);
        Assert.Equal(35, spec.Surface.Z[0].Count);
        Assert.Equal(-3, spec.Surface.X.Min);
        Assert.Equal(3, spec.Surface.X.Max);

        // Also verify Roslyn compiles this exact user code snippet
        var compiler = new RoslynCompilerService();
        var code = @"Display.Surface3D((x, y) => {
    double r = Math.Sqrt(x * x + y * y);
    return r == 0 ? 1.0 : Math.Sin(r * 2.5) / (r * 2.5);
}, minX: -3, maxX: 3, minY: -3, maxY: 3, resX: 35, resY: 35,
   colorMap: ColorMapPreset.Viridis, wireframe: true, title: ""3D Sinc Surface"");";
        var (success, _, diagnostics) = compiler.CompileToAssembly(code, ExecutionLanguageMode.Statements);
        Assert.True(success, string.Join("; ", diagnostics.Select(d => d.Message)));
    }

    // Every 3D kind draws data z upward (the matplotlib/Plotly convention), so the same (x, y, z) data looks the same as
    // points, as a trajectory and as a surface.
    [Fact]
    public void Points_DrawDataZUpward()
    {
        var options = new Plot3DOptions { MinX = -1, MaxX = 1, MinY = -1, MaxY = 1, MinZ = -1, MaxZ = 1 };
        var projector = new Projector3D(new Rect(0, 0, 400, 400), options);

        var top = projector.MapDataToWorld(0, 0, 1);
        var side = projector.MapDataToWorld(0, 1, 0);

        Assert.Equal(Projector3D.WorldBoxSize / 2, top.Y, 6);
        Assert.Equal(0, side.Y, 6);
    }

    // A surface z = f(x, y) keeps x, y and z as they are: its height is the plot's z range, not its y range.
    [Fact]
    public void ASurface_KeepsItsHeightOnTheZAxis()
    {
        var options = new Plot3DOptions { Surface = Surface3DData.FromFunction((x, y) => 100, -2, 2, -3, 3, 5, 5) };

        options.RecalculateBounds();

        Assert.Equal((-2.0, 2.0), (options.MinX, options.MaxX));
        Assert.Equal((-3.0, 3.0), (options.MinY, options.MaxY));
        Assert.InRange(options.MinZ, 98.9, 100);
        Assert.InRange(options.MaxZ, 100, 101.1);
    }
}

