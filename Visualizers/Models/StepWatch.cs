using System.Collections.Generic;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

public enum WatchKind
{
    Value,
    List,
    Queue,
    Stack,
    PriorityQueue,
    Set,
    Map
}

/// <summary>A watched collection as it looked when a step was recorded (items in the order they come out).</summary>
public sealed class StepWatch
{
    public StepWatch(string name, WatchKind kind, IReadOnlyList<string> items, int count)
    {
        Name = name;
        Kind = kind;
        Items = items;
        Count = count;
    }

    public string Name { get; }
    public WatchKind Kind { get; }

    /// <summary>Display text of the first items; <see cref="Count"/> can be larger.</summary>
    public IReadOnlyList<string> Items { get; }

    public int Count { get; }
}
