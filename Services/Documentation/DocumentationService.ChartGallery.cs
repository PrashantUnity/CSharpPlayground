using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Documentation;

public partial class DocumentationService
{
    private DocCategory BuildChartGalleryCategory() => new()
    {
        Id = "chart_gallery",
        Title = "Chart Gallery",
        IconKind = MaterialIconKind.ChartMultiple,
        AccentColor = "#4EC9B0",
        Badge = "Charts",
        Description = "Line, bar, round and scaled charts: a runnable sample for each of the kinds the Chart.js samples show.",
        Articles = new List<DocArticle>
        {
            CreateGalleryLinesArticle(),
            CreateGalleryBarsArticle(),
            CreateGalleryRoundArticle(),
            CreateGalleryScalesArticle()
        }
    };

    private static DocCodeSnippet Sample(string id, string title, string description, string code) => new()
    {
        Id = id,
        Title = title,
        Description = description,
        Language = "csharp",
        TargetKind = WorkspaceItemKind.Script,
        Code = code
    };

    private DocArticle CreateGalleryLinesArticle() => new()
    {
        Id = "gallery_lines",
        Title = "Gallery: Lines & Areas",
        Subtitle = "Several lines in one chart, two value axes, curves, steps, dashes, markers, fills.",
        ReadingTime = "5 min read",
        Summary = "Charts.Line((\"A\", a), (\"B\", b)) draws a line for each name. A series' style says how it is drawn: Dashed, Smooth, Step, OnRightAxis, Fill, Points. Press Run on a sample to see it here.",
        Keywords = new List<string> { "line", "multi", "series", "axis", "interpolation", "smooth", "stepped", "dashed", "point style", "segment", "area", "stacked", "fill", "chart.js" },
        Sections = new List<DocSection>
        {
            new()
            {
                Heading = "More than one line",
                Content = "Give Charts.Line (or Display.LineChart) one (name, values) pair for each line, or add lines to a builder with .Series(name, values). Each series takes the next colour of the palette and gets a legend entry that hides or shows it when clicked.",
                CalloutType = DocCalloutType.Tip,
                CalloutText = "In the chart, hover to see every line's value at that place, scroll with Ctrl to zoom, and click a legend entry to hide a line."
            },
            new()
            {
                Heading = "How a line is drawn",
                Content = "Each series can be drawn differently from the others.",
                BulletPoints = new List<string>
                {
                    "Dashed(), Dotted(), LineWidth(3), Color(\"#f43f5e\")",
                    "Smooth(0.4), Monotone(), Step(LineStep.After | Before | Middle)",
                    "Points(PointShape.Star, 6): circle, triangle, square, diamond, cross, star",
                    "Fill(), FillTo(seriesIndex): an area under a line, or between two lines",
                    "OnRightAxis(): measured on the second value axis"
                }
            }
        },
        CodeSnippets = new List<DocCodeSnippet>
        {
            Sample("gallery_line", "Line chart", "Two lines with names under the x axis.", """
                var months = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun" };

                Charts.Line(
                        ("Sales", new double[] { 12, 19, 3, 5, 2, 3 }),
                        ("Costs", new double[] { 8, 11, 7, 9, 6, 5 }))
                    .Labels(months)
                    .Title("Line chart")
                    .YLabel("USD (k)")
                    .Show();
                """),
            Sample("gallery_line_multi_axis", "Multi axis line chart", "Two lines measured on different axes: °C on the left, mm on the right.", """
                var months = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun" };

                Charts.Line(("Temperature", new double[] { 12, 15, 21, 25, 22, 16 }))
                    .Series("Rainfall", new double[] { 90, 60, 30, 10, 25, 70 }, s => s.OnRightAxis().Color("#60a5fa"))
                    .Labels(months)
                    .Title("Multi-axis line chart")
                    .YLabel("°C")
                    .RightAxis("mm")
                    .Show();
                """),
            Sample("gallery_line_interpolation", "Interpolation modes", "The same zig-zag drawn straight, as a smooth curve and as a monotone curve.", """
                var zigzag = new double[] { 0, 10, 3, 9, 2, 10, 4 };
                double[] Up(double by) => zigzag.Select(v => v + by).ToArray();

                Charts.Line(("linear", zigzag))
                    .Series("smooth", Up(1), s => s.Smooth(0.4))
                    .Series("monotone", Up(2), s => s.Monotone())
                    .Title("Interpolation modes")
                    .Show();
                """),
            Sample("gallery_line_styling", "Line styling", "Solid, dashed and dotted lines of different widths.", """
                var steps = Enumerable.Range(0, 7).ToArray();
                double[] Wave(double shift) => steps.Select(i => 5 + shift + 3 * Math.Sin(i)).ToArray();

                Charts.Line(("solid", Wave(0)))
                    .Series("dashed", Wave(4), s => s.Dashed().LineWidth(3))
                    .Series("dotted", Wave(8), s => s.Dotted().LineWidth(4).Color("#f59e0b"))
                    .Title("Line styling")
                    .Show();
                """),
            Sample("gallery_line_points", "Point styling", "Every marker shape, one series each.", """
                var shapes = Enum.GetValues<PointShape>();
                var chart = Charts.Line(("circle", new double[] { 1, 2, 3, 2 })).Style(s => s.Points(PointShape.Circle, 6));

                for (var i = 1; i < shapes.Length; i++)
                {
                    var rise = i * 2;
                    chart.Series(shapes[i].ToString().ToLower(), new double[] { 1 + rise, 2 + rise, 3 + rise, 2 + rise },
                        s => s.Points(shapes[i], 6));
                }

                chart.Title("Point styling").Show();
                """),
            Sample("gallery_line_segments", "Line segment styling", "Each segment is coloured by whether the line went up or down to reach it.", """
                var values = new double[] { 3, 5, 2, 6, 4, 8, 5, 9 };
                var colours = values
                    .Select((v, i) => i == 0 ? null : v >= values[i - 1] ? "#10b981" : "#f43f5e")
                    .ToArray();

                Charts.Line(values, "Index")
                    .Style(s => s.ColorSegments().Colors(colours))
                    .Title("Line segment styling")
                    .Show();
                """),
            Sample("gallery_line_stepped", "Stepped line charts", "Where the line changes level: before, after or in the middle of a step.", """
                var data = new double[] { 3, 6, 4, 8, 5, 9, 7, 10 };
                double[] Down(double by) => data.Select(v => v - by).ToArray();

                Charts.Line(("before", data)).Style(s => s.Step(LineStep.Before))
                    .Series("after", Down(2), s => s.Step(LineStep.After).Dashed())
                    .Series("middle", Down(4), s => s.Step(LineStep.Middle).Dotted())
                    .Title("Stepped lines")
                    .Show();
                """),
            Sample("gallery_area_fill", "Area chart: line datasets", "Two areas under their lines.", """
                var months = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun" };

                Charts.Area(
                        ("Visitors", new double[] { 40, 55, 48, 70, 66, 90 }),
                        ("Signups", new double[] { 10, 18, 14, 25, 22, 38 }))
                    .Labels(months)
                    .Title("Area chart")
                    .Show();
                """),
            Sample("gallery_area_boundaries", "Area chart: fill between lines", "The space between a line and another is filled, not the space under it.", """
                Charts.Line(("Upper", new double[] { 8, 10, 9, 12, 11, 14 }))
                    .Series("Lower", new double[] { 3, 4, 5, 4, 6, 5 }, s => s.FillTo(0).Color("#4ec9b0"))
                    .Title("Fill between two lines")
                    .Show();
                """),
            Sample("gallery_area_stacked", "Area chart: stacked", "Each area starts where the one under it ends.", """
                Charts.Area(
                        ("North", new double[] { 4, 6, 5, 8, 7 }),
                        ("South", new double[] { 3, 2, 6, 4, 5 }),
                        ("East", new double[] { 2, 3, 1, 5, 4 }))
                    .Stacked()
                    .Title("Stacked areas")
                    .Show();
                """)
        }
    };

