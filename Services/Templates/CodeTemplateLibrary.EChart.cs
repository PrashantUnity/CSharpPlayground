using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Templates;

public static partial class CodeTemplateLibrary
{
    private static IEnumerable<CodeTemplate> GetEChartTemplates() => new List<CodeTemplate>
    {
        new()
        {
            Id = "echart_3d_surface_webgl",
            Title = "EChart 3D Surface (WebGL)",
            Category = "Data & Visuals",
            Kind = WorkspaceItemKind.Script,
            Description = "A 3D surface drawn by Apache ECharts and echarts-gl, coloured by height, that you can turn and zoom.",
            IconKind = MaterialIconKind.ChartLine,
            AccentColor = "#38BDF8",
            AccentBackground = "#0C4A6E",
            AccentBorder = "#0284C7",
            CategoryBadge = "EChart • 3D Surface",
            Tags = new List<string> { "EChart", "3D", "Surface", "WebGL", "Math" },
            Notes = """
            # EChart 3D Surface
            A surface z = f(x, y) drawn with WebGL.

            ### In the chart
            - **Drag**: turn it.
            - **Scroll**: zoom.
            - **Right-drag**: move it.
            """,
            InitialCode = """
            // z = sin(πr) / πr, sampled 80 times along each axis
            EChart.Surface((x, y) =>
                {
                    var r = Math.Sqrt(x * x + y * y);
                    return r == 0 ? 1 : Math.Sin(r * Math.PI) / (r * Math.PI);
                },
                xRange: (-4, 4), yRange: (-4, 4), resolution: 80)
                .Title("Ripple")
                .ColorMap(ColorMapPreset.Viridis)
                .Shading(SurfaceShading.Realistic)
                .AutoRotate()
                .Show();
            """
        },
        new()
        {
            Id = "echart_interactive_dashboard",
            Title = "EChart Interactive Dashboard",
            Category = "Data & Visuals",
            Kind = WorkspaceItemKind.Script,
            Description = "Revenue as bars and profit as a line on the same quarters, with zoom, a legend and the toolbox.",
            IconKind = MaterialIconKind.ViewDashboardOutline,
            AccentColor = "#A855F7",
            AccentBackground = "#581C87",
            AccentBorder = "#7E22CE",
            CategoryBadge = "EChart • Dashboard",
            Tags = new List<string> { "EChart", "Dashboard", "Bar", "Line", "Zoom" },
            Notes = """
            # EChart Dashboard
            Two series of different kinds on the same axes, with zoom and the save and data-view buttons.
            """,
            InitialCode = """
            var revenue = new Dictionary<string, double>
            {
                ["Q1"] = 120_000, ["Q2"] = 155_000, ["Q3"] = 190_000, ["Q4"] = 245_000
            };
            var profit = new Dictionary<string, double>
            {
                ["Q1"] = 45_000, ["Q2"] = 62_000, ["Q3"] = 78_000, ["Q4"] = 105_000
            };

            EChart.Bar(revenue, "Revenue ($)")
                .Series("Profit ($)", profit, EChartType.Line)
                .Title("Fiscal metrics")
                .Subtitle("Revenue and net profit")
                .Smooth()
                .Zoom()
                .Toolbox()
                .Show();
            """
        }
    };
}
