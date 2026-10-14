using System.Reflection;
using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// One <c>Display</c> in a desktop script: Core's members (what the browser studio and the CLI have too) and the desktop's
/// own (live controls, Animate, Avalonia bitmaps), through the same name, so scripts written before the split still compile.
/// </summary>
public class DisplayFacadeTests
{
    [Fact]
    public void ADesktopScript_CallsCoresAndTheDesktopsMembers_ThroughOneDisplay()
    {
        const string script = """
            Display.Html("<b>hi</b>");
            Display.Markdown("# Title");
            Display.Table(new[] { 1, 2, 3 }, "numbers");
            Display.Image(new byte[] { 1, 2, 3 });
            Display.Image("image.png");
            Display.ThrowIfCancellationRequested();
            var token = Display.CancellationToken;
            Display.Image(new Avalonia.Media.Imaging.WriteableBitmap(new Avalonia.PixelSize(1, 1), new Avalonia.Vector(96, 96)));
            Display.Control(new Avalonia.Controls.TextBlock { Text = "live" });
            var box = Display.Control(() => new Avalonia.Controls.TextBox());
            var animation = Display.Animate((context, elapsed) => context.DrawEllipse(Brushes.Red, null, new Avalonia.Point(50, 50), 10, 10));
            new[] { 1, 2 }.Dump("dumped");
            """;

        var errors = new RoslynCompilerService().CheckDiagnostics(script).Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors.Select(e => $"{e.Id} at {e.Line}: {e.Message}")));
    }

    [Fact]
    public void TheDesktopsDisplay_IsCoresDisplay_WithMore()
    {
        var desktop = typeof(PdfEditorApp.Plugins.CSharpEditor.Services.Display.Display);
        var core = typeof(PdfEditorApp.Plugins.CSharpEditor.Scripting.Display);

        Assert.Same(core, desktop.BaseType);
        // Nothing of Core's is hidden by a desktop member of the same name and parameters.
        const BindingFlags statics = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var coreSignatures = core.GetMethods(statics).Select(Signature).ToHashSet();
        Assert.DoesNotContain(desktop.GetMethods(statics).Select(Signature), coreSignatures.Contains);
    }

    private static string Signature(MethodInfo method) =>
        $"{method.Name}({string.Join(",", method.GetParameters().Select(p => p.ParameterType.FullName ?? p.ParameterType.Name))})";
}
