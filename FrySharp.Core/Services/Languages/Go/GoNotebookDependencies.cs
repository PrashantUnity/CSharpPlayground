using System.Text;
using System.Text.RegularExpressions;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;

/// <summary>
/// The modules notebook cells have asked for with <c>%go get</c>, kept as a module of their own (go.mod and go.sum, which <c>go get</c> writes) so
/// every Go notebook build sees them. A cell's directive lines are blanked before the kernel gets the code, and the package manager is only told
/// the toolchain (not which notebook), so the list can't live in either place; like <c>pip install</c>, an added module stays added.
/// </summary>
public sealed partial class GoNotebookDependencies(string goRoot)
{
    // "github.com/google/uuid v1.6.0" and "golang.org/x/text v0.3.0 // indirect", inside require ( ) or after "require"
    [GeneratedRegex(@"^\s*(?:require\s+)?(?<path>[^\s()/][^\s()]*)\s+(?<version>v[0-9][^\s]*)(?<rest>\s*//.*)?$")]
    private static partial Regex Requirement();

    public string Directory => Path.Combine(goRoot, "notebook", "deps");

    public string GoModPath => Path.Combine(Directory, "go.mod");

    public string GoSumPath => Path.Combine(Directory, "go.sum");

    /// <summary>The requirements <c>go get</c> recorded, each as <c>path version</c> (with <c>// indirect</c> when it has it).</summary>
    public IReadOnlyList<string> Requirements()
    {
        var found = new List<string>();
        try
        {
            if (!File.Exists(GoModPath)) return found;
            foreach (var line in File.ReadAllLines(GoModPath))
            {
                if (line.TrimStart().StartsWith("module", StringComparison.Ordinal) || line.TrimStart().StartsWith("go ", StringComparison.Ordinal)) continue;
                if (Requirement().Match(line) is { Success: true } match)
                {
                    found.Add($"{match.Groups["path"].Value} {match.Groups["version"].Value}{match.Groups["rest"].Value}");
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // An unreadable list is an empty one.
        }

        return found;
    }

    public string? GoSum()
    {
        try
        {
            return File.Exists(GoSumPath) ? File.ReadAllText(GoSumPath) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The module the downloads are made in, made if it isn't there (a go.mod for the toolchain's language version, and a file to be a package).</summary>
    public void EnsureModule(Version goVersion)
    {
        System.IO.Directory.CreateDirectory(Directory);
        if (!File.Exists(GoModPath)) File.WriteAllText(GoModPath, $"module fry_deps\n\ngo {goVersion.Major}.{goVersion.Minor}\n", new UTF8Encoding(false));
        var source = Path.Combine(Directory, "deps.go");
        if (!File.Exists(source)) File.WriteAllText(source, "package deps\n", new UTF8Encoding(false));
    }
}
