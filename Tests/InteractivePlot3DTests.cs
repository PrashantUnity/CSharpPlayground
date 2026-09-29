using System;
using System.Collections.Generic;
using System.Text.Json;
using Avalonia;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Layouts;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Renderers;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Services;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Spatial;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;
using Vector3D = PdfEditorApp.Plugins.CSharpEditor.Charting3D.Spatial.Vector3D;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

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
        Assert.NotNull(emitted.Plot3DOptions);
        Assert.Equal("Scatter 3D Demo", emitted.Plot3DOptions.Title);
        Assert.Equal(Plot3DType.Scatter, emitted.Plot3DOptions.Type);
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
        Assert.Equal("Dumped 3D", emitted.Plot3DOptions?.Title);
        Assert.Equal(2, emitted.Plot3DOptions?.Series[0].Points.Count);
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
        Assert.Contains("three.min.js", html);
        Assert.Contains("OrbitControls.js", html);
        Assert.Contains("Exported 3D View", html);
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
        Assert.Equal("Polyglot 3D", output.Rich.Plot3DOptions?.Title);
        Assert.Equal(Plot3DType.Scatter, output.Rich.Plot3DOptions?.Type);
        Assert.Equal(2, output.Rich.Plot3DOptions?.Series[0].Points.Count);
    }

    [Fact]
    public void NotebookCellViewModel_ShouldSupportPlot3DOutputTab()
    {
        var item = new Models.NotebookCellItem { Type = Models.CellType.Code, Source = "Display.Plot3D(...);" };
        var vm = new NotebookCellViewModel(item);
        var plotOpts = new Plot3DOptions { Title = "Cell 3D Output" };

        vm.SetPlot3DOutput(plotOpts);

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
        var control = new Charting3D.Controls.InteractivePlot3DControl(opts);

        Assert.Same(opts, control.Options);
    }

    [Fact]
    public void InteractivePlot3DWindow_ShouldInitializeWithTitleAndOptions()
    {
        var opts = new Plot3DOptions { Title = "Immersive Chaos Attractor" };
        try
        {
            var win = new Charting3D.Controls.InteractivePlot3DWindow(opts);
            Assert.Contains("Immersive Chaos Attractor", win.Title);
            Assert.Equal(Avalonia.Controls.WindowState.Normal, win.WindowState);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("IWindowingPlatform"))
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
        var opts = Display.Surface3D(
            (x, y) => {
                double r = Math.Sqrt(x * x + y * y);
                return r == 0 ? 1.0 : Math.Sin(r * 2.5) / (r * 2.5);
            },
            minX: -3, maxX: 3, minY: -3, maxY: 3, resX: 35, resY: 35,
            colorMap: ColorMapPreset.Viridis, wireframe: true, title: "3D Sinc Surface");

        Assert.NotNull(opts);
        Assert.Equal("3D Sinc Surface", opts.Title);
        Assert.True(opts.Wireframe);
        Assert.Equal(ColorMapPreset.Viridis, opts.ColorMap);
        Assert.NotNull(opts.Surface);
        Assert.Equal(35, opts.Surface.ResolutionX);
        Assert.Equal(35, opts.Surface.ResolutionY);
        Assert.Equal(-3, opts.Surface.MinX);
        Assert.Equal(3, opts.Surface.MaxX);

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
}

