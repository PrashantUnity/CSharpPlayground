using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

/// <summary>Live collections registered with Watch(...); every step recorded afterwards snapshots them.</summary>
public sealed class WatchList
{
    private readonly List<(string Name, object Source)> _entries = new();

    public void Add(object source, string name)
    {
        name = string.IsNullOrWhiteSpace(name) ? source.GetType().Name : name.Trim();
        int existing = _entries.FindIndex(e => e.Name == name);
        if (existing >= 0)
        {
            _entries[existing] = (name, source);
        }
        else
        {
            _entries.Add((name, source));
        }
    }

    public List<StepWatch> Capture() => _entries.Select(e => WatchCapture.Capture(e.Name, e.Source)).ToList();
}

public static class WatchCapture
{
    public const int MaxItems = 24;
    private const int MaxTextLength = 24;
    private const int MaxNestedItems = 6;

    public static StepWatch Capture(string name, object? source)
    {
        try
        {
            return CaptureCore(name, source);
        }
        catch (Exception ex)
        {
            // Snapshotting runs inside the learner's algorithm; a throwing getter must not break it.
            return new StepWatch(name, WatchKind.Value, new[] { $"({ex.GetType().Name})" }, 1);
        }
    }

    private static StepWatch CaptureCore(string name, object? source)
    {
        if (source == null || source is string || ObjectInspectorBuilder.IsScalarType(source.GetType()))
        {
            return new StepWatch(name, WatchKind.Value, new[] { Describe(source) }, 1);
        }

        if (source is IDictionary map)
        {
            var entries = new List<string>();
            foreach (DictionaryEntry entry in map)
            {
                if (entries.Count == MaxItems) break;
                entries.Add(Truncate($"{Describe(entry.Key)}: {Describe(entry.Value)}"));
            }
            return new StepWatch(name, WatchKind.Map, entries, map.Count);
        }

        var type = source.GetType();
        var definition = type.IsGenericType ? type.GetGenericTypeDefinition() : null;

        if (definition == typeof(PriorityQueue<,>))
        {
            return CapturePriorityQueue(name, source, type);
        }

        if (source is not IEnumerable items)
        {
            return new StepWatch(name, WatchKind.Value, new[] { Describe(source) }, 1);
        }

        var kind = definition == typeof(Queue<>) || definition == typeof(ConcurrentQueue<>) ? WatchKind.Queue
            : definition == typeof(Stack<>) || definition == typeof(ConcurrentStack<>) ? WatchKind.Stack
            : definition == typeof(HashSet<>) || definition == typeof(SortedSet<>) ? WatchKind.Set
            : WatchKind.List;

        // Queue and Stack enumerate in removal order (front first, top first), which is how they are drawn.
        var described = new List<string>();
        int count = 0;
        foreach (var item in items)
        {
            if (count < MaxItems) described.Add(Describe(item));
            count++;
        }
        return new StepWatch(name, kind, described, count);
    }

    // Shown in dequeue order: sorted by priority with the queue's own comparer (so max-heaps work too).
    private static StepWatch CapturePriorityQueue(string name, object queue, Type type)
    {
        var priorityType = type.GetGenericArguments()[1];
        var comparer = type.GetProperty(nameof(PriorityQueue<object, object>.Comparer))!.GetValue(queue)!;
        var compare = typeof(IComparer<>).MakeGenericType(priorityType).GetMethod(nameof(IComparer<object>.Compare))!;
        var unordered = (IEnumerable)type.GetProperty(nameof(PriorityQueue<object, object>.UnorderedItems))!.GetValue(queue)!;

        var entries = new List<(object? Element, object? Priority)>();
        foreach (var entry in unordered)
        {
            var tuple = entry!.GetType();
            entries.Add((tuple.GetField("Item1")!.GetValue(entry), tuple.GetField("Item2")!.GetValue(entry)));
        }

        entries.Sort((a, b) => (int)compare.Invoke(comparer, new[] { a.Priority, b.Priority })!);
        var described = entries.Take(MaxItems).Select(e => Truncate($"{Describe(e.Element)} ({Describe(e.Priority)})")).ToList();
        return new StepWatch(name, WatchKind.PriorityQueue, described, entries.Count);
    }

    private static string Describe(object? item)
    {
        var text = item switch
        {
            null => "null",
            string s => s,
            _ when ObjectInspectorBuilder.IsScalarType(item.GetType()) => ObjectInspectorBuilder.FormatScalarValue(item),
            IEnumerable nested => DescribeNested(nested),
            _ => DescribeObject(item)
        };
        return Truncate(text);
    }

    private static string DescribeNested(IEnumerable nested)
    {
        var parts = nested.Cast<object?>().Take(MaxNestedItems + 1).Select(DescribeShallow).ToList();
        var shown = string.Join(", ", parts.Take(MaxNestedItems));
        return parts.Count > MaxNestedItems ? $"[{shown}, …]" : $"[{shown}]";
    }

    private static string DescribeObject(object item)
    {
        var type = item.GetType();
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
        {
            return $"{DescribeShallow(type.GetProperty("Key")!.GetValue(item))}: {DescribeShallow(type.GetProperty("Value")!.GetValue(item))}";
        }

        // Tree/list/graph nodes read best as their value rather than their type name.
        var value = VisualizerReflectionHelper.GetMemberValue(item, "DisplayValue", "val", "Value", "Data", "Key", "Name", "Label", "Id");
        return value != null ? DescribeShallow(value) : item.ToString() ?? type.Name;
    }

    private static string DescribeShallow(object? item) => item switch
    {
        null => "null",
        _ when ObjectInspectorBuilder.IsScalarType(item.GetType()) => ObjectInspectorBuilder.FormatScalarValue(item),
        _ => item.ToString() ?? item.GetType().Name
    };

    private static string Truncate(string text) =>
        text.Length <= MaxTextLength ? text : text[..(MaxTextLength - 1)] + "…";
}
