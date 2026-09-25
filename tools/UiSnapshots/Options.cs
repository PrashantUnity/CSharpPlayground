using System.Globalization;
using PdfEditorApp.Plugins.CSharpEditor.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Tools.UiSnapshots;

/// <summary>The command line: a command, then problem numbers and <c>--name value</c> / <c>--flag</c> options in any order.</summary>
internal sealed class Options
{
    // Options that never take a value, so `visualizer --quiet 1 11` doesn't read "1" as --quiet's value.
    private static readonly HashSet<string> Flags = new(StringComparer.OrdinalIgnoreCase) { "light", "quiet", "run", "edit-notes", "empty", "templates", "list" };

    private readonly Dictionary<string, string?> _named = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _positional = new();

    public Options(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--", StringComparison.Ordinal))
            {
                string name = args[i][2..];
                bool takesValue = !Flags.Contains(name) && i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal);
                _named[name] = takesValue ? args[++i] : null;
            }
            else if (Command == null)
            {
                Command = args[i].ToLowerInvariant();
            }
            else
            {
                _positional.Add(args[i]);
            }
        }
    }

    public string? Command { get; }

    public bool Flag(string name) => _named.ContainsKey(name);

    public string? Value(string name) => _named.TryGetValue(name, out var value) ? value : null;

    public int Int(string name, int fallback) =>
        Value(name) is { } text ? ParseInt(text, $"--{name}") : fallback;

    /// <summary>A comma-separated option as its parts: <c>--sort Difficulty,Difficulty</c>.</summary>
    public IEnumerable<string> List(string name) =>
        (Value(name) ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>A comma-separated list of Blind 75 problem numbers: <c>--solved 1,3,5</c>.</summary>
    public IEnumerable<int> Numbers(string name) => List(name).Select(n => CheckedProblem(ParseInt(n, $"--{name}")));

    /// <summary>The one problem number a command needs (<c>studio 1</c>).</summary>
    public int Problem() =>
        _positional.Count > 0 ? CheckedProblem(ParseInt(_positional[0], "the problem number")) : throw new ArgumentException($"`{Command}` needs a Blind 75 problem number, e.g. `{Command} 1`.");

    /// <summary>Every problem number given, or all of them when none are.</summary>
    public IReadOnlyList<int> Problems() =>
        _positional.Count > 0
            ? _positional.Select(p => CheckedProblem(ParseInt(p, "a problem number"))).ToList()
            : Blind75CatalogService.GetAllProblems().Select(p => p.Number).ToList();

    private static int ParseInt(string text, string what) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : throw new ArgumentException($"{what} should be a whole number, not '{text}'.");

    private static int CheckedProblem(int number) =>
        Blind75CatalogService.GetProblemByNumber(number) != null ? number : throw new ArgumentException($"There is no Blind 75 problem #{number}.");
}
