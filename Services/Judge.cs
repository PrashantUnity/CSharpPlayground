using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

/// <summary>
/// Checks answers against LeetCode-style expected output and prints one line per case:
/// <c>✅ Example 1 → [0,1]</c> or <c>❌ Example 1 → got [1,0] · expected [0,1]</c>. The Code Studio Test Cases panel
/// reads these lines, so a case passes only when its own line is a ✅.
/// </summary>
public sealed class Judge
{
    public const string PassMark = "✅";
    public const string FailMark = "❌";
    private const string Arrow = " → ";
    private const int MaxListNodes = 1000;
    private const int MaxTreeNodes = 2000;

    // Answers are for reading, not for HTML: keep <, &, ' and accented letters as they are.
    private static readonly JsonSerializerOptions TextOptions = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public int Passed { get; private set; }
    public int Failed { get; private set; }
    public bool AllPassed => Failed == 0 && Passed > 0;

    /// <summary>Runs one case. Expected is written the way LeetCode prints it: <c>[0,1]</c>, <c>true</c>, <c>"bab"</c>, <c>[[1,2],[3]]</c>.</summary>
    public bool Case<T>(string name, Func<T> run, string expected, bool anyOrder = false)
    {
        object? actual;
        try
        {
            actual = run();
        }
        catch (Exception ex)
        {
            Report(name, false, $"threw {ex.GetType().Name}: {ex.Message}");
            return false;
        }
        return Check(name, Format(actual, typeof(T)), expected, anyOrder);
    }

    public bool Case(string name, object? actual, string expected, bool anyOrder = false) =>
        Check(name, Format(actual), expected, anyOrder);

    /// <summary>
    /// Stress test: runs <paramref name="trials"/> random inputs through a trusted (usually brute-force) version and
    /// the candidate, and reports the first input where they disagree.
    /// </summary>
    public bool Agree<TInput, TOutput>(
        string name,
        Func<Random, TInput> generate,
        Func<TInput, TOutput> reference,
        Func<TInput, TOutput> candidate,
        int trials = 200,
        bool anyOrder = false,
        int seed = 75)
    {
        var random = new Random(seed);
        for (int trial = 1; trial <= trials; trial++)
        {
            var input = generate(random);
            string expected, actual;
            try
            {
                expected = Format(reference(input), typeof(TOutput));
                actual = Format(candidate(input), typeof(TOutput));
            }
            catch (Exception ex)
            {
                Report(name, false, $"input {Format(input)} threw {ex.GetType().Name}: {ex.Message}");
                return false;
            }

            if (Canonical(actual, anyOrder) != Canonical(expected, anyOrder))
            {
                Report(name, false, $"input {Format(input)}: got {actual} · expected {expected}");
                return false;
            }
        }

        Report(name, true, $"{trials} random inputs agree with the reference");
        return true;
    }

    public void Summary()
    {
        int total = Passed + Failed;
        Console.WriteLine(Failed == 0
            ? $"🏁 All {total} test{(total == 1 ? "" : "s")} passed"
            : $"🏁 {Passed}/{total} passed, {Failed} failed");
    }

    private bool Check(string name, string got, string expected, bool anyOrder)
    {
        bool ok = Canonical(got, anyOrder) == Canonical(expected, anyOrder);
        Report(name, ok, ok ? got : $"got {got} · expected {expected}");
        return ok;
    }

    private void Report(string name, bool ok, string detail)
    {
        if (ok) Passed++;
        else Failed++;
        Console.WriteLine($"{(ok ? PassMark : FailMark)} {name}{Arrow}{detail}");
    }

    /// <summary>For the Test Cases panel: true or false when the output has this case's line, null when it has none.</summary>
    public static bool? Verdict(string output, string caseName)
    {
        var line = LineFor(output, caseName);
        return line == null ? null : line.StartsWith(PassMark, StringComparison.Ordinal);
    }

