using System.Collections.Generic;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static partial class CodeTemplateLibrary
{
    private static IEnumerable<CodeTemplate> GetPolyglotVisualsTemplates() => new List<CodeTemplate>
    {
        new()
        {
            Id = "visuals_python_starter",
            Title = "Python Interactive Visuals",
            Category = "Visuals & Data",
            Kind = WorkspaceItemKind.Script,
            LanguageId = "python",
            Description = "Generate 2D line charts, 3D parametric surfaces, and step-by-step array visualizers in Python.",
            IconKind = MaterialIconKind.LanguagePython,
            AccentColor = "#38BDF8",
            AccentBackground = "#0C4A6E",
            AccentBorder = "#0284C7",
            CategoryBadge = "Python • Visuals",
            Tags = new List<string> { "Python", "Charts", "Visuals", "3D" },
            Notes = "# Python Visuals\nDemonstrates generating interactive 2D charts and 3D plots via `fry_display`.",
            InitialCode = @"import math
from fry_display import Display

# 1. 2D Sine Wave Chart
x = [i * 0.1 for i in range(60)]
y = [math.sin(v) for v in x]
Display.chart(x=x, y=y, title=""Sine Wave Analysis"", chart_type=""line"")

# 2. 3D Surface
def saddle(x, y):
    return x**2 - y**2

Display.plot3d_surface(saddle, x_range=(-4, 4), y_range=(-4, 4), res=30, colormap=""plasma"")
"
        },
        new()
        {
            Id = "visuals_javascript_starter",
            Title = "JavaScript Visuals & Charts",
            Category = "Visuals & Data",
            Kind = WorkspaceItemKind.Script,
            LanguageId = "javascript",
            Description = "Render interactive 2D charts and data-structure grids using Node.js and fry_display.",
            IconKind = MaterialIconKind.LanguageJavascript,
            AccentColor = "#FACC15",
            AccentBackground = "#713F12",
            AccentBorder = "#EAB308",
            CategoryBadge = "JS • Visuals",
            Tags = new List<string> { "JavaScript", "Charts", "NodeJS" },
            Notes = "# JavaScript Visuals\nInteractive bar charts and grid visualizers in Node.js.",
            InitialCode = @"const { Display } = require('fry_display');

// Render a bar chart
Display.chart({
    type: 'bar',
    title: 'Monthly Performance',
    labels: ['Jan', 'Feb', 'Mar', 'Apr', 'May'],
    series: [{ name: 'Velocity', values: [34, 45, 52, 60, 58] }]
});
"
        },
        new()
        {
            Id = "visuals_java_starter",
            Title = "Java Algorithm Visualizer",
            Category = "Visuals & Data",
            Kind = WorkspaceItemKind.Script,
            LanguageId = "java",
            Description = "Visualize matrix traversals and island search algorithms step-by-step in Java.",
            IconKind = MaterialIconKind.LanguageJava,
            AccentColor = "#FB923C",
            AccentBackground = "#7C2D12",
            AccentBorder = "#EA580C",
            CategoryBadge = "Java • Visuals",
            Tags = new List<string> { "Java", "Visualizer", "Matrix" },
            Notes = "# Java Visualizer\nStep-by-step matrix visualization in Java.",
            InitialCode = @"import com.frypdf.display.Visualizer;

public class Main {
    public static void main(String[] args) {
        int[][] grid = {
            {1, 1, 0, 0},
            {1, 1, 0, 1},
            {0, 0, 1, 1}
        };
        Visualizer.grid(grid).title(""Island Map"").show();
    }
}
"
        },
        new()
        {
            Id = "visuals_go_starter",
            Title = "Go 2D & 3D Charts",
            Category = "Visuals & Data",
            Kind = WorkspaceItemKind.Script,
            LanguageId = "go",
            Description = "High-performance data charting and 3D scatter plots in Go.",
            IconKind = MaterialIconKind.LanguageGo,
            AccentColor = "#00ADD8",
            AccentBackground = "#083344",
            AccentBorder = "#0891B2",
            CategoryBadge = "Go • Visuals",
            Tags = new List<string> { "Go", "Charts", "3D" },
            Notes = "# Go Visuals\nEmit visual MIME bundles from Go applications.",
            InitialCode = @"package main

import (
    ""math""
    ""fry""
)

func main() {
    var values []float64
    for i := 0.0; i < 50; i++ {
        values = append(values, math.Cos(i*0.1))
    }
    fry.Chart(values, fry.Title(""Cosine Wave""))
}
"
        },
        new()
        {
            Id = "visuals_rust_starter",
            Title = "Rust Data Visualizer",
            Category = "Visuals & Data",
            Kind = WorkspaceItemKind.Script,
            LanguageId = "rust",
            Description = "Zero-overhead data plotting and algorithm visualization in Rust.",
            IconKind = MaterialIconKind.LanguageRust,
            AccentColor = "#F97316",
            AccentBackground = "#431407",
            AccentBorder = "#C2410C",
            CategoryBadge = "Rust • Visuals",
            Tags = new List<string> { "Rust", "Visuals", "Charts" },
            Notes = "# Rust Visuals\nEmit canonical MIME visual specs directly from Rust.",
            InitialCode = @"use fry::prelude::*;

fn main() {
    let telemetry = vec![(0.0, 10.0), (1.0, 15.0), (2.0, 12.0), (3.0, 22.0)];
    line_chart(&telemetry)
        .title(""Telemetry Data"")
        .show();
}
"
        },
        new()
        {
            Id = "visuals_cpp_starter",
            Title = "C++ 3D Surface Visualizer",
            Category = "Visuals & Data",
            Kind = WorkspaceItemKind.Script,
            LanguageId = "cpp",
            Description = "Interactive 3D plots and mathematical surfaces rendered from modern C++20.",
            IconKind = MaterialIconKind.LanguageCpp,
            AccentColor = "#60A5FA",
            AccentBackground = "#1E3A8A",
            AccentBorder = "#2563EB",
            CategoryBadge = "C++ • 3D",
            Tags = new List<string> { "C++", "3D", "Visuals" },
            Notes = "# C++ Visuals\nModern C++ header-only visual runtime integration.",
            InitialCode = @"#include <fry_display.hpp>
#include <cmath>

int main() {
    fry::Surface3D surface(""Paraboloid"", [](double x, double y) {
        return x * x + y * y;
    }, -5.0, 5.0, -5.0, 5.0, 30);
    surface.show();
    return 0;
}
"
        },
        new()
        {
            Id = "visuals_fsharp_starter",
            Title = "F# Exploratory Data Plot",
            Category = "Visuals & Data",
            Kind = WorkspaceItemKind.Script,
            LanguageId = "fsharp",
            Description = "Functional pipeline data analysis and interactive charting in F#.",
            IconKind = MaterialIconKind.CodeBraces,
            AccentColor = "#38BDF8",
            AccentBackground = "#0F172A",
            AccentBorder = "#0284C7",
            CategoryBadge = "F# • Visuals",
            Tags = new List<string> { "F#", "Charts", "Functional" },
            Notes = "# F# Visuals\nPipeline data charting in F#.",
            InitialCode = @"open Fry

let points = [ for x in 0.0 .. 0.2 .. 10.0 -> (x, sin x) ]
Display.LineChart points
"
        }
    };
}
