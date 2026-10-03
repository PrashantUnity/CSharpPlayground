using System;
using System.Threading.Tasks;
using FrySharp.Sdk;

namespace DartSupportExtension;

/// <summary>
/// Dynamic entry point for the Dart Language Support extension.
/// Registers the full-featured DartLanguage definition with FrySharp's language engine.
/// </summary>
public class DartExtensionEntryPoint : IExtensionEntryPoint
{
    public Task InitializeAsync(IExtensionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var dartLanguage = new DartLanguage(context);
        try { context.App?.Languages?.Unregister(dartLanguage.Id); } catch { }
        // Automatically tracked by the extension lifetime bag and cleanly unregistered on unload/reload
        if (context.App?.Languages != null)
        {
            var reg = context.App.Languages.Register(dartLanguage);
            if (reg != null)
            {
                context.TrackDisposable(reg);
            }
        }

        // Register rich Dart documentation category in the Documentation & Learning Center
        var docCategory = new PdfEditorApp.Plugins.CSharpEditor.Models.DocCategory
        {
            Id = "dart_guide",
            Title = "Dart 3 Guide & Visual SDK",
            IconKind = Material.Icons.MaterialIconKind.CodeBraces,
            AccentColor = "#0175C2",
            Badge = "Dart 3",
            Description = "Guide to Dart 3 document automation, interactive polyglot notebooks, 2D charts, 3D surface plots, and algorithm visualizers.",
            Articles = new System.Collections.Generic.List<PdfEditorApp.Plugins.CSharpEditor.Models.DocArticle>
            {
                new()
                {
                    Id = "dart_getting_started",
                    Title = "Dart 3 Interactive Scripting",
                    Subtitle = "Write, execute, and visualize Dart code in scripts and polyglot notebooks.",
                    ReadingTime = "3 min read",
                    Summary = "FrySharp provides first-class Dart 3 support including live execution, interactive polyglot notebook cells (#!dart), stdin streaming, and the full Display visual SDK.",
                    Keywords = new System.Collections.Generic.List<string> { "dart", "flutter", "display", "notebook", "visuals" },
                    Sections = new System.Collections.Generic.List<PdfEditorApp.Plugins.CSharpEditor.Models.DocSection>
                    {
                        new()
                        {
                            Heading = "Running Dart in Notebooks",
                            Content = "Use the '#!dart' magic directive at the top of any notebook cell, or select Dart from the cell language picker (RUN THIS CELL AS).",
                            CalloutType = PdfEditorApp.Plugins.CSharpEditor.Models.DocCalloutType.Tip,
                            CalloutText = "Dart scripts and cells automatically receive the Display runtime without needing manual pubspec imports."
                        }
                    },
                    CodeSnippets = new System.Collections.Generic.List<PdfEditorApp.Plugins.CSharpEditor.Models.DocCodeSnippet>
                    {
                        new()
                        {
                            Id = "dart_snippet_bar",
                            Title = "Dart 2D Bar Chart",
                            Language = "dart",
                            Code = """
                                void main() {
                                  print('Dart 3 runtime active!');
                                  Display.barChart({
                                    'categories': ['Q1', 'Q2', 'Q3', 'Q4'],
                                    'series': [{'name': 'Revenue', 'data': [140, 210, 290, 380]}]
                                  }, 'Quarterly Growth Performance');
                                }
                                """,
                            Description = "Render an interactive 2D Bar Chart with customized series in Dart."
                        }
                    }
                }
            }
        };

        var docToken = PdfEditorApp.Plugins.CSharpEditor.Services.Documentation.DocumentationService.Instance.RegisterCategory(docCategory);
        if (docToken != null) context.TrackDisposable(docToken);

        // Register Dart Starter Templates in the Hub Gallery
        var dartVisualsTemplate = new PdfEditorApp.Plugins.CSharpEditor.Models.CodeTemplate
        {
            Id = "dart_visuals_starter",
            Title = "Dart Interactive Visuals & Charts",
            Category = "Visuals & Data",
            Kind = PdfEditorApp.Plugins.CSharpEditor.Models.WorkspaceItemKind.Script,
            LanguageId = "dart",
            Description = "Render interactive 2D charts and 3D surface plots in Dart using the Display SDK.",
            IconKind = Material.Icons.MaterialIconKind.CodeBraces,
            AccentColor = "#0175C2",
            AccentBackground = "#0B2D48",
            AccentBorder = "#0284C7",
            CategoryBadge = "Dart • Visuals",
            Tags = new System.Collections.Generic.List<string> { "Dart", "Charts", "Visuals", "3D" },
            Notes = "# Dart Visuals & Charts\nInteractive 2D bar charts, line plots, and 3D surface graphs in Dart 3.",
            InitialCode = """
                void main() {
                  print('Rendering Dart 3 visual components...');
                  Display.barChart({
                    'categories': ['Jan', 'Feb', 'Mar', 'Apr', 'May'],
                    'series': [
                      {'name': 'Revenue ($k)', 'data': [45, 62, 85, 110, 142]}
                    ]
                  }, 'Monthly Revenue Growth');

                  Display.plot3dSurface((double x, double y) => (x * x) - (y * y),
                    xRange: [-3.0, 3.0],
                    yRange: [-3.0, 3.0],
                    resolution: 25,
                    title: 'Hyperbolic Paraboloid');
                }
                """
        };

        var dartScriptTemplate = new PdfEditorApp.Plugins.CSharpEditor.Models.CodeTemplate
        {
            Id = "dart_script_starter",
            Title = "Dart 3 Script Starter",
            Category = "General",
            Kind = PdfEditorApp.Plugins.CSharpEditor.Models.WorkspaceItemKind.Script,
            LanguageId = "dart",
            Description = "Starter Dart 3 console automation script with interactive stdin/stdout.",
            IconKind = Material.Icons.MaterialIconKind.CodeBraces,
            AccentColor = "#0175C2",
            AccentBackground = "#0B2D48",
            AccentBorder = "#0284C7",
            CategoryBadge = "Dart • Starter",
            Tags = new System.Collections.Generic.List<string> { "Dart", "Console", "Automation" },
            Notes = "# Dart 3 Automation Script\nRun fast JIT/AOT compiled Dart automation tasks with streaming console.",
            InitialCode = """
                import 'dart:io';

                void main() {
                  print('Hello from FrySharp Dart 3!');
                  print('Platform: ${Platform.operatingSystem} (${Platform.version})');
                }
                """
        };

        var t1 = PdfEditorApp.Plugins.CSharpEditor.Services.Templates.CodeTemplateLibrary.RegisterTemplate(dartVisualsTemplate);
        if (t1 != null) context.TrackDisposable(t1);
        var t2 = PdfEditorApp.Plugins.CSharpEditor.Services.Templates.CodeTemplateLibrary.RegisterTemplate(dartScriptTemplate);
        if (t2 != null) context.TrackDisposable(t2);

        return Task.CompletedTask;
    }

    public Task DeactivateAsync() => Task.CompletedTask;
}