    /// <summary>The last ✅/❌ line printed for <paramref name="caseName"/>, or null.</summary>
    public static string? LineFor(string output, string caseName)
    {
        string? found = null;
        foreach (var raw in output.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith($"{PassMark} {caseName}{Arrow}", StringComparison.Ordinal) ||
                line.StartsWith($"{FailMark} {caseName}{Arrow}", StringComparison.Ordinal))
            {
                found = line;
            }
        }
        return found;
    }

    /// <summary>
    /// Like <see cref="Format(object?)"/>, but an empty linked list or tree (null of a node type) prints as [], the way
    /// LeetCode shows it.
    /// </summary>
    public static string Format(object? value, Type declaredType) =>
        value == null && IsNodeType(declaredType) ? "[]" : Format(value);

    // List and tree nodes link to their own type; graph nodes (Clone Graph) keep a list of neighbours of their own type.
    private static bool IsNodeType(Type type) =>
        !type.IsValueType && type != typeof(string) && type != typeof(object) &&
        (new[] { "next", "Next", "left", "Left", "right", "Right" }.Any(name =>
            type.GetField(name)?.FieldType == type || type.GetProperty(name)?.PropertyType == type) ||
         new[] { "neighbors", "Neighbors" }.Any(name =>
            typeof(IEnumerable<>).MakeGenericType(type).IsAssignableFrom(type.GetField(name)?.FieldType ?? type.GetProperty(name)?.PropertyType ?? typeof(object))));

    /// <summary>A value the way LeetCode prints it: lists as [a,b], strings quoted, linked lists and trees serialized.</summary>
    public static string Format(object? value)
    {
        var text = new StringBuilder();
        Write(text, value, 0);
        return text.ToString();
    }

    private static void Write(StringBuilder text, object? value, int depth)
    {
        if (depth > 64)
        {
            text.Append("…");
            return;
        }

        switch (value)
        {
            case null:
                text.Append("null");
                return;
            case string s:
                text.Append(JsonSerializer.Serialize(s, TextOptions));
                return;
            case char c:
                text.Append(JsonSerializer.Serialize(c.ToString(), TextOptions));
                return;
            case bool b:
                text.Append(b ? "true" : "false");
                return;
            case double or float or decimal:
                text.Append(Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture));
                return;
            case IFormattable number when value.GetType().IsPrimitive:
                text.Append(number.ToString(null, CultureInfo.InvariantCulture));
                return;
            case IDictionary map:
                text.Append('{');
                bool firstEntry = true;
                foreach (DictionaryEntry entry in map)
                {
                    if (!firstEntry) text.Append(',');
                    firstEntry = false;
                    text.Append(JsonSerializer.Serialize(entry.Key.ToString(), TextOptions)).Append(':');
                    Write(text, entry.Value, depth + 1);
                }
                text.Append('}');
                return;
            case ITuple tuple:
                text.Append('[');
                for (int i = 0; i < tuple.Length; i++)
                {
                    if (i > 0) text.Append(',');
                    Write(text, tuple[i], depth + 1);
                }
                text.Append(']');
                return;
            case IEnumerable items:
                text.Append('[');
                bool firstItem = true;
                foreach (var item in items)
                {
                    if (!firstItem) text.Append(',');
                    firstItem = false;
                    Write(text, item, depth + 1);
                }
                text.Append(']');
                return;
        }

        switch (DataStructureDetector.Detect(value))
        {
            case DataStructureShape.LinkedList:
                WriteList(text, value, depth);
                return;
            case DataStructureShape.Tree:
                WriteTree(text, value, depth);
                return;
        }

        if (VisualizerReflectionHelper.GetMemberValue(value, "neighbors", "Neighbors") is IEnumerable)
        {
            WriteGraph(text, value, depth);
            return;
        }

        text.Append(value.ToString());
    }

    private static object? NodeValue(object node) =>
        VisualizerReflectionHelper.GetMemberValue(node, "val", "Val", "value", "Value", "data", "Data", "key", "Key");

    // 1 -> 2 -> 3 prints as [1,2,3]; a cycle stops at the first repeat.
    private static void WriteList(StringBuilder text, object head, int depth)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        text.Append('[');
        object? node = head;
        while (node != null && seen.Add(node) && seen.Count <= MaxListNodes)
        {
            if (seen.Count > 1) text.Append(',');
            Write(text, NodeValue(node), depth + 1);
            node = VisualizerReflectionHelper.GetMemberValue(node, "next", "Next");
        }
        text.Append(']');
    }

    // Level order with nulls for missing children, trailing nulls trimmed: [3,9,20,null,null,15,7].
    private static void WriteTree(StringBuilder text, object root, int depth)
    {
        var order = new List<object?>();
        var queue = new Queue<object?>();
        queue.Enqueue(root);
        while (queue.Count > 0 && order.Count < MaxTreeNodes)
        {
            var node = queue.Dequeue();
            order.Add(node);
            if (node == null) continue;
            queue.Enqueue(VisualizerReflectionHelper.GetMemberValue(node, "left", "Left"));
            queue.Enqueue(VisualizerReflectionHelper.GetMemberValue(node, "right", "Right"));
        }

        int last = order.Count - 1;
        while (last >= 0 && order[last] == null) last--;

        text.Append('[');
        for (int i = 0; i <= last; i++)
        {
            if (i > 0) text.Append(',');
            Write(text, order[i] == null ? null : NodeValue(order[i]!), depth + 1);
        }
        text.Append(']');
    }

    // The adjacency list LeetCode uses for Clone Graph: neighbours of node 1, node 2, … by value.
    private static void WriteGraph(StringBuilder text, object start, int depth)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance) { start };
        var queue = new Queue<object>();
        queue.Enqueue(start);
        var nodes = new List<object>();
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            nodes.Add(node);
            foreach (var next in (IEnumerable)VisualizerReflectionHelper.GetMemberValue(node, "neighbors", "Neighbors")!)
            {
                if (next != null && seen.Add(next)) queue.Enqueue(next);
            }
        }

        text.Append('[');
        bool first = true;
        foreach (var node in nodes.OrderBy(n => Convert.ToDouble(NodeValue(n) ?? 0, CultureInfo.InvariantCulture)))
        {
            if (!first) text.Append(',');
            first = false;
            var neighbours = ((IEnumerable)VisualizerReflectionHelper.GetMemberValue(node, "neighbors", "Neighbors")!).Cast<object?>()
                .Where(n => n != null).Select(n => NodeValue(n!));
            Write(text, neighbours.ToList(), depth + 1);
        }
        text.Append(']');
    }

    /// <summary>Parses LeetCode/JSON-looking text so spacing, 2 vs 2.0 and (with anyOrder) element order don't matter.</summary>
    internal static string Canonical(string text, bool anyOrder)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            return Normalize(document.RootElement, anyOrder);
        }
        catch (JsonException)
        {
            return text.Trim();
        }
    }

    private static string Normalize(JsonElement element, bool anyOrder)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                var items = element.EnumerateArray().Select(e => Normalize(e, anyOrder)).ToList();
                if (anyOrder) items = Sort(items);
                return "[" + string.Join(",", items) + "]";
            case JsonValueKind.Object:
                return "{" + string.Join(",", element.EnumerateObject()
                    .Select(p => JsonSerializer.Serialize(p.Name, TextOptions) + ":" + Normalize(p.Value, anyOrder))
                    .OrderBy(p => p, StringComparer.Ordinal)) + "}";
            case JsonValueKind.Number:
                return element.GetDouble().ToString("R", CultureInfo.InvariantCulture);
            case JsonValueKind.String:
                return JsonSerializer.Serialize(element.GetString(), TextOptions);
            case JsonValueKind.True:
                return "true";
            case JsonValueKind.False:
                return "false";
            case JsonValueKind.Null:
                return "null";
            default:
                return element.GetRawText();
        }
    }

    // Numbers sort numerically, everything else ordinally, so [10,9] and [9,10] normalize the same way.
    private static List<string> Sort(List<string> items)
    {
        bool numeric = items.All(i => double.TryParse(i, NumberStyles.Float, CultureInfo.InvariantCulture, out _));
        return numeric
            ? items.OrderBy(i => double.Parse(i, CultureInfo.InvariantCulture)).ToList()
            : items.OrderBy(i => i, StringComparer.Ordinal).ToList();
    }
}
