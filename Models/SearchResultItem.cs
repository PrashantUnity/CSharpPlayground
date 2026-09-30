namespace PdfEditorApp.Plugins.CSharpEditor.Models;

/// <summary>
/// A row of the Search panel's list. Searching the open document lists only matches; searching the workspace lists a header
/// row for each file (<see cref="IsFileHeader"/>) followed by that file's matches.
/// </summary>
public class SearchResultItem
{
    public int LineNumber { get; set; }
    public int Column { get; set; }
    public int Length { get; set; }
    public string LineText { get; set; } = string.Empty;

    /// <summary>The file, as a path from the workspace root; null for a match in the open document.</summary>
    public string? FilePath { get; set; }

    /// <summary>The 1-based notebook cell the match is in; 0 for anything that is not a notebook.</summary>
    public int Cell { get; set; }

    /// <summary>True for the row that names a file (which then carries <see cref="MatchCount"/> and the folder in <see cref="LineText"/>).</summary>
    public bool IsFileHeader { get; set; }

    public int MatchCount { get; set; }
    public string FileName { get; set; } = string.Empty;

    public string FormattedLocation => Cell > 0 ? $"Cell {Cell}, Ln {LineNumber}" : $"Ln {LineNumber}, Col {Column}";
    public string MatchCountText => MatchCount == 1 ? "1 match" : $"{MatchCount} matches";
}
