using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Documentation;

public partial class DocumentationService
{
    private DocCategory BuildEChartCategory()
    {
        return new DocCategory
        {
            Id = "echart_visual_computing",
            Title = "ECharts: Interactive Web Charts",
            IconKind = MaterialIconKind.ChartLine,
            AccentColor = "#38BDF8",
            Badge = "EChart • C#",
            Description = "Charts drawn by Apache ECharts in a web view, 3D ones with WebGL: hover, zoom, legends and the whole ECharts option when you need it.",
            Articles = new List<DocArticle>
            {
                CreateEChartQuickstartArticle(),
                CreateEChart2DArticle(),
                CreateEChartTreesAndFlowsArticle(),
                CreateEChart3DArticle(),
                CreateEChartFullOptionArticle()
            }
        };
    }

    private DocArticle CreateEChartQuickstartArticle()
    {
        return new DocArticle
        {
            Id = "echart_quickstart",
            Title = "EChart Quick Start",
            Subtitle = "Pick a chart, give it data, add settings, show it.",
            ReadingTime = "3 min read",
            Summary = "EChart reads data the way Charts does, then draws it with Apache ECharts: tooltips, legends, zoom and saving as an image come with it.",
            Keywords = new List<string> { "echart", "echarts", "quickstart", "chart", "line", "bar", "pie", "toechart", "dumpechart", "csharp" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "One shape for every chart",
                    Content = "Start from EChart.Line, EChart.Bar, EChart.Pie, EChart.Surface and the rest, chain the settings you want (Title, XLabel, Legend, Zoom, Toolbox…), and end with Show(). A chart returned as a cell's last value shows without Show().",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "The data reads as it does for Charts: numbers, [x, y] pairs, a label → number map, a name → sequence map (a series each), tuples, and records whose members are found by name."
                },
                new()
                {
                    Heading = "From any data",
                    Content = "data.ToEChart(EChartType.Bar) gives the chart to set up; data.DumpEChart(\"Title\", EChartType.Bar) shows it and returns the data, so a query can go on."
                }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "doc_snippet_echart_numbers",
                    Title = "A Line Of Numbers",
                    Description = "Numbers in, a smooth line out.",
                    Code = """
                    var readings = new[] { 14, 28, 45, 32, 68, 85, 92, 74 };

                    EChart.Line(readings, "Sensor")
                        .Title("Weekly readings")
                        .XLabel("Day")
                        .YLabel("Value")
                        .Smooth()
                        .Show();
                    """
                },
                new()
                {
                    Id = "doc_snippet_echart_records",
                    Title = "Records, Two Selectors",
                    Description = "Each record is a bar: its place from one member, its height from another.",
                    Code = """
                    var sales = new[]
                    {
                        new { Region = "Americas", Revenue = 420_000 },
                        new { Region = "Europe", Revenue = 310_000 },
                        new { Region = "Asia", Revenue = 540_000 }
                    };

                    EChart.Bar(sales, s => s.Region, s => s.Revenue, "Revenue")
                        .Title("Revenue by region")
                        .ValueLabels()
                        .Show();
                    """
                },
                new()
                {
                    Id = "doc_snippet_echart_extensions",
                    Title = "Straight From The Data",
                    Description = "ToEChart returns the chart to set up; DumpEChart shows it and hands the data back.",
                    Code = """
                    var share = new Dictionary<string, double>
                    {
                        ["Chrome"] = 64.2, ["Safari"] = 18.5,
                        ["Edge"] = 5.2, ["Other"] = 12.1
                    };

                    share.ToEChart(EChartType.Donut).Title("Browsers").Show();

                    var total = share.DumpEChart("Same data, as bars", EChartType.Bar)
                        .Sum(kv => kv.Value);
                    Console.WriteLine($"Total: {total}");
                    """
                },
                new()
                {
                    Id = "doc_snippet_echart_returned",
                    Title = "Returned As The Last Value",
                    Description = "A chart that ends a cell is shown without Show().",
                    Code = """
                    EChart.Area(("Visits", new[] { 120.0, 132, 101, 134 }),
                                ("Sign-ups", new[] { 22.0, 18, 19, 23 }))
                        .Title("Traffic")
                        .Stacked()
                    """
                }
            }
        };
    }

    private DocArticle CreateEChart2DArticle()
    {
        return new DocArticle
        {
            Id = "echart_2d_charts",
            Title = "EChart 2D Charts",
            Subtitle = "Lines, bars, pies, radars, heatmaps, candlesticks and gauges.",
            ReadingTime = "5 min read",
            Summary = "Every 2D factory with the settings that matter for it: series of other kinds on the same axes, stacking, lying bars on their side, zoom and the toolbox.",
            Keywords = new List<string> { "echart", "bar", "line", "stacked", "horizontal", "zoom", "toolbox", "radar", "heatmap", "candlestick", "gauge", "funnel" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Several series, several kinds",
                    Content = "Series(name, data, kind) adds a series to a chart with axes; it lines up with the categories already there. Stacked() piles them up, Horizontal() lays the chart on its side, Zoom() adds the wheel and a slider, Toolbox() the save and data-view buttons."
                },
                new()
                {
                    Heading = "Charts with their own shape",
                    Content = "Radar takes one spoke per label; Heatmap a grid (a row per y, the first at the top); Candlestick [open, close, low, high] items or records with those members; Gauge one number."
                }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "doc_snippet_echart_dashboard",
                    Title = "Bars And A Line, With Zoom",
                    Description = "Revenue as bars and profit as a line on the same quarters.",
                    Code = """
                    var revenue = new Dictionary<string, double>
                    {
                        ["Q1"] = 120, ["Q2"] = 155, ["Q3"] = 190,
                        ["Q4"] = 245, ["Q5"] = 280, ["Q6"] = 310
                    };
                    var profit = revenue.ToDictionary(q => q.Key, q => q.Value * 0.4);

                    EChart.Bar(revenue, "Revenue")
                        .Series("Profit", profit, EChartType.Line)
                        .Title("Revenue and profit")
                        .Subtitle("thousands of dollars")
                        .Zoom()
                        .Toolbox()
                        .Show();
                    """
                },
                new()
                {
                    Id = "doc_snippet_echart_stacked_horizontal",
                    Title = "Stacked, On Its Side",
                    Description = "Three named series piled up, the categories down the left.",
                    Code = """
                    EChart.Bar(("Direct", new[] { 320.0, 302, 301 }),
                               ("Email", new[] { 120.0, 132, 101 }),
                               ("Search", new[] { 220.0, 182, 191 }))
                        .Labels(["Mon", "Tue", "Wed"])
                        .Horizontal()
                        .Stacked()
                        .Title("Visits by source")
                        .Show();
                    """
                },
                new()
                {
                    Id = "doc_snippet_echart_radar",
                    Title = "Radar",
                    Description = "A polygon per series over named spokes.",
                    Code = """
                    var phones = new Dictionary<string, double[]>
                    {
                        ["Phone A"] = [8, 6, 9, 5, 7],
                        ["Phone B"] = [6, 9, 7, 8, 6]
                    };

                    EChart.Radar(phones,
                            ["Camera", "Battery", "Screen", "Price", "Speed"],
                            max: 10)
                        .Title("Phones")
                        .Show();
                    """
                },
                new()
                {
                    Id = "doc_snippet_echart_heatmap",
                    Title = "Heatmap",
                    Description = "A grid of numbers, coloured by value, with its labels.",
                    Code = """
                    var confusion = new double[,]
                    {
                        { 50, 2, 1 },
                        { 3, 45, 4 },
                        { 0, 5, 48 }
                    };
                    string[] classes = ["cat", "dog", "fox"];

                    EChart.Heatmap(confusion, classes, classes)
                        .Title("Predicted (across) vs actual (down)")
                        .ColorMap(ColorMapPreset.Plasma)
                        .Show();
                    """
                },
                new()
                {
                    Id = "doc_snippet_echart_candlestick",
                    Title = "Candlesticks From Records",
                    Description = "Prices of each day: open, close, low and high.",
                    Code = """
                    var start = new DateTime(2026, 1, 5);
                    var rnd = new Random(3);
                    var price = 100.0;
                    var days = Enumerable.Range(0, 30).Select(i =>
                    {
                        var open = price;
                        price += rnd.NextDouble() * 6 - 3;
                        return (Day: start.AddDays(i), Open: open, Close: price,
                                Low: Math.Min(open, price) - rnd.NextDouble() * 2,
                                High: Math.Max(open, price) + rnd.NextDouble() * 2);
                    }).ToList();

                    EChart.Candlestick(days, d => d.Day,
                            d => d.Open, d => d.Close, d => d.Low, d => d.High)
                        .Title("Daily prices")
                        .Zoom()
                        .Show();
                    """
                },
                new()
                {
                    Id = "doc_snippet_echart_gauge_funnel",
                    Title = "Gauge And Funnel",
                    Description = "One number on a dial; stages of a funnel, widest first.",
                    Code = """
                    EChart.Gauge(72, max: 100, name: "CPU %").Title("Load").Show();

                    EChart.Funnel(new Dictionary<string, double>
                    {
                        ["Visited"] = 1000, ["Signed up"] = 420,
                        ["Tried it"] = 260, ["Bought"] = 90
                    }).Title("Conversion").Show();
                    """
                }
            }
        };
    }

    private DocArticle CreateEChartTreesAndFlowsArticle()
    {
        return new DocArticle
        {
            Id = "echart_trees_and_flows",
            Title = "EChart Trees, Flows And Networks",
            Subtitle = "Treemaps, sunbursts, Sankey diagrams and force-directed graphs.",
            ReadingTime = "3 min read",
            Summary = "A tree is a map of names to numbers (or to the maps inside them), or records with a name, a value and children. Flows and networks are (from, to, amount) links or an adjacency map.",
            Keywords = new List<string> { "echart", "treemap", "sunburst", "sankey", "graph", "network", "tree", "flow", "links" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Trees",
                    Content = "Treemap and Sunburst read the same tree: Dictionary<string, object> nested as deep as you like, with numbers at the leaves."
                },
                new()
                {
                    Heading = "Flows and networks",
                    Content = "Sankey draws (from, to, amount) flows, which can't go round in a circle. Graph takes links or an adjacency map; nodes with more links are drawn bigger, and they can be dragged.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "Graph(data, directed: true) draws an arrow on each link."
                }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "doc_snippet_echart_treemap",
                    Title = "Treemap And Sunburst Of One Tree",
                    Description = "The size of a project's folders, two ways.",
                    Code = """
                    var project = new Dictionary<string, object>
                    {
                        ["src"] = new Dictionary<string, object>
                        {
                            ["ui"] = 420, ["core"] = 610, ["io"] = 180
                        },
                        ["tests"] = 380,
                        ["docs"] = 120
                    };

                    EChart.Treemap(project).Title("Lines of code").Show();
                    EChart.Sunburst(project).Title("Lines of code").Show();
                    """
                },
                new()
                {
                    Id = "doc_snippet_echart_sankey",
                    Title = "Sankey Flows",
                    Description = "Where the visitors went.",
                    Code = """
                    var flows = new[]
                    {
                        ("Visit", "Cart", 40.0), ("Visit", "Left", 60.0),
                        ("Cart", "Bought", 12.0), ("Cart", "Left", 28.0)
                    };

                    EChart.Sankey(flows).Title("Checkout").Show();
                    """
                },
                new()
                {
                    Id = "doc_snippet_echart_graph",
                    Title = "A Network From An Adjacency Map",
                    Description = "Each service and the ones it calls.",
                    Code = """
                    var calls = new Dictionary<string, string[]>
                    {
                        ["web"] = ["auth", "api"],
                        ["api"] = ["db", "cache", "auth"],
                        ["auth"] = ["db"],
                        ["worker"] = ["db", "queue"]
                    };

                    EChart.Graph(calls, directed: true).Title("Who calls whom").Show();
                    """
                }
            }
        };
    }

    private DocArticle CreateEChart3DArticle()
    {
        return new DocArticle
        {
            Id = "echart_3d_charts",
            Title = "EChart 3D Charts (WebGL)",
            Subtitle = "Surfaces, parametric shapes, 3D bars and 3D points, z up.",
            ReadingTime = "4 min read",
            Summary = "echarts-gl draws 3D charts with WebGL. Drag to turn them, scroll to zoom, right-drag to move. A surface whose function is NaN somewhere has a hole there.",
            Keywords = new List<string> { "echart", "3d", "surface", "parametric", "torus", "bar3d", "scatter3d", "webgl", "echarts-gl", "colormap" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Surfaces",
                    Content = "Surface(f, xRange, yRange, resolution) samples z = f(x, y); Surface(grid) draws a grid of heights (a row per y). ParametricSurface draws shapes that aren't one height per place: spheres, tori, strips.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "ColorMap(ColorMapPreset.Plasma) recolours by height; Shading(SurfaceShading.Realistic) adds shine; Wireframe() draws the grid lines; AutoRotate() turns the view."
                },
                new()
                {
                    Heading = "Bars and points",
                    Content = "Bar3D takes a grid of heights or (x, y, height) items; text places are categories, kept in the order of xLabels and yLabels when given. Scatter3D takes (x, y, z) points."
                }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "doc_snippet_echart_sinc_3d",
                    Title = "A Ripple Surface",
                    Description = "z = sin(πr) / πr, coloured by height.",
                    Code = """
                    EChart.Surface((x, y) =>
                        {
                            var r = Math.Sqrt(x * x + y * y);
                            return r == 0 ? 1 : Math.Sin(r * Math.PI) / (r * Math.PI);
                        },
                        xRange: (-4, 4), yRange: (-4, 4), resolution: 80)
                        .Title("Ripple")
                        .ColorMap(ColorMapPreset.Plasma)
                        .Show();
                    """
                },
                new()
                {
                    Id = "doc_snippet_echart_parametric_torus",
                    Title = "A Torus",
                    Description = "x, y and z as functions of two angles.",
                    Code = """
                    EChart.ParametricSurface(
                            x: (u, v) => (2 + Math.Cos(v)) * Math.Cos(u),
                            y: (u, v) => (2 + Math.Cos(v)) * Math.Sin(u),
                            z: (u, v) => Math.Sin(v),
                            u: (0, 2 * Math.PI), v: (0, 2 * Math.PI))
                        .Title("Torus")
                        .ColorMap(ColorMapPreset.Turbo)
                        .Shading(SurfaceShading.Realistic)
                        .AutoRotate()
                        .Show();
                    """
                },
                new()
                {
                    Id = "doc_snippet_echart_bar3d_punchcard",
                    Title = "A 3D Punch Card",
                    Description = "Commits by hour and day, the hours in their own order.",
                    Code = """
                    string[] hours = ["12a", "3a", "6a", "9a", "12p", "3p", "6p", "9p"];
                    string[] days = ["Mon", "Tue", "Wed", "Thu", "Fri"];
                    var rnd = new Random(1);
                    var commits =
                        from h in hours
                        from d in days
                        select (Hour: h, Day: d, Count: rnd.Next(0, 12));

                    EChart.Bar3D(commits.ToList(),
                            c => c.Hour, c => c.Day, c => c.Count,
                            xLabels: hours, yLabels: days)
                        .Title("Commits")
                        .ZLabel("commits")
                        .Show();
                    """
                },
                new()
                {
                    Id = "doc_snippet_echart_bar3d_matrix",
                    Title = "3D Bars From A Grid",
                    Description = "A row per department, a column per quarter.",
                    Code = """
                    var scores = new double[,]
                    {
                        { 92, 85, 78, 96 },
                        { 88, 94, 91, 89 },
                        { 75, 80, 85, 92 }
                    };

                    EChart.Bar3D(scores,
                            xLabels: ["Q1", "Q2", "Q3", "Q4"],
                            yLabels: ["Eng", "Sales", "Support"])
                        .Title("Scores")
                        .ColorMap(ColorMapPreset.Viridis)
                        .Show();
                    """
                },
                new()
                {
                    Id = "doc_snippet_echart_scatter3d",
                    Title = "Points In 3D",
                    Description = "Two named clouds of points, a series each.",
                    Code = """
                    var rnd = new Random(2);
                    (double, double, double) Near(double c) =>
                        (c + rnd.NextDouble(), c + rnd.NextDouble(), c + rnd.NextDouble());

                    var clusters = new Dictionary<string, List<(double, double, double)>>
                    {
                        ["A"] = Enumerable.Range(0, 80).Select(_ => Near(0)).ToList(),
                        ["B"] = Enumerable.Range(0, 80).Select(_ => Near(2)).ToList()
                    };

                    EChart.Scatter3D(clusters).Title("Two clusters").Show();
                    """
                }
            }
        };
    }

    private DocArticle CreateEChartFullOptionArticle()
    {
        return new DocArticle
        {
            Id = "echart_raw_schema_control",
            Title = "The Whole ECharts Option",
            Subtitle = "Paste an option from the ECharts examples, reach any part of it, and put JavaScript functions in it.",
            ReadingTime = "4 min read",
            Summary = "Every setting writes into chart.Option, the ECharts option itself (echarts.apache.org/en/option.html). Set, Merge and Configure reach what no setting does; FromJson and FromJs start from an option you already have.",
            Keywords = new List<string> { "echart", "option", "json", "fromjson", "fromjs", "set", "merge", "configure", "jsfunc", "formatter", "callback" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Reaching any part",
                    Content = "Set(\"series[0].label.show\", true) sets one value by its path; a path that names a list without an index sets every item (\"series.label.show\"). Merge(json) merges another option in. Configure(o => ...) changes the JsonObject in code."
                },
                new()
                {
                    Heading = "Starting from an option",
                    Content = "EChart.FromJson(json) takes JSON and says where it's wrong. The ECharts examples are JavaScript (unquoted names, 'single quotes'): EChart.FromJs(code) takes them as they are, either an object or code that sets option. Settings apply on top of both.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "JsFunc.From(\"p => ...\") is a JavaScript function in the option, for formatters and callbacks: it is written into the page as code, not text."
                }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "doc_snippet_echart_raw_json",
                    Title = "An Option As JSON, Then Settings",
                    Description = "A rounded donut from raw JSON, with a title set on top.",
                    Code = """"
                    EChart.FromJson("""
                        {
                          "tooltip": { "trigger": "item" },
                          "series": [{
                            "type": "pie",
                            "radius": ["40%", "70%"],
                            "itemStyle": { "borderRadius": 8 },
                            "data": [
                              { "value": 1048, "name": "Search" },
                              { "value": 735, "name": "Direct" },
                              { "value": 580, "name": "Email" }
                            ]
                          }]
                        }
                        """)
                        .Title("Where visitors came from")
                        .Legend()
                        .Show();
                    """"
                },
                new()
                {
                    Id = "doc_snippet_echart_from_js",
                    Title = "An Example Pasted As It Is",
                    Description = "JavaScript from the ECharts examples, unchanged.",
                    Code = """"
                    EChart.FromJs("""
                        option = {
                          xAxis: { type: 'category', data: ['Mon', 'Tue', 'Wed', 'Thu'] },
                          yAxis: { type: 'value' },
                          series: [{ data: [150, 230, 224, 218], type: 'line', smooth: true }]
                        };
                        """)
                        .Title("Pasted from the examples")
                        .Show();
                    """"
                },
                new()
                {
                    Id = "doc_snippet_echart_jsfunc",
                    Title = "Paths And A Formatter Function",
                    Description = "Values written on the bars, a rotated axis, and a tooltip written by a function.",
                    Code = """"
                    var temps = new Dictionary<string, double>
                    {
                        ["Jan"] = 3.1, ["Feb"] = 4.2, ["Mar"] = 7.9, ["Apr"] = 11.6
                    };

                    EChart.Bar(temps, "°C")
                        .Set("series.label.show", true)
                        .Set("xAxis.axisLabel.rotate", 30)
                        .Set("tooltip.formatter",
                            JsFunc.From("p => p[0].name + ': ' + p[0].value.toFixed(1) + ' °C'"))
                        .Merge("""{ "animationDuration": 300 }""")
                        .Title("Temperature")
                        .Show();
                    """"
                }
            }
        };
    }
}