    private DocArticle CreateGalleryBarsArticle() => new()
    {
        Id = "gallery_bars",
        Title = "Gallery: Bars",
        Subtitle = "Vertical, horizontal, floating, stacked, grouped and rounded bars, and bars mixed with lines.",
        ReadingTime = "4 min read",
        Summary = "Charts.Bar draws one bar for each value; several series sit side by side, or pile up with .Stacked(). Floating(from) starts a bar above the baseline, Rounded() rounds its corners, Horizontal() lays the chart on its side.",
        Keywords = new List<string> { "bar", "horizontal", "stacked", "floating", "rounded", "border radius", "combo", "grouped", "percent", "chart.js" },
        Sections = new List<DocSection>
        {
            new()
            {
                Heading = "Bars, side by side or piled up",
                Content = "Several bar series are drawn side by side in each slot. Stacked() piles them up (Stacked(percent: true) scales every slot to 100%), and a series' StackGroup(\"name\") makes separate piles beside each other. A series of another kind (Kind(ChartType.Line)) is drawn over the bars, on the same axis or the right one."
            }
        },
        CodeSnippets = new List<DocCodeSnippet>
        {
            Sample("gallery_bar_vertical", "Vertical bar chart", "A label → number map is one bar each.", """
                Display.BarChart(
                    new Dictionary<string, double> { ["Red"] = 12, ["Blue"] = 19, ["Yellow"] = 3, ["Green"] = 5, ["Purple"] = 2 },
                    "Vertical bar chart", yLabel: "votes");
                """),
            Sample("gallery_bar_horizontal", "Horizontal bar chart", "Categories run down the left side.", """
                Charts.Bar(("2025", new double[] { 12, 19, 3, 5, 2 }), ("2026", new double[] { 14, 12, 6, 9, 4 }))
                    .Labels(new[] { "North", "South", "East", "West", "Central" })
                    .Horizontal()
                    .Title("Horizontal bar chart")
                    .XLabel("Region")
                    .YLabel("Sales")
                    .Show();
                """),
            Sample("gallery_bar_floating", "Floating bars", "Each bar runs from one value to another: here a month's lowest to highest temperature.", """
                var lows = new double[] { -5, -3, 2, 6, 11, 14 };
                var highs = new double[] { 3, 6, 12, 17, 22, 26 };

                Charts.Bar(highs, "Temperature range")
                    .Style(s => s.Floating(lows).Rounded(6))
                    .Labels(new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun" })
                    .Title("Floating bars")
                    .YLabel("°C")
                    .Show();
                """),
            Sample("gallery_bar_stacked", "Stacked bar chart", "Three series piled in each slot.", """
                Charts.Bar(
                        ("North", new double[] { 4, 6, 5, 8 }),
                        ("South", new double[] { 3, 2, 6, 4 }),
                        ("East", new double[] { 2, 3, 1, 5 }))
                    .Labels(new[] { "Q1", "Q2", "Q3", "Q4" })
                    .Stacked()
                    .Title("Stacked bar chart")
                    .Show();
                """),
            Sample("gallery_bar_stacked_groups", "Stacked bar chart with groups", "Two piles in each slot: 2025 beside 2026.", """
                Charts.Bar(("2025 online", new double[] { 4, 6, 5, 8 }), ("2025 shops", new double[] { 3, 2, 6, 4 }))
                    .Style(s => s.StackGroup("2025"))
                    .Series("2026 online", new double[] { 5, 7, 6, 9 }, s => s.StackGroup("2026"))
                    .Series("2026 shops", new double[] { 2, 3, 3, 4 }, s => s.StackGroup("2026"))
                    .Labels(new[] { "Q1", "Q2", "Q3", "Q4" })
                    .Stacked()
                    .Title("Stacked bars in groups")
                    .Show();
                """),
            Sample("gallery_bar_percent", "Percent stacked bars", "Every slot is scaled to 100%.", """
                Charts.Bar(
                        ("Chrome", new double[] { 52, 55, 60, 62 }),
                        ("Safari", new double[] { 30, 28, 24, 21 }),
                        ("Other", new double[] { 18, 17, 16, 17 }))
                    .Labels(new[] { "2022", "2023", "2024", "2025" })
                    .Stacked(percent: true)
                    .Title("Browser share")
                    .Show();
                """),
            Sample("gallery_bar_radius", "Bar chart border radius", "Rounded outer corners.", """
                Charts.Bar(new double[] { 12, 19, 3, 5, 2, 8 }, "Orders")
                    .Style(s => s.Rounded(12))
                    .Labels(new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" })
                    .Title("Bar chart border radius")
                    .Show();
                """),
            Sample("gallery_combo_bar_line", "Combo bar/line chart", "Bars for revenue, a line for margin on the right axis.", """
                Charts.Bar(("Revenue", new double[] { 40, 55, 48, 70, 66, 90 }))
                    .Series("Costs", new double[] { 30, 35, 38, 44, 50, 52 })
                    .Series("Margin %", new double[] { 25, 36, 21, 37, 24, 42 }, s => s.Kind(ChartType.Line).OnRightAxis().Points(PointShape.Diamond, 5))
                    .Labels(new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun" })
                    .RightAxis("Margin %", 0, 100)
                    .Title("Combo: bars and a line")
                    .YLabel("USD (k)")
                    .Show();
                """)
        }
    };

