using System.Text;
using System.Text.RegularExpressions;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;

/// <summary>A definition an earlier notebook cell made, kept so later cells can use it.</summary>
internal sealed record RustStoredItem(string Key, string Kind, string? Name, string Text)
{
    /// <summary>The text with its whitespace made single spaces: two definitions with the same one are the same definition.</summary>
    public string Collapsed { get; } = Collapse(Text);

    internal static string Collapse(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}

/// <summary>
/// The items (functions, types, imports…) the notebook's cells have defined, in the order they were. A cell that defines a name
/// again replaces the old definition where it stood, and one that changes a type drops the older <c>impl</c> blocks that name
/// it, which no longer describe it: run the cell that defines the type together with its <c>impl</c> blocks.
/// </summary>
internal sealed class RustItemStore
{
    private readonly List<RustStoredItem> _items = [];

    public IReadOnlyList<RustStoredItem> Items => _items;

    public RustItemStore Clone()
    {
        var copy = new RustItemStore();
        copy._items.AddRange(_items);
        return copy;
    }

    public void Clear() => _items.Clear();

    /// <summary>Takes a cell's items in; <c>fn main</c> is never kept.</summary>
    public void Apply(IEnumerable<RustCellChunk> cellItems)
    {
        var incoming = cellItems
            .Where(i => !(i.ItemKind == "fn" && i.Name == "main"))
            .Select(i => new RustStoredItem(i.Key, i.ItemKind!, i.Name, i.Text))
            .ToList();

        // A type that changed makes the impls written for its old shape stale: drop them before the cell's own come in.
        foreach (var changed in incoming.Where(i => i.Key.StartsWith("type:", StringComparison.Ordinal)))
        {
            var existing = _items.FirstOrDefault(e => e.Key == changed.Key);
            if (existing == null || existing.Collapsed == changed.Collapsed) continue;
            var name = new Regex(@"\b" + Regex.Escape(changed.Name ?? string.Empty) + @"\b");
            _items.RemoveAll(e => e.Kind == "impl" && name.IsMatch(Header(e.Text)));
        }

        foreach (var item in incoming)
        {
            var at = _items.FindIndex(e => e.Key == item.Key);
            if (at >= 0) _items[at] = item;
            else _items.Add(item);
        }
    }

    private static string Header(string text)
    {
        var brace = text.IndexOf('{');
        return brace < 0 ? text : text[..brace];
    }
}

/// <summary>Which lines of a cell's program are which lines of the cell: a run of lines starting at a program line and a cell line.</summary>
internal readonly record struct RustLineSegment(int GeneratedStartLine, int LineCount, int CellStartLine);

/// <summary>A notebook cell made into a program, and the way back from the program's lines to the cell's.</summary>
internal sealed class RustCellProgram
{
    public required string Source { get; init; }

    public required IReadOnlyList<RustLineSegment> Segments { get; init; }

    /// <summary>Why the cell can't be made a program (it defines <c>fn main</c> and also has statements); null when it can.</summary>
    public string? Error { get; init; }

    /// <summary>The cell's own line for a line of the program, or null for a line that isn't the cell's: the scaffolding, or an earlier cell's item.</summary>
    public int? CellLineOf(int generatedLine)
    {
        foreach (var segment in Segments)
        {
            if (generatedLine >= segment.GeneratedStartLine && generatedLine < segment.GeneratedStartLine + segment.LineCount)
            {
                return segment.CellStartLine + (generatedLine - segment.GeneratedStartLine);
            }
        }

        return null;
    }
}

/// <summary>
/// Lays a cell out as a Rust program: the items every cell so far defined, then this cell's, then <c>fn main</c> with the cell's
/// statements in it. What the cell wrote goes in unchanged (padded to its own column), so a compiler message names the line
/// and column the cell has. The cell's last expression, if it has no semicolon, is shown, as a notebook does.
/// </summary>
internal static class RustCellProgramBuilder
{
    // Every cell defines things it may never use; warning about them is noise in a notebook.
    public const string Header = "#![allow(unused)]";

