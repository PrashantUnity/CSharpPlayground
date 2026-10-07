using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Documentation;

public partial class DocumentationService
{
    private DocArticle CreateChartsQuickStartArticle()
    {
        return new DocArticle
        {
            Id = "charts_quickstart",
            Title = "Charts & Plots Quick Start",
            Subtitle = "Line, bar, pie and histogram charts, and 3D surfaces and point clouds, in one line each.",
            ReadingTime = "4 min read",
            Summary = "Pass your data to Display.LineChart, Display.BarChart, Display.Surface3D and friends, or build a chart one setting at a time with Charts.Line(...).Title(...).Show(). Every chart and plot is interactive: zoom, pan, orbit, hover, fullscreen, copy as CSV or PNG.",
            Keywords = new List<string> { "chart", "charts", "plot", "plots", "line", "bar", "pie", "histogram", "scatter", "surface", "3d", "axis", "label", "legend", "fluent", "builder", "quickstart" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Two ways to say the same thing",
                    Content = "Display.LineChart(data, title, xLabel: ...) draws a chart in one call. Charts.Line(data).Title(...).XLabel(...).Show() builds it one setting at a time. They draw exactly the same chart, so use whichever reads better.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "In a notebook cell, the last expression is shown by itself: Charts.Line(sales).Title(\"Sales\") needs no .Show()."
                },
                new()
                {
                    Heading = "Give it your data as it is",
                    Content = "Numbers, [x, y] pairs, a label → number map (bar and pie), a name → numbers map (a series each) or records with an x and a value all work. Settings you leave out are the studio's defaults.",
                    BulletPoints = new List<string>
                    {
                        "Names and sizes: title, xLabel, yLabel, width, height, legend, color",
                        "Kinds: LineChart, AreaChart, BarChart, ScatterChart, PieChart, DonutChart, Histogram",
                        "Anything else in the spec: configure: s => s.Grid = false, or .Configure(s => ...)"
                    }
                },
                new()
                {
                    Heading = "3D: z is always up",
                    Content = "Surface3D takes a function and two ranges; Scatter3D, Trajectory3D and VoxelBar3D take points; Graph3D takes nodes and edges. Drag to orbit, Shift+drag to pan, scroll to zoom."
                },
                new()
                {
                    Heading = "Change a chart after it is shown",
                    Content = "Display calls and .Show() return a handle. handle.Update(s => ...) redraws the chart in place, and handle.OnClick(...) runs code when someone clicks a bar or point."
                }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_charts_first_chart",
                    Title = "Your first chart",
                    Description = "The same line chart, as one call and as a builder.",
                    Code = """
                        var sales = new[] { 12, 18, 9, 24, 30, 27 };

                        Display.LineChart(sales, "Sales (one call)", xLabel: "Month", yLabel: "USD (k)");

                        Charts.Line(sales)
                            .Title("Sales (builder)")
                            .XLabel("Month")
                            .YLabel("USD (k)")
                            .Size(640, 300)
                            .Show();
                        """
                },
                new()
                {
                    Id = "snip_charts_shapes",
                    Title = "Bars, slices and histograms",
                    Description = "A label → number map is a bar or a pie; a list of samples is a histogram.",
                    Code = """
                        var browsers = new Dictionary<string, double>
                        {
                            ["Chrome"] = 62, ["Safari"] = 21, ["Edge"] = 11, ["Other"] = 6
                        };

                        Display.BarChart(browsers, "Browser share", yLabel: "%");
                        Display.PieChart(browsers, "Browser share");

                        var latencies = Enumerable.Range(0, 400)
                            .Select(i => 40 + 25 * Math.Sin(i * 0.7) + (i % 17))
                            .ToArray();

                        Charts.Histogram(latencies).Title("Latency").XLabel("ms").Bins(12).Show();
                        """
                },
                new()
                {
                    Id = "snip_charts_series_and_records",
                    Title = "Several series, and records",
                    Description = "Name each series, or chart records by choosing their x and y.",
                    Code = """
                        var months = new[] { "Jan", "Feb", "Mar", "Apr" };

                        Charts.Bar(new[] { 4, 7, 5, 9 }, "2025")
                            .Series("2026", new[] { 6, 8, 9, 12 }, color: "#4ec9b0")
                            .Title("Orders by month")
                            .YLabel("orders")
                            .Show();

                        var revenue = months.Select((m, i) => new { Month = m, Total = 100 + i * 35 }).ToList();
                        Charts.Line(revenue, r => r.Month, r => r.Total, "Revenue").Title("Revenue").Show();
                        """
                },
                new()
                {
                    Id = "snip_plots3d_surface",
                    Title = "A 3D surface",
                    Description = "z = f(x, y) over two ranges, with named axes and a colour map.",
                    Code = """
                        Display.Surface3D(
                            (x, y) => Math.Sin(x) * Math.Cos(y),
                            xRange: (-5, 5),
                            yRange: (-5, 5),
                            title: "Wave",
                            zLabel: "height",
                            colorMap: ColorMapPreset.Plasma);

                        Charts.Wireframe((x, y) => x * x - y * y, (-3, 3), (-3, 3))
                            .Title("Saddle")
                            .ZLabel("z")
                            .Show();
                        """
                },
                new()
                {
                    Id = "snip_plots3d_points",
                    Title = "A 3D point cloud",
                    Description = "Records with an x, y and z, labelled for hover.",
                    Code = """
                        var rng = new Random(7);
                        var cities = Enumerable.Range(1, 40)
                            .Select(i => new { Name = $"City {i}", X = rng.NextDouble() * 10, Y = rng.NextDouble() * 10, Z = rng.NextDouble() * 5 })
                            .ToList();

                        Charts.Scatter3D(cities, c => c.X, c => c.Y, c => c.Z, c => c.Name)
                            .Title("Cities")
                            .XLabel("east").YLabel("north").ZLabel("altitude")
                            .Show();
                        """
                },
                new()
                {
                    Id = "snip_charts_update",
                    Title = "Update a chart while it runs",
                    Description = "The handle redraws the chart in place; the studio draws at most about 30 times a second.",
                    Code = """
                        var values = new List<double> { 1 };
                        var chart = Display.LineChart(values, "Growing", xLabel: "step");

                        for (var i = 2; i <= 12; i++)
                        {
                            Display.ThrowIfCancellationRequested();
                            await Task.Delay(150);
                            var next = values[^1] * 1.3 + i % 3;
                            values.Add(next);
                            chart.Update(s => s.Series[0].Y.Add(next));
                        }
                        """
                }
            }
        };
    }
}