    private DocArticle CreateGalleryRoundArticle() => new()
    {
        Id = "gallery_round",
        Title = "Gallery: Pie, Donut, Polar, Radar, Bubble & Scatter",
        Subtitle = "Slices, rings, gauges, radar polygons, polar areas, bubbles and scatter plots.",
        ReadingTime = "4 min read",
        Summary = "Display.PieChart, DonutChart, PolarAreaChart, RadarChart, BubbleChart and ScatterChart (and the matching Charts.* builders) draw the round and point-based kinds. A donut with several series draws a ring for each; gauge: true draws a half circle.",
        Keywords = new List<string> { "pie", "donut", "doughnut", "polar", "radar", "bubble", "scatter", "gauge", "rings", "chart.js" },
        Sections = new List<DocSection>
        {
            new()
            {
                Heading = "Round charts",
                Content = "A pie or donut divides each series into slices by value. With several series each is a ring (the first outermost). Angles(start, sweep) turns and trims it, so Angles(-90, 180) is a half circle gauge. A polar area gives every slice the same angle and sets its reach by value; a radar draws a polygon for each series over a spoke for each value."
            },
            new()
            {
                Heading = "Bubbles and scatter",
                Content = "A bubble chart takes (x, y, size) items; the size is the radius in pixels. A scatter chart takes (x, y) pairs and can use any marker shape."
            }
        },
        CodeSnippets = new List<DocCodeSnippet>
        {
            Sample("gallery_doughnut", "Doughnut chart", "A donut: a pie with a hole.", """
                Display.DonutChart(
                    new Dictionary<string, double> { ["Chrome"] = 62, ["Safari"] = 21, ["Edge"] = 11, ["Other"] = 6 },
                    "Doughnut chart");
                """),
            Sample("gallery_pie", "Pie chart", "Each value's share of the whole.", """
                Display.PieChart(
                    new Dictionary<string, double> { ["Red"] = 300, ["Blue"] = 50, ["Yellow"] = 100 },
                    "Pie chart");
                """),
            Sample("gallery_multi_pie", "Multi series pie", "A ring for each series: this year's inside last year's.", """
                Display.DonutChart(
                    new Dictionary<string, double[]>
                    {
                        ["2025"] = new double[] { 62, 21, 11, 6 },
                        ["2026"] = new double[] { 55, 28, 12, 5 }
                    },
                    "Browser share, two years", labels: new[] { "Chrome", "Safari", "Edge", "Other" });
                """),
            Sample("gallery_gauge", "Half circle gauge", "A donut over 180 degrees, as a gauge showing 65 out of 100.", """
                Charts.Gauge(65, 100, "Disk used").Show();

                Display.DonutChart(
                    new Dictionary<string, double> { ["Done"] = 40, ["Doing"] = 25, ["To do"] = 35 },
                    "Progress", gauge: true);
                """),
            Sample("gallery_polar", "Polar area chart", "Equal slices that reach as far as their value.", """
                Display.PolarAreaChart(
                    new Dictionary<string, double> { ["Red"] = 11, ["Blue"] = 16, ["Yellow"] = 7, ["Green"] = 3, ["Purple"] = 14, ["Orange"] = 9 },
                    "Polar area chart");
                """),
            Sample("gallery_radar", "Radar chart", "Two polygons over six spokes.", """
                Display.RadarChart(
                    new Dictionary<string, double[]>
                    {
                        ["Ada"] = new double[] { 8, 6, 9, 5, 7, 4 },
                        ["Bo"] = new double[] { 5, 9, 6, 8, 4, 7 }
                    },
                    "Radar chart", labels: new[] { "Speed", "Power", "Skill", "Stamina", "Focus", "Luck" });
                """),
            Sample("gallery_radar_gaps", "Radar chart with a missing value", "A value that is NaN is not drawn, and breaks the outline.", """
                Charts.Radar(new Dictionary<string, double[]>
                    {
                        ["Measured"] = new double[] { 8, 6, double.NaN, 5, 7, 4 }
                    },
                    new[] { "Speed", "Power", "Skill", "Stamina", "Focus", "Luck" })
                    .Style(s => s.Fill(false))
                    .Title("Radar with a gap")
                    .Show();
                """),
            Sample("gallery_bubble", "Bubble chart", "Position says x and y, the circle's size says a third value.", """
                var random = new Random(4);
                (double X, double Y, double Size)[] Cloud(int count) =>
                    Enumerable.Range(0, count).Select(_ => (random.NextDouble() * 10, random.NextDouble() * 10, 4 + random.NextDouble() * 18)).ToArray();

                Display.BubbleChart(new Dictionary<string, (double, double, double)[]> { ["Group A"] = Cloud(8), ["Group B"] = Cloud(8) },
                    "Bubble chart", xLabel: "x", yLabel: "y");
                """),
            Sample("gallery_scatter", "Scatter chart", "Points with their own marker shapes.", """
                var random = new Random(2);
                (double, double)[] Points(int count, double shift) =>
                    Enumerable.Range(0, count).Select(i => ((double)i, shift + i * 0.5 + random.NextDouble() * 4)).ToArray();

                Charts.Scatter(Points(14, 0), "circles")
                    .Series("triangles", Points(14, 6), s => s.Points(PointShape.Triangle, 6))
                    .Series("stars", Points(14, 12), s => s.Points(PointShape.Star, 6))
                    .Title("Scatter chart")
                    .Show();
                """)
        }
    };