    // Written out in full so a cell that imports its own Result or Ok doesn't change what this means.
    private const string MainSignature = "fn main() -> ::std::result::Result<(), ::std::boxed::Box<dyn ::std::error::Error>> {";
    private const string MainResult = "    ::std::result::Result::Ok(())";

    public static RustCellProgram Build(RustCell cell, IReadOnlyList<RustStoredItem> earlierItems, IReadOnlyList<string> shareDeclarations)
    {
        var program = new Writer();
        program.Line(Header);

        foreach (var attribute in cell.InnerAttributes) program.User(attribute);

        program.Line("use fry::prelude::*;");
        program.Line(string.Empty);

        var cellItemKeys = cell.Items.Select(i => i.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var item in earlierItems.Where(i => !cellItemKeys.Contains(i.Key)))
        {
            program.Line(item.Text);
            program.Line(string.Empty);
        }

        foreach (var item in cell.Items)
        {
            program.User(item);
            program.Line(string.Empty);
        }

        var statements = cell.Statements.ToList();
        if (cell.DefinesMain)
        {
            return statements.Count > 0
                ? new RustCellProgram { Source = program.ToString(), Segments = program.Segments, Error = "This cell defines fn main, so it can't also have statements outside a function. Put them inside main, or remove main." }
                : new RustCellProgram { Source = program.ToString(), Segments = program.Segments };
        }

        program.Line(MainSignature);
        foreach (var declaration in shareDeclarations) program.Line("    " + declaration);

        for (var i = 0; i < statements.Count; i++)
        {
            var statement = statements[i];
            var isLast = i == statements.Count - 1;
            if (isLast && statement.CanBeShownValue && !statement.StartsWithLet)
            {
                program.Line("    let __fry_last = {", statement.StartLine);
                program.User(statement);
                program.Line("    };", statement.StartLine + statement.Text.Count(c => c == '\n'));
                program.Line("    fry::__auto!(__fry_last);", statement.StartLine + statement.Text.Count(c => c == '\n'));
            }
            else
            {
                program.User(statement);

                // `let x = 5` without its semicolon, as a notebook allows, is still a statement.
                if (!statement.EndsWithSemicolon && statement.StartsWithLet) program.Line(";", statement.StartLine + statement.Text.Count(c => c == '\n'));
            }
        }

        program.Line(MainResult);
        program.Line("}");
        return new RustCellProgram { Source = program.ToString(), Segments = program.Segments };
    }

    // Line-counting output: it knows which line each thing it writes lands on.
    private sealed class Writer
    {
        private readonly StringBuilder _text = new();
        private readonly List<RustLineSegment> _segments = [];
        private int _line = 1;

        public IReadOnlyList<RustLineSegment> Segments => _segments;

        /// <summary>One line of the program's own; with <paramref name="cellLine"/> it stands for that line of the cell.</summary>
        public void Line(string text, int? cellLine = null)
        {
            if (cellLine is { } mapped) _segments.Add(new RustLineSegment(_line, 1, mapped));
            _text.Append(text).Append('\n');
            _line += text.Count(c => c == '\n') + 1;
        }

        /// <summary>A piece of the cell, at its own column, so its lines and columns are the cell's.</summary>
        public void User(RustCellChunk chunk)
        {
            var text = ToOrdinaryComments(chunk.Text);
            var lines = text.Count(c => c == '\n') + 1;
            _segments.Add(new RustLineSegment(_line, lines, chunk.StartLine));
            _text.Append(' ', chunk.StartColumn).Append(text).Append('\n');
            _line += lines;
        }

        public override string ToString() => _text.ToString();

        // //! and /*! document the enclosing module and are an error in the middle of a function; same length, so columns hold.
        private static string ToOrdinaryComments(string text) =>
            text.Contains("//!", StringComparison.Ordinal) || text.Contains("/*!", StringComparison.Ordinal)
                ? Regex.Replace(text, @"(?m)^(\s*)//!", "$1// ").Replace("/*!", "/* ")
                : text;
    }
}
