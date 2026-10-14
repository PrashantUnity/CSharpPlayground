using System.Text;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;

/// <summary>
/// The crates notebook cells have asked for with <c>%cargo add</c>, kept in one file so every Rust notebook build sees them.
/// A notebook cell's directive lines are blanked before the kernel gets the code, and the package manager is only told the
/// toolchain (not which notebook), so the list can't live in either place; like <c>pip install</c>, an added crate stays
/// added.
/// </summary>
public sealed class RustNotebookDependencies(string rustRoot)
{
    private readonly object _gate = new();

    public string FilePath => Path.Combine(rustRoot, "notebook", "dependencies.txt");

    public IReadOnlyList<RustCrate> Load()
    {
        lock (_gate) return Read();
    }

    /// <summary>Adds a crate, replacing an earlier line for the same name.</summary>
    public void Add(RustCrate crate)
    {
        lock (_gate)
        {
            var crates = Read().Where(c => c.Name != crate.Name).Append(crate).ToList();
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

            // Write beside it and move it in, so a build reading the list never sees half of it.
            var temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, string.Join('\n', crates.Select(c => c.ToManifestLine())) + "\n", new UTF8Encoding(false));
            File.Move(temporary, FilePath, overwrite: true);
        }
    }

    private List<RustCrate> Read()
    {
        var crates = new List<RustCrate>();
        try
        {
            if (!File.Exists(FilePath)) return crates;
            foreach (var line in File.ReadAllLines(FilePath))
            {
                if (RustDirectives.TryParseCrate(line, out var crate) && crate.Name != RustDirectives.DisplayCrateName) crates.Add(crate);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // An unreadable list is an empty one.
        }

        return crates;
    }
}
