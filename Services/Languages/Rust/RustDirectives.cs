using System.Text.RegularExpressions;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;

/// <summary>A crate a script depends on: its name and the right-hand side of its <c>[dependencies]</c> line.</summary>
/// <param name="Name">E.g. <c>serde</c>.</param>
/// <param name="TomlValue">A TOML value, e.g. <c>"1"</c> or <c>{ version = "1", features = ["derive"] }</c>.</param>
public sealed record RustCrate(string Name, string TomlValue)
{
    /// <summary>The line as it would appear under <c>[dependencies]</c>.</summary>
    public string ToManifestLine() => $"{Name} = {TomlValue}";
}

/// <summary>
/// The build settings a Rust script asks for in its comments: <c>// #crate: rand = "0.8"</c> (or <c>rand@0.8</c>, or just
/// <c>rand</c>), <c>// #edition: 2024</c> and <c>// #profile: release</c>. They are ordinary comments to the compiler.
/// </summary>
public sealed partial class RustDirectives
{
    public const string DefaultEdition = "2021";

    private static readonly string[] Editions = ["2015", "2018", "2021", "2024"];

    /// <summary>The name the display crate is added under; a script can't redefine it.</summary>
    public const string DisplayCrateName = "fry";

    public static readonly RustDirectives None = new();

    public IReadOnlyList<RustCrate> Crates { get; init; } = Array.Empty<RustCrate>();

    public string Edition { get; init; } = DefaultEdition;

    /// <summary>True when the script asked for an optimized build.</summary>
    public bool Release { get; init; }

    [GeneratedRegex(@"^\s*//\s*#crate:\s*(?<spec>\S.*?)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex CrateLineRegex();

    [GeneratedRegex(@"^\s*//\s*#edition:\s*(?<edition>\d{4})\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex EditionLineRegex();

    [GeneratedRegex(@"^\s*//\s*#profile:\s*(?<profile>dev|debug|release)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex ProfileLineRegex();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_\-]*$")]
    private static partial Regex CrateNameRegex();

    // A bare version requirement such as 0.8, ^1.2, >=1, <2, 1.*, ~3.1.4 or "1, <1.5" without its quotes.
    [GeneratedRegex(@"^[0-9^~<>=*][0-9A-Za-z.\-+*^~<>=, ]*$")]
    private static partial Regex BareVersionRegex();

    public static RustDirectives Parse(string? source)
    {
        if (string.IsNullOrEmpty(source)) return None;

        var crates = new Dictionary<string, RustCrate>(StringComparer.Ordinal);
        var order = new List<string>();
        var edition = DefaultEdition;
        var release = false;

        foreach (var raw in source.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.IndexOf("#", StringComparison.Ordinal) < 0) continue;

            if (TryParseCrateLine(line, out var crate))
            {
                if (crate.Name == DisplayCrateName) continue;
                if (crates.TryAdd(crate.Name, crate)) order.Add(crate.Name);
                else crates[crate.Name] = crate;
                continue;
            }

            var editionMatch = EditionLineRegex().Match(line);
            if (editionMatch.Success)
            {
                var value = editionMatch.Groups["edition"].Value;
                if (Array.IndexOf(Editions, value) >= 0) edition = value;
                continue;
            }

            var profileMatch = ProfileLineRegex().Match(line);
            if (profileMatch.Success)
            {
                release = profileMatch.Groups["profile"].Value.Equals("release", StringComparison.OrdinalIgnoreCase);
            }
        }

        if (order.Count == 0 && edition == DefaultEdition && !release) return None;
        return new RustDirectives
        {
            Crates = order.Select(name => crates[name]).ToArray(),
            Edition = edition,
            Release = release
        };
    }

    /// <summary>True for a <c>// #crate: …</c> comment line, reading the dependency it names.</summary>
    public static bool TryParseCrateLine(string line, out RustCrate crate)
    {
        var match = CrateLineRegex().Match(line);
        if (match.Success) return TryParseCrate(match.Groups["spec"].Value, out crate);
        crate = null!;
        return false;
    }

    /// <summary>
    /// Reads <c>name</c>, <c>name@1.2</c>, <c>name = "1.2"</c>, <c>name = 1.2</c> or <c>name = { version = "1", features = [...] }</c>.
    /// </summary>
    public static bool TryParseCrate(string spec, out RustCrate crate)
    {
        crate = null!;
        spec = spec.Trim();
        if (spec.Length == 0) return false;

        var equals = spec.IndexOf('=');
        if (equals >= 0)
        {
            var name = spec[..equals].Trim();
            var value = spec[(equals + 1)..].Trim();
            if (!CrateNameRegex().IsMatch(name) || value.Length == 0) return false;

            if (value[0] is '"' or '\'' or '{' or '[')
            {
                crate = new RustCrate(name, value);
                return true;
            }

            if (BareVersionRegex().IsMatch(value))
            {
                crate = new RustCrate(name, Quote(value));
                return true;
            }

            return false;
        }

        var at = spec.IndexOf('@');
        if (at >= 0)
        {
            var name = spec[..at].Trim();
            var version = spec[(at + 1)..].Trim();
            if (!CrateNameRegex().IsMatch(name) || version.Length == 0 || version.Any(char.IsWhiteSpace)) return false;
            crate = new RustCrate(name, Quote(version));
            return true;
        }

        if (!CrateNameRegex().IsMatch(spec)) return false;
        crate = new RustCrate(spec, "\"*\"");
        return true;
    }

    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
