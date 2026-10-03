using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>A spec the studio can draw passes; one it can't is told where and why, in words for whoever wrote it.</summary>
public class VisualSpecValidatorTests
{
    [Theory]
    [MemberData(nameof(VisualJsonTests.Fixtures), MemberType = typeof(VisualJsonTests))]
    public void EveryFixture_IsValid(string file)
    {
        Assert.Empty(VisualSpecValidator.Validate(VisualJsonTests.ReadFixture(file)));
    }

    private static VisualSpecIssue Only(string json, VisualFamily family)
    {
        var issues = VisualSpecValidator.Validate(VisualJson.Deserialize(family, json));
        return Assert.Single(issues);
    }

    [Theory]
    [InlineData("""{"kind": "line", "series": [{"x": [1, 2], "y": [1, 2, 3]}]}""", "$.series[0].x", "has 2 values but y has 3")]
    [InlineData("""{"kind": "bar", "series": [{"y": [1], "labels": ["a", "b"]}]}""", "$.series[0].labels", "has 2 values but y has 1")]
    [InlineData("""{"kind": "histogram", "series": [{"name": "no samples"}]}""", "$.series[0].values", "needs values")]
    [InlineData("""{"kind": "histogram", "series": [{"y": [1, 2], "values": [3]}]}""", "$.series[0].y", "give the samples as values")]
    [InlineData("""{"kind": "line", "series": [{"y": [1], "values": [1]}]}""", "$.series[0].values", "only a histogram counts values")]
    [InlineData("""{"kind": "pie", "series": [{"y": [3, -1]}]}""", "$.series[0].y[1]", "a slice can't be negative")]
    [InlineData("""{"kind": "histogram", "bins": 0, "series": [{"values": [1]}]}""", "$.bins", "between 1 and 50")]
    [InlineData("""{"kind": "line", "yAxis": {"min": 5, "max": 1}}""", "$.yAxis", "min must be below max")]
    [InlineData("""{"kind": "line", "width": -3}""", "$.width", "positive number of pixels")]
    public void AChartThatCantBeDrawn_IsToldWhy(string json, string path, string message)
    {
        var issue = Only(json, VisualFamily.Chart);

        Assert.Equal(path, issue.Path);
        Assert.Contains(message, issue.Message);
    }

    // C# can put NaN in a spec, JSON can't carry it: the spec is told before it is sent anywhere.
    [Fact]
    public void ANaNInACSharpSpec_IsFound()
    {
        var spec = new ChartSpec { Series = { new ChartSeriesSpec { Y = [1, double.NaN] } } };

        var issue = Assert.Single(VisualSpecValidator.Validate(spec));

        Assert.Equal("$.series[0].y[1]", issue.Path);
        Assert.Contains("null for a missing value", issue.Message);
    }

    [Theory]
    [InlineData("""{"kind": "surface"}""", "$.surface", "needs a surface")]
    [InlineData("""{"kind": "surface", "surface": {"x": {"min": 0, "max": 1}, "y": {"min": 0, "max": 1}, "z": [[1, 2], [3]]}}""", "$.surface.z[1]", "has 1 heights but row 0 has 2")]
    [InlineData("""{"kind": "wireframe", "surface": {"x": {"min": 1, "max": 1}, "y": {"min": 0, "max": 1}, "z": [[1, 2], [3, 4]]}}""", "$.surface.x", "min must be below max")]
    [InlineData("""{"kind": "scatter", "series": [{"x": [1, 2], "y": [1, 2], "z": [1]}]}""", "$.series[0].z", "has 1 values but x has 2")]
    [InlineData("""{"kind": "graph", "graph": {"nodes": [{"id": "a"}], "edges": [{"from": "a", "to": "b"}]}}""", "$.graph.edges[0].to", "no node \"b\"")]
    [InlineData("""{"kind": "scatter", "graph": {"nodes": []}}""", "$.graph", "a graph is for graph plots")]
    [InlineData("""{"kind": "graph", "graph": {"nodes": [{"id": "a", "state": "visited"}]}}""", "$.graph.nodes[0]", "states, pointers and notes are for graph visualizers")]
    public void A3DPlotThatCantBeDrawn_IsToldWhy(string json, string path, string message)
    {
        var issue = Only(json, VisualFamily.Plot3D);

        Assert.Equal(path, issue.Path);
        Assert.Contains(message, issue.Message);
    }

    [Theory]
    [InlineData("""{"kind": "tree", "state": {"grid": {"values": [[1]]}}}""", "$.state.tree", "a tree visualizer draws tree")]
    [InlineData("""{"kind": "matrix", "state": {}}""", "$.state", "the state is empty")]
    [InlineData("""{"kind": "matrix", "state": {"grid": {"values": [[1, 2], [3]]}}}""", "$.state.grid.values[1]", "a grid is rectangular")]
    [InlineData("""{"kind": "matrix", "state": {"grid": {"values": [[1]], "cells": [{"row": 0, "col": 4}]}}}""", "$.state.grid.cells[0]", "isn't a cell of this 1 × 1 grid")]
    [InlineData("""{"kind": "tree", "state": {"tree": {"nodes": [{"id": "a", "value": 1, "left": "z"}]}}}""", "$.state.tree.nodes[0].left", "no node \"z\"")]
    [InlineData("""{"kind": "tree", "state": {"tree": {"nodes": [{"id": "a", "value": 1, "left": "b", "right": "c"}, {"id": "b", "value": 2, "right": "c"}, {"id": "c", "value": 3}]}}}""", "$.state.tree.nodes[1].right", "already has a parent")]
    [InlineData("""{"kind": "tree", "state": {"tree": {"nodes": [{"id": "a", "value": 1}, {"id": "b", "value": 2}]}}}""", "$.state.tree.nodes", "can't be reached from the root")]
    [InlineData("""{"kind": "tree", "state": {"tree": {"nodes": [{"id": "a", "value": 1, "left": "b"}, {"id": "b", "value": 2, "children": ["a"]}]}}}""", "$.state.tree.root", "has a parent (\"b\")")]
    [InlineData("""{"kind": "tree", "state": {"tree": {"nodes": [{"id": "a", "value": 1, "left": "b", "children": ["c"]}, {"id": "b", "value": 2}, {"id": "c", "value": 3}]}}}""", "$.state.tree.nodes[0]", "not both")]
    [InlineData("""{"kind": "linkedList", "state": {"linkedList": {"nodes": [{"id": "a", "value": 1, "next": "b"}]}}}""", "$.state.linkedList.nodes[0].next", "no node \"b\"")]
    [InlineData("""{"kind": "arrayPointers", "state": {"array": {"values": [1, 2]}}, "pointers": [{"name": "i", "at": 5}]}""", "$.pointers[0].at", "5 is outside the 2 items")]
    [InlineData("""{"kind": "matrix", "state": {"grid": {"values": [[1]]}}, "steps": [{"highlight": [0]}]}""", "$.steps[0].highlight[0]", "must be a cell, like [2, 3]")]
    [InlineData("""{"kind": "tree", "state": {"tree": {"nodes": [{"id": "a", "value": 1}]}}, "steps": [{"changes": {"cells": [{"row": 0, "col": 0}]}}]}""", "$.steps[0].changes.cells", "has no cells to change")]
    [InlineData("""{"kind": "graph", "state": {"graph": {"nodes": [{"id": "a"}, {"id": "b"}], "edges": [{"from": "a", "to": "b"}]}}, "steps": [{"highlight": [{"from": "b", "to": "c"}]}]}""", "$.steps[0].highlight[0]", "no edge from \"b\" to \"c\"")]
    [InlineData("""{"kind": "matrix", "traversal": "inorder", "state": {"grid": {"values": [[1]]}}}""", "$.traversal", "is for tree visualizers")]
    [InlineData("""{"kind": "canvas", "state": {"canvas": {"shapes": [{"type": "text", "id": "t", "x": 1, "y": 1, "text": "a"}, {"type": "text", "id": "t", "x": 2, "y": 2, "text": "b"}]}}}""", "$.state.canvas.shapes[1].id", "ids must be unique")]
    [InlineData("""{"kind": "bars", "state": {"bars": {"values": [1, 2]}}, "steps": [{"line": 0}]}""", "$.steps[0].line", "lines count from 1")]
    [InlineData("""{"kind": "board", "state": {"grid": {"values": [["Q"]], "cells": [{"row": 0, "col": 0, "terrain": "wall"}]}}}""", "$.state.grid.cells[0]", "terrain, clusters, heat and notes are for matrix visualizers")]
    [InlineData("""{"kind": "matrix", "state": {"grid": {"values": [[1]], "checkerboard": true}}}""", "$.state.grid.checkerboard", "is for board visualizers")]
    [InlineData("""{"kind": "arrayPointers", "state": {"array": {"values": [1], "items": [{"index": 0, "label": "first"}]}}}""", "$.state.array.items[0]", "labels and pointer tags are for bars")]
    [InlineData("""{"kind": "graph", "state": {"graph": {"nodes": [{"id": "a", "z": 3}]}}}""", "$.state.graph.nodes[0]", "z and size are for 3D graphs")]
    [InlineData("""{"kind": "canvas", "state": {"canvas": {"shapes": []}}, "steps": [{"highlight": ["x"]}]}""", "$.steps[0].highlight", "a canvas draws its shapes as they are")]
    [InlineData("""{"kind": "canvas", "state": {"canvas": {"shapes": []}}, "pointers": [{"name": "p", "at": "x"}]}""", "$.pointers[0]", "a canvas has no pointers")]
    public void AVisualizerThatCantBeDrawn_IsToldWhy(string json, string path, string message)
    {
        var issue = Only(json, VisualFamily.Visualizer);

        Assert.Equal(path, issue.Path);
        Assert.Contains(message, issue.Message);
    }

    // A step's highlights are checked against the latest whole state, which a step can replace.
    [Fact]
    public void AStepsNewState_IsWhatLaterStepsAreCheckedAgainst()
    {
        var spec = VisualJson.Deserialize<VisualizerSpec>("""
            {"kind": "arrayPointers", "state": {"array": {"values": [1]}},
             "steps": [{"state": {"array": {"values": [1, 2, 3]}}}, {"highlight": [2]}]}
            """);

        Assert.Empty(VisualSpecValidator.Validate(spec));
    }

    [Fact]
    public void ManyProblems_AreCappedSoTheMessageStaysReadable()
    {
        var spec = new ChartSpec { Kind = ChartType.Pie, Series = { new ChartSeriesSpec { Y = Enumerable.Repeat<double?>(-1, 100).ToList() } } };

        var issues = VisualSpecValidator.Validate(spec);

        Assert.Equal(VisualSpecValidator.MaxIssues, issues.Count);
    }

    [Fact]
    public void EnsureValid_ThrowsWithEveryProblem()
    {
        var spec = new Plot3DSpec { Kind = Plot3DType.Surface, Series = { new Plot3DSeriesSpec() } };

        var error = Assert.Throws<VisualSpecException>(() => VisualSpecValidator.EnsureValid(spec));

        Assert.Contains("$.surface: a surface plot needs a surface", error.Message);
        Assert.Contains("$.series: a surface plot draws its surface", error.Message);
    }
}
