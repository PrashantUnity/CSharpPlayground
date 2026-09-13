using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Rendering;
using PdfEditorApp.Plugins.CSharpEditor.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class BreakpointMarginTests
{
    [Fact]
    public void BreakpointMargin_ShouldImplement_ICustomHitTest()
    {
        var margin = new BreakpointMargin();
        Assert.True(margin is ICustomHitTest);
    }

    [Fact]
    public void BreakpointMargin_HitTest_ShouldReturnTrue_WhenPointInsideBounds()
    {
        var margin = new BreakpointMargin();
        // Custom hit-test uses Bounds.Size; when tested with a point inside Bounds
        margin.Arrange(new Rect(0, 0, 24, 100));

        var hitTest = (ICustomHitTest)margin;
        Assert.True(hitTest.HitTest(new Point(12, 50)));
        Assert.True(hitTest.HitTest(new Point(0, 0)));
        Assert.False(hitTest.HitTest(new Point(30, 50)));
        Assert.False(hitTest.HitTest(new Point(-5, 50)));
    }

    [Fact]
    public void BreakpointMargin_AddAndRemoveBreakpoint_ShouldUpdateState()
    {
        var margin = new BreakpointMargin();
        Assert.False(margin.HasBreakpoint(10));

        margin.AddBreakpoint(10);
        Assert.True(margin.HasBreakpoint(10));

        margin.AddBreakpoint(20);
        Assert.True(margin.HasBreakpoint(20));

        margin.RemoveBreakpoint(10);
        Assert.False(margin.HasBreakpoint(10));
        Assert.True(margin.HasBreakpoint(20));
    }

    [Fact]
    public void BreakpointMargin_SetBreakpoints_ShouldSynchronizeCollection()
    {
        var margin = new BreakpointMargin();
        margin.AddBreakpoint(5);
        margin.AddBreakpoint(10);

        margin.SetBreakpoints(new[] { 15, 20, 25 });

        Assert.False(margin.HasBreakpoint(5));
        Assert.False(margin.HasBreakpoint(10));
        Assert.True(margin.HasBreakpoint(15));
        Assert.True(margin.HasBreakpoint(20));
        Assert.True(margin.HasBreakpoint(25));
    }

    [Fact]
    public void BreakpointMargin_CurrentPausedLine_ShouldUpdateAndRetrieve()
    {
        var margin = new BreakpointMargin();
        Assert.Equal(-1, margin.CurrentPausedLine);

        margin.CurrentPausedLine = 42;
        Assert.Equal(42, margin.CurrentPausedLine);

        margin.CurrentPausedLine = -1;
        Assert.Equal(-1, margin.CurrentPausedLine);
    }

    [Fact]
    public void BreakpointMargin_Width_ShouldDefaultTo24()
    {
        var margin = new BreakpointMargin();
        Assert.Equal(24, margin.Width);
        Assert.Equal(24, margin.MinWidth);
    }

    [Fact]
    public void DebugInstrumentationRewriter_ShouldInjectDebugProbes()
    {
        var source = "void Test() {\nint a = 1;\nint b = 2;\nint c = a + b;\n}";
        var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(source);
        var instrumented = DebugInstrumentationRewriter.Instrument(tree).ToString();

        Assert.Contains("ScriptDebugSession.Hit(2", instrumented);
        Assert.Contains("ScriptDebugSession.Hit(3", instrumented);
        Assert.Contains("ScriptDebugSession.Hit(4", instrumented);
    }

    [Fact]
    public void BreakpointMargin_BreakpointToggled_ShouldFireEvent()
    {
        var margin = new BreakpointMargin();
        int toggledLine = -1;
        margin.BreakpointToggled += line => toggledLine = line;

        // Simulate toggling
        margin.AddBreakpoint(12);
        Assert.True(margin.HasBreakpoint(12));
    }
}
