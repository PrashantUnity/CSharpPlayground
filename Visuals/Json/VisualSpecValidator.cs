namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;

/// <summary>A problem with a spec: where it is (a JSON path such as <c>$.series[0].x</c>) and what is wrong.</summary>
public sealed record VisualSpecIssue(string Path, string Message)
{
    public override string ToString() => $"{Path}: {Message}";
}

/// <summary>
/// Checks what JSON's shape alone can't: columns as long as each other, edges between nodes that exist, a grid that is
/// rectangular, a step that highlights elements its visualizer has. What it finds is reported with the path it was
/// found at; a spec that passes can be drawn.
/// </summary>
public static class VisualSpecValidator
{
    /// <summary>At most this many problems are reported; the rest usually follow from them.</summary>
    public const int MaxIssues = 20;

    public static IReadOnlyList<VisualSpecIssue> Validate(VisualSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var issues = new VisualSpecIssues();

        CheckSize(issues, spec);
        switch (spec)
        {
            case ChartSpec chart: ChartSpecRules.Check(issues, chart); break;
            case Plot3DSpec plot: Plot3DSpecRules.Check(issues, plot); break;
            case VisualizerSpec visualizer: VisualizerSpecRules.Check(issues, visualizer); break;
        }

        return issues.All;
    }

    /// <summary>Throws <see cref="VisualSpecException"/> listing the problems, if there are any.</summary>
    public static void EnsureValid(VisualSpec spec)
    {
        var issues = Validate(spec);
        if (issues.Count > 0)
        {
            throw new VisualSpecException(spec.Family, string.Join("; ", issues));
        }
    }

    private static void CheckSize(VisualSpecIssues issues, VisualSpec spec)
    {
        if (spec.Width is { } width && !(double.IsFinite(width) && width > 0)) issues.Add("$.width", "must be a positive number of pixels");
        if (spec.Height is { } height && !(double.IsFinite(height) && height > 0)) issues.Add("$.height", "must be a positive number of pixels");
    }
}

/// <summary>The problems found so far, up to <see cref="VisualSpecValidator.MaxIssues"/>.</summary>
internal sealed class VisualSpecIssues
{
    private readonly List<VisualSpecIssue> _issues = [];

    public IReadOnlyList<VisualSpecIssue> All => _issues;

    public bool IsFull => _issues.Count >= VisualSpecValidator.MaxIssues;

    public void Add(string path, string message)
    {
        if (!IsFull) _issues.Add(new VisualSpecIssue(path, message));
    }

    /// <summary>A column that must be as long as another one.</summary>
    public void SameLength<T>(string path, IReadOnlyCollection<T>? column, string otherName, int otherCount)
    {
        if (column != null && column.Count != otherCount)
        {
            Add(path, $"has {column.Count} values but {otherName} has {otherCount}; give one for each");
        }
    }

    /// <summary>Numbers must be finite: JSON has no NaN or infinity, so a missing value is null.</summary>
    public void Finite(string path, IReadOnlyList<double?>? values)
    {
        if (values == null) return;
        for (var i = 0; i < values.Count && !IsFull; i++)
        {
            if (values[i] is { } value && !double.IsFinite(value)) Add($"{path}[{i}]", "must be a finite number, or null for a missing value");
        }
    }

    public void Finite(string path, double? value)
    {
        if (value is { } number && !double.IsFinite(number)) Add(path, "must be a finite number");
    }

    /// <summary>Ids that must be present and unique; returns the set of them.</summary>
    public HashSet<string> UniqueIds<T>(string path, IReadOnlyList<T> items, Func<T, string?> id, string what)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < items.Count; i++)
        {
            var value = id(items[i]);
            if (string.IsNullOrEmpty(value)) Add($"{path}[{i}].id", $"every {what} needs an id");
            else if (!ids.Add(value)) Add($"{path}[{i}].id", $"\"{value}\" is used by another {what}; ids must be unique");
        }

        return ids;
    }
}
