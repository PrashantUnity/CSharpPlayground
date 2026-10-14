using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;

/// <summary>One piece of what <c>sqlite3 -json</c> printed: a result set, or the text around it. <see cref="Start"/> and
/// <see cref="End"/> are character offsets in the output.</summary>
public abstract record SqlOutputSegment(int Start, int End);

/// <summary>Text a script printed (<c>.print</c>, a dot-command's answer): not a result set.</summary>
public sealed record SqlTextSegment(int Start, int End, string Text) : SqlOutputSegment(Start, End);

/// <summary>The rows one statement returned.</summary>
public sealed record SqlTableSegment(int Start, int End, DumpTableResult Table) : SqlOutputSegment(Start, End);

/// <summary>
/// Reads the output of <c>sqlite3 -json</c> into result sets and text. SQLite writes a statement's rows as one JSON array
/// with one row to a line (<c>[{...},\n{...}]</c>), a statement without rows as nothing, and everything else a script
/// prints as it is, so a line that starts a result set is told from text by being the start of an array that parses.
/// A script that changes the output mode (<c>.mode csv</c>) simply gets its text.
/// </summary>
public static class SqlResultStream
{
    public static IReadOnlyList<SqlOutputSegment> Split(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var segments = new List<SqlOutputSegment>();
        var lines = Lines(output);

        var i = 0;
        while (i < lines.Count)
        {
            var line = lines[i];
            if (StartsResultSet(line.Text) && TryReadResultSet(lines, i, out var table, out var last))
            {
                segments.Add(new SqlTableSegment(line.Start, lines[last].End, table));
                i = last + 1;
                continue;
            }

            segments.Add(new SqlTextSegment(line.Start, line.End, output[line.Start..line.End]));
            i++;
        }

        return segments;
    }

    /// <summary>The text of <paramref name="segments"/> only, as a console would show it.</summary>
    public static string TextOf(IEnumerable<SqlOutputSegment> segments) =>
        string.Concat(segments.OfType<SqlTextSegment>().Select(s => s.Text));

    private readonly record struct Line(int Start, int End, string Text);

    private static List<Line> Lines(string output)
    {
        var lines = new List<Line>();
        var start = 0;
        while (start < output.Length)
        {
            var newline = output.IndexOf('\n', start);
            var end = newline < 0 ? output.Length : newline + 1;
            lines.Add(new Line(start, end, output[start..end].TrimEnd('\r', '\n')));
            start = end;
        }

        return lines;
    }

    private static bool StartsResultSet(string line) => line.StartsWith("[{", StringComparison.Ordinal);

    // A result set is the lines from "[{" to the first line ending "}]": a row ends "}," and only the last one "}]".
    private static bool TryReadResultSet(List<Line> lines, int first, out DumpTableResult table, out int last)
    {
        table = null!;
        for (last = first; last < lines.Count; last++)
        {
            var text = lines[last].Text;
            if (last > first && StartsResultSet(text)) break;
            if (!text.EndsWith("}]", StringComparison.Ordinal)) continue;

            var json = string.Join("\n", lines.Skip(first).Take(last - first + 1).Select(l => l.Text));
            return TryBuild(json, out table);
        }

        last = first;
        return false;
    }

    private static bool TryBuild(string json, out DumpTableResult table)
    {
        table = null!;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0) return false;

            var firstRow = root[0];
            if (firstRow.ValueKind != JsonValueKind.Object) return false;

            // Columns by position, not by name: "select 1 as a, 2 as a" has two.
            var headers = firstRow.EnumerateObject().Select(p => p.Name).ToList();
            var cells = new List<List<DumpTableCell>>();
            var numeric = Enumerable.Repeat(true, headers.Count).ToArray();
            foreach (var row in root.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object) return false;
                var values = new List<DumpTableCell>(headers.Count);
                var column = 0;
                foreach (var property in row.EnumerateObject())
                {
                    if (column >= headers.Count) break;
                    if (property.Value.ValueKind is not (JsonValueKind.Number or JsonValueKind.Null)) numeric[column] = false;
                    values.Add(Cell(property.Value));
                    column++;
                }

                while (values.Count < headers.Count) values.Add(DumpTableBuilder.CreateCell(null));
                cells.Add(values);
            }

            var result = new DumpTableResult("Result");
            for (var c = 0; c < headers.Count; c++)
            {
                result.Columns.Add(new DumpTableColumn { Header = headers[c], IsNumeric = numeric[c] });
            }

            result.AddRows(cells.Select((values, index) => new DumpTableRow(index, values)).ToList());
            table = result;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static DumpTableCell Cell(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => DumpTableBuilder.CreateCell(null),
        JsonValueKind.True => DumpTableBuilder.CreateCell(true),
        JsonValueKind.False => DumpTableBuilder.CreateCell(false),
        JsonValueKind.Number when value.TryGetInt64(out var whole) => DumpTableBuilder.CreateCell(whole),
        JsonValueKind.Number => DumpTableBuilder.CreateCell(value.GetDouble()),
        JsonValueKind.String => DumpTableBuilder.CreateCell(value.GetString()),
        _ => DumpTableBuilder.CreateCell(value.GetRawText())
    };
}
