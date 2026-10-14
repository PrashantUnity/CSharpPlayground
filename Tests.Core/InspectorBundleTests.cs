using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using Xunit;

namespace FrySharp.Core.Tests;

/// <summary>An object inspector sent by a kernel in another language reads back as the studio's own inspector.</summary>
public sealed class InspectorBundleTests
{
    // What the Java kernel sends for a People object whose "another" points back at itself.
    private const string JavaPeople = """
        {
          "application/vnd.fry.inspector+json": {
            "title": "People",
            "properties": [
              { "name": "name", "value": "Prashant", "isString": true },
              { "name": "numbers", "value": "[ 1, 34, 45 ]" },
              { "name": "age", "value": 30 },
              { "name": "nickname", "value": "<null>", "isNull": true },
              { "name": "address", "child": { "title": "Address", "properties": [ { "name": "city", "value": "Zurich", "isString": true } ] } },
              { "name": "another", "child": { "title": "People", "circular": true, "properties": [] } }
            ]
          },
          "text/plain": "People@1b2c6ec2"
        }
        """;

    private static ObjectInspectorNode Inspect(string bundle)
    {
        using var document = JsonDocument.Parse(bundle);
        var output = MimeOutputMapper.Map(document.RootElement, default);
        Assert.Null(output.Text);
        var rich = Assert.IsType<RichCellOutput>(output.Rich);
        Assert.Equal(CellOutputKind.ObjectInspector, rich.Kind);
        return Assert.IsType<ObjectInspectorNode>(rich.InspectorNode);
    }

    [Fact]
    public void AKernelsInspector_IsMappedToTheObjectInspector_NotToItsTextFallback()
    {
        var node = Inspect(JavaPeople);

        Assert.Equal("People", node.HeaderTitle);
        Assert.True(node.IsExpanded);
        Assert.Equal(["name", "numbers", "age", "nickname", "address", "another"], node.Properties.Select(p => p.Name));

        var name = node.Properties[0];
        Assert.Equal("Prashant", name.SimpleValueText);
        Assert.True(name.IsString);

        Assert.Equal("[ 1, 34, 45 ]", node.Properties[1].SimpleValueText);
        Assert.Equal("30", node.Properties[2].SimpleValueText);
        Assert.True(node.Properties[3].IsNull);
    }

    [Fact]
    public void ANestedObject_IsAChildNode_AndACycleIsMarkedAndClosed()
    {
        var node = Inspect(JavaPeople);

        var address = node.Properties[4];
        Assert.True(address.IsComplexChild);
        Assert.Equal("Address", address.ChildNode!.HeaderTitle);
        Assert.True(address.ChildNode.IsExpanded);
        Assert.Equal("city", address.ChildNode.Properties.Single().Name);

        var another = node.Properties[5];
        Assert.True(another.ChildNode!.IsCircularReference);
        Assert.False(another.ChildNode.IsExpanded);
    }

    [Fact]
    public void ObjectsNestedDeeperThanTheLimit_KeepTheirTitleAndLoseTheirRows()
    {
        var json = "{\"title\":\"leaf\",\"properties\":[{\"name\":\"x\",\"value\":\"1\"}]}";
        for (var level = 0; level < InspectorBundle.MaxDepth + 2; level++)
        {
            json = "{\"title\":\"n" + level + "\",\"properties\":[{\"name\":\"c\",\"child\":" + json + "}]}";
        }

        var node = Inspect("{\"" + RichOutputMime.InspectorMime + "\":" + json + "}");

        var depth = 0;
        for (var current = node; current.Properties.Count > 0; current = current.Properties[0].ChildNode!) depth++;
        Assert.Equal(InspectorBundle.MaxDepth, depth);
    }

    [Fact]
    public void AnInspectorTheStudioWrote_ReadsBackTheSame()
    {
        var people = new ObjectInspectorNode("People");
        people.Properties.Add(new ObjectInspectorPropertyRow { Name = "Name", SimpleValueText = "Code", IsString = true });
        people.Properties.Add(new ObjectInspectorPropertyRow
        {
            Name = "Another",
            IsComplexChild = true,
            ChildNode = new ObjectInspectorNode("People", isExpanded: false) { IsCircularReference = true }
        });

        var bundle = RichOutputMime.ToBundle(new RichCellOutput { Kind = CellOutputKind.ObjectInspector, InspectorNode = people }, out _);
        var read = Inspect(bundle!.ToJsonString());

        Assert.Equal("People", read.HeaderTitle);
        Assert.Equal("Code", read.Properties[0].SimpleValueText);
        Assert.True(read.Properties[0].IsString);
        Assert.True(read.Properties[1].ChildNode!.IsCircularReference);
    }

    [Fact]
    public void ABundleWithNoInspector_StillFallsBackToText()
    {
        using var document = JsonDocument.Parse("""{ "text/plain": "People@1b2c6ec2" }""");

        var output = MimeOutputMapper.Map(document.RootElement, default);

        Assert.Equal("People@1b2c6ec2\n", output.Text);
        Assert.Null(output.Rich);
    }
}
