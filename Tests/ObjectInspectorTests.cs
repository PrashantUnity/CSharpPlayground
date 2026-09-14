using System;
using System.Collections.Generic;
using System.Linq;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class TestPeople
{
    public string Name { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty;
    public int[] Numbers { get; set; } = [1, 34, 45, 235, 25];
    public TestPeople? Another { get; set; }

    public void WhoAreYou()
    {
        Console.WriteLine($"My name is {Name}");
    }
}

public class ObjectInspectorTests
{
    [Fact]
    public void IsCollectionOfScalars_ShouldFormat_PrimitiveArrayInline()
    {
        var numbers = new[] { 1, 34, 45, 235, 25 };
        bool isScalarCol = ObjectInspectorBuilder.IsCollectionOfScalars(numbers, out var formatted);

        Assert.True(isScalarCol);
        Assert.Equal("[ 1, 34, 45, 235, 25 ]", formatted);
    }

    [Fact]
    public void IsCollectionOfScalars_ShouldFormat_ListInline()
    {
        var list = new List<int> { 1, 3, 4, 5, 6, 7, 8, 10 };
        bool isScalarCol = ObjectInspectorBuilder.IsCollectionOfScalars(list, out var formatted);

        Assert.True(isScalarCol);
        Assert.Equal("[ 1, 3, 4, 5, 6, 7, 8, 10 ]", formatted);
    }

    [Fact]
    public void Build_ShouldInspect_ComplexObject_WithProperties()
    {
        var people = new TestPeople();
        var node = ObjectInspectorBuilder.Build(people);

        Assert.NotNull(node);
        Assert.Equal("TestPeople", node.HeaderTitle);
        Assert.True(node.IsExpanded);

        // Find Name property
        var nameProp = node.Properties.FirstOrDefault(p => p.Name == "Name");
        Assert.NotNull(nameProp);
        Assert.Equal(string.Empty, nameProp.SimpleValueText);

        // Find Class property
        var classProp = node.Properties.FirstOrDefault(p => p.Name == "Class");
        Assert.NotNull(classProp);
        Assert.Equal(string.Empty, classProp.SimpleValueText);

        // Find Numbers property (should be formatted inline array)
        var numbersProp = node.Properties.FirstOrDefault(p => p.Name == "Numbers");
        Assert.NotNull(numbersProp);
        Assert.Equal("[ 1, 34, 45, 235, 25 ]", numbersProp.SimpleValueText);

        // Find Another property (should be <null>)
        var anotherProp = node.Properties.FirstOrDefault(p => p.Name == "Another");
        Assert.NotNull(anotherProp);
        Assert.True(anotherProp.IsNull);
        Assert.Equal("<null>", anotherProp.SimpleValueText);
    }

    [Fact]
    public void Build_ShouldHandle_CircularReferences_WithoutInfiniteLoop()
    {
        var people = new TestPeople
        {
            Name = "Code",
            Class = "II"
        };
        people.Another = people; // Circular self-reference!

        // Must not throw StackOverflowException or loop forever!
        var node = ObjectInspectorBuilder.Build(people);

        Assert.NotNull(node);
        Assert.Equal("TestPeople", node.HeaderTitle);

        var nameProp = node.Properties.FirstOrDefault(p => p.Name == "Name");
        Assert.NotNull(nameProp);
        Assert.Equal("Code", nameProp.SimpleValueText);

        var classProp = node.Properties.FirstOrDefault(p => p.Name == "Class");
        Assert.NotNull(classProp);
        Assert.Equal("II", classProp.SimpleValueText);

        // Another should be a complex child node pointing back to TestPeople
        var anotherProp = node.Properties.FirstOrDefault(p => p.Name == "Another");
        Assert.NotNull(anotherProp);
        Assert.True(anotherProp.IsComplexChild);
        Assert.NotNull(anotherProp.ChildNode);
        Assert.Equal("TestPeople", anotherProp.ChildNode.HeaderTitle);
    }

    [Fact]
    public void Build_ShouldHandle_NullObject()
    {
        var node = ObjectInspectorBuilder.Build(null);
        Assert.NotNull(node);
        Assert.Equal("null", node.HeaderTitle);
        Assert.Single(node.Properties);
        Assert.True(node.Properties[0].IsNull);
        Assert.Equal("<null>", node.Properties[0].SimpleValueText);
    }

    [Fact]
    public void ToggleExpand_ShouldToggle_IsExpandedAndToggleIcon()
    {
        var node = new ObjectInspectorNode("Test", isExpanded: true, isExpandable: true);
        Assert.Equal("▼", node.ToggleIcon);

        node.ToggleExpand();
        Assert.False(node.IsExpanded);
        Assert.Equal("▶", node.ToggleIcon);

        node.ToggleExpand();
        Assert.True(node.IsExpanded);
        Assert.Equal("▼", node.ToggleIcon);
    }

    [Fact]
    public async System.Threading.Tasks.Task Kernel_ShouldEmit_InlineListAndObjectInspector()
    {
        var kernel = new NotebookExecutionKernel();

        // 1. Array/List inline evaluation (matches Screenshot 1)
        string liveConsole = "";
        RichCellOutput? richOutput = null;

        var r1 = await kernel.ExecuteCellAsync(
            "var list = new System.Collections.Generic.List<int> { 1, 3, 4, 5, 6, 7, 8, 10 };\nlist",
            onLiveConsole: text => liveConsole += text,
            onRichOutput: rich => richOutput = rich);

        Assert.True(r1.Success);
        Assert.Contains("[ 1, 3, 4, 5, 6, 7, 8, 10 ]", liveConsole);

        // 2. Class definition (matches Screenshot 1 & 2)
        var r2 = await kernel.ExecuteCellAsync(@"
public class NotebookSamplePerson
{
    public string Name { get; set; } = ""Code"";
    public string Class { get; set; } = ""II"";
    public int[] Numbers { get; set; } = [1, 34, 45, 235, 25];
}");
        Assert.True(r2.Success);

        // 3. Object evaluation (matches Screenshot 2 & 3)
        liveConsole = "";
        richOutput = null;
        var r3 = await kernel.ExecuteCellAsync(
            "var p = new NotebookSamplePerson();\np",
            onLiveConsole: text => liveConsole += text,
            onRichOutput: rich => richOutput = rich);

        Assert.True(r3.Success);
        Assert.NotNull(richOutput);
        Assert.Equal(CellOutputKind.ObjectInspector, richOutput.Kind);
        Assert.NotNull(richOutput.InspectorNode);
        Assert.Contains(richOutput.InspectorNode.Properties, prop => prop.Name == "Name" && prop.SimpleValueText == "Code");
        Assert.Contains(richOutput.InspectorNode.Properties, prop => prop.Name == "Numbers" && prop.SimpleValueText == "[ 1, 34, 45, 235, 25 ]");
    }

    [Fact]
    public async System.Threading.Tasks.Task Kernel_ShouldEmit_TableOnDump()
    {
        var kernel = new NotebookExecutionKernel();
        RichCellOutput? richOutput = null;

        var result = await kernel.ExecuteCellAsync(
            "int[] digit = [1, 2, 3, 4, 4, 0, 0, 7, 6];\ndigit.Dump();",
            onRichOutput: rich => richOutput = rich);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(richOutput);
        Assert.Equal(CellOutputKind.Table, richOutput.Kind);
        Assert.NotNull(richOutput.TableResult);
        Assert.Equal(9, richOutput.TableResult.Rows.Count);
        Assert.Single(richOutput.TableResult.Columns);
        Assert.Equal("Item", richOutput.TableResult.Columns[0].Header);
    }

    [Fact]
    public async System.Threading.Tasks.Task Kernel_ShouldEmit_TableOnAnonymousObjectDump()
    {
        var kernel = new NotebookExecutionKernel();
        RichCellOutput? richOutput = null;

        var result = await kernel.ExecuteCellAsync(
            "var obj = new { Title = \"FryPDF\", Version = \"1.0.0\", Count = 42 };\nobj.Dump();",
            onRichOutput: rich => richOutput = rich);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(richOutput);
        Assert.Equal(CellOutputKind.Table, richOutput.Kind);
        Assert.NotNull(richOutput.TableResult);
        Assert.Equal(3, richOutput.TableResult.Rows.Count);
        Assert.Equal(2, richOutput.TableResult.Columns.Count);
        Assert.Equal("Property", richOutput.TableResult.Columns[0].Header);
        Assert.Equal("Value", richOutput.TableResult.Columns[1].Header);
    }
}