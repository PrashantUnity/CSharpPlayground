using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.Visualizers;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.Debugging;

public class DebugTypeVisualizerTests
{
    private class CustomUser
    {
        public int Id { get; set; } = 42;
        public string Name { get; set; } = "Prashant";
        public string Email { get; set; } = "prashant@example.com";
        public List<string> Roles { get; set; } = new() { "Admin", "Developer" };
    }

    private class CustomOrder
    {
        public int OrderId { get; set; } = 999;
        public CustomUser User { get; set; } = new();
    }

    [Fact]
    public void Registry_FindsHttpVisualizer_ForHttpTypes()
    {
        var resp = new HttpResponseMessage(HttpStatusCode.OK);
        var visualizer = DebugTypeVisualizerRegistry.Default.FindVisualizer(resp, resp.GetType());

        Assert.NotNull(visualizer);
        Assert.IsType<HttpTypeVisualizer>(visualizer);
        var summary = visualizer.FormatSummary(resp, resp.GetType());
        Assert.Contains("200 OK", summary);
    }

    [Fact]
    public void Registry_FindsCollectionVisualizer_ForCollections()
    {
        var list = new List<string> { "Apple", "Banana", "Cherry" };
        var visualizer = DebugTypeVisualizerRegistry.Default.FindVisualizer(list, list.GetType());

        Assert.NotNull(visualizer);
        Assert.IsType<CollectionTypeVisualizer>(visualizer);
        var summary = visualizer.FormatSummary(list, list.GetType());
        Assert.Equal("Count = 3", summary);

        var children = visualizer.GetChildren(list, list.GetType()).ToList();
        Assert.Equal(3, children.Count);
        Assert.Equal("[0]", children[0].Name);
        Assert.Equal("Apple", children[0].Value);
    }

    [Fact]
    public void Registry_FindsFallbackReflectionVisualizer_ForPoco()
    {
        var user = new CustomUser();
        var visualizer = DebugTypeVisualizerRegistry.Default.FindVisualizer(user, user.GetType());

        Assert.NotNull(visualizer);
        Assert.IsType<DefaultReflectionVisualizer>(visualizer);

        var children = visualizer.GetChildren(user, user.GetType()).ToList();
        Assert.Contains(children, c => c.Name == "Id" && Equals(c.Value, 42));
        Assert.Contains(children, c => c.Name == "Name" && Equals(c.Value, "Prashant"));
    }

    [Fact]
    public void LazyExpansion_ExpandsVariableChildren_OnDemand()
    {
        var order = new CustomOrder();
        var orderVar = new DebugVariableItem
        {
            Name = "order",
            TypeName = "CustomOrder",
            RawValue = order,
            PathExpression = "order",
            HasChildren = true,
            ChildrenLoaded = false
        };

        Assert.Empty(orderVar.Children);

        var expanded = ScriptDebugSession.ExpandVariableChildren(orderVar);
        Assert.NotEmpty(expanded);
        Assert.True(orderVar.ChildrenLoaded);
        Assert.Equal(2, orderVar.Children.Count);

        var userVar = orderVar.Children.Single(c => c.Name == "User");
        Assert.NotNull(userVar);
        Assert.True(userVar.HasChildren);

        // Expand nested child lazily
        var userChildren = ScriptDebugSession.ExpandVariableChildren(userVar);
        Assert.NotEmpty(userChildren);
        Assert.Contains(userChildren, c => c.Name == "Name" && c.ValueDisplay == "\"Prashant\"");
    }

    [Fact]
    public void CustomVisualizer_CanBeRegistered_WithHigherPriority()
    {
        var registry = new DebugTypeVisualizerRegistry();
        registry.Register(new DefaultReflectionVisualizer());

        var customVisualizer = new CustomDummyVisualizer();
        registry.Register(customVisualizer);

        var found = registry.FindVisualizer("custom_string", typeof(string));
        Assert.Same(customVisualizer, found);
    }

    private class CustomDummyVisualizer : IDebugTypeVisualizer
    {
        public int Priority => 999;
        public bool CanVisualize(object? value, Type type) => value is string;
        public string FormatSummary(object value, Type type) => "CUSTOM:" + value;
        public IEnumerable<(string Name, object? Value)> GetChildren(object value, Type type) => Enumerable.Empty<(string, object?)>();
    }
}