    private DocArticle CreateGalleryScalesArticle() => new()
    {
        Id = "gallery_scales",
        Title = "Gallery: Scales & Axes",
        Subtitle = "Logarithmic and time axes, suggested ranges, reversed axes and a second axis.",
        ReadingTime = "3 min read",
        Summary = "LogY(), TimeX(), SuggestedY(min, max), ReverseY() and RightAxis() change how values map to positions. A log axis needs values above 0; on a time axis x is Unix milliseconds.",
        Keywords = new List<string> { "log", "logarithmic", "time", "date", "scale", "axis", "suggested", "reverse", "multi axis", "chart.js" },
        Sections = new List<DocSection>
        {
            new()
            {
                Heading = "Axes",
                Content = "An axis is linear unless the chart says otherwise. LogY() puts a power of ten at each step; TimeX() reads each series' x as Unix milliseconds and puts ticks on calendar units (seconds to years). SuggestedY(min, max) makes sure the axis reaches at least that far without fixing it; YRange(min, max) fixes it.",
                CalloutType = DocCalloutType.Tip,
                CalloutText = "A value that a log axis can't show (0 or less) is an error that names it: use NaN for a missing value."
            }
        },
        CodeSnippets = new List<DocCodeSnippet>
        {
            Sample("gallery_scale_log", "Logarithmic scale", "Growth by a factor of ten each step is a straight line.", """
                Charts.Line(new double[] { 1, 10, 100, 1_000, 10_000, 100_000 }, "Growth")
                    .LogY()
                    .Title("Logarithmic scale")
                    .YLabel("value")
                    .Show();
                """),
            Sample("gallery_scale_time", "Time scale", "x values are dates; the axis puts its ticks on months.", """
                var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
                var days = Enumerable.Range(0, 120).Where(d => d % 4 == 0).ToArray();
                var x = days.Select(d => (double)start.AddDays(d).ToUnixTimeMilliseconds()).ToArray();
                var y = days.Select(d => 20 + 8 * Math.Sin(d / 15.0) + d / 10.0).ToArray();

                Charts.Scatter(x.Zip(y, (a, b) => (a, b)).ToArray(), "Temperature")
                    .As(ChartType.Line)
                    .TimeX()
                    .Title("Time scale")
                    .XLabel("Date")
                    .Show();
                """),
            Sample("gallery_scale_suggested", "Suggested minimum and maximum", "The data stays in 40 to 60, the axis reaches at least 0 to 100.", """
                Charts.Line(new double[] { 42, 55, 48, 60, 51 }, "Score")
                    .SuggestedY(0, 100)
                    .Title("Suggested range")
                    .Show();
                """),
            Sample("gallery_scale_reverse", "Reversed axes", "The value axis runs downwards, as for ranks or depths.", """
                Charts.Line(new double[] { 5, 3, 4, 1, 2 }, "Rank")
                    .Labels(new[] { "Mon", "Tue", "Wed", "Thu", "Fri" })
                    .ReverseY()
                    .Title("Rank over the week (1 is best)")
                    .Show();
                """),
            Sample("gallery_scatter_multi_axis", "Scatter with two value axes", "Two scatter series on different axes.", """
                var x = Enumerable.Range(1, 12).Select(i => (double)i).ToArray();
                (double, double)[] Pairs(Func<double, double> f) => x.Select(v => (v, f(v))).ToArray();

                Charts.Scatter(Pairs(v => v * 2 + 3), "Small numbers")
                    .Series("Big numbers", Pairs(v => v * 400 + Math.Sin(v) * 300), s => s.OnRightAxis().Points(PointShape.Diamond, 5))
                    .RightAxis("big")
                    .Title("Scatter with two axes")
                    .Show();
                """)
        }
    };
}
