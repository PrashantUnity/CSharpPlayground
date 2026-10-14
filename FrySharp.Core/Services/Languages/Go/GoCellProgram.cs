using System.Text;
using System.Text.RegularExpressions;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;

/// <summary>A declaration an earlier notebook cell made (a function, a method, a type, a constant, a variable), kept so later cells can use it.</summary>
internal sealed record GoItem(string Kind, IReadOnlyList<string> Keys, IReadOnlyList<string> Names, string Text, string Masked)
{
    public static GoItem From(GoChunk chunk) => new(chunk.ItemKind!, chunk.Keys, chunk.Names, chunk.Text, chunk.Masked);
}

/// <summary>A variable a cell made and the notebook kept: its type as Go writes it, and its value as JSON.</summary>
internal sealed record GoVariable(string Name, string Type, string Json);

/// <summary>
/// The declarations the notebook's cells have made, in order. A cell that declares a name again takes the place of the older
/// declaration of it (a function written again, a type with another field).
/// </summary>
internal sealed class GoItemStore
{
    private readonly List<GoItem> _items = [];

    public IReadOnlyList<GoItem> Items => _items;

    public void Clear() => _items.Clear();

    public void Apply(IEnumerable<GoChunk> cellItems)
    {
        foreach (var chunk in cellItems)
        {
            var item = GoItem.From(chunk);
            if (item.Kind == "func" && item.Names.Contains("main")) continue; // a cell that is a whole program isn't kept
            _items.RemoveAll(existing => existing.Keys.Any(item.Keys.Contains));
            _items.Add(item);
        }
    }
}

/// <summary>A cell made into a Go program, and the way back from the program's lines to the cell's.</summary>
internal sealed class GoCellProgram
{
    public required string Source { get; init; }

    /// <summary>For each line of the program (index 0 is line 1): the cell's line it is, or 0 for a line the cell didn't write.</summary>
    public required IReadOnlyList<int> CellLines { get; init; }

    /// <summary>The program's lines that give a variable back its value, and the variable each one gives.</summary>
    public IReadOnlyDictionary<int, string> RestoreLines { get; init; } = new Dictionary<int, string>();

    /// <summary>The cell's lines of the last statement when the program shows its value, else null.</summary>
    public (int First, int Last)? ShownStatement { get; init; }

    /// <summary>Why the cell can't be made a program (it has its own <c>func main</c> and statements too); null when it can.</summary>
    public string? Error { get; init; }

    public int? CellLineOf(int generatedLine) => generatedLine >= 1 && generatedLine <= CellLines.Count && CellLines[generatedLine - 1] > 0 ? CellLines[generatedLine - 1] : null;
}

/// <summary>
/// Lays a cell out as a Go program: the imports it needs, every declaration the notebook has so far, then <c>func main</c>, which gives the
/// variables of earlier cells back their values, runs the cell's statements (in a block of their own, so <c>x := 5</c> may make a variable
/// that an earlier cell made too) and hands the variables on to the next cell. The statements go in unchanged, so a compiler message names the
/// line and column the cell has. The last expression, if the cell ends with one, is shown, as a notebook does.
/// </summary>
internal static partial class GoCellProgramBuilder
{
    [GeneratedRegex(@"\bmain\.")]
    private static partial Regex MainQualifier();

    private sealed record Line(string Text, int CellLine, string? Restores = null);

    /// <param name="imports">Every import the notebook has been given, plus the ones a cell may use without writing them: each is written only if the program uses it.</param>
    /// <param name="restore">The variables earlier cells made, to give back.</param>
    /// <param name="showLast">Show the value of the last statement when it is an expression.</param>
    public static GoCellProgram Build(GoCell cell, IReadOnlyList<GoItem> earlierItems, IReadOnlyList<string> imports, IReadOnlyList<GoVariable> restore, bool showLast)
    {
        var cellKeys = cell.Items.SelectMany(i => i.Keys).ToHashSet(StringComparer.Ordinal);
        var kept = earlierItems.Where(i => !i.Keys.Any(cellKeys.Contains)).ToList();
        var body = new List<Line>();
        var maskedCode = new StringBuilder();

        void Items()
        {
            foreach (var item in kept)
            {
                foreach (var text in item.Text.Split('\n')) body.Add(new Line(text, 0));
                body.Add(new Line(string.Empty, 0));
                maskedCode.AppendLine(item.Masked);
            }

            foreach (var item in cell.Items)
            {
                AddChunk(body, item);
                body.Add(new Line(string.Empty, 0));
                maskedCode.AppendLine(item.Masked);
            }
        }

        Items();
        (int, int)? shown = null;

        if (cell.DefinesMain)
        {
            if (cell.Statements.Count > 0)
            {
                return new GoCellProgram { Source = string.Empty, CellLines = [], Error = "A cell that defines func main is a whole program: it can't also have statements outside the functions." };
            }
        }
        else
        {
            var packageVars = kept.Concat(cell.Items.Select(GoItem.From)).Where(i => i.Kind == "var").SelectMany(i => i.Names).ToHashSet(StringComparer.Ordinal);
            var declaredHere = cell.Statements.SelectMany(s => s.Names).ToHashSet(StringComparer.Ordinal);
            var varItemsHere = cell.Items.Where(i => i.ItemKind == "var").SelectMany(i => i.Names).ToHashSet(StringComparer.Ordinal);

            body.Add(new Line("func main() {", 0));
            var restored = new List<string>();
            foreach (var variable in restore.Where(v => !varItemsHere.Contains(v.Name)))
            {
                if (packageVars.Contains(variable.Name))
                {
                    body.Add(new Line($"\tfry.RestoreInto(&{variable.Name}, {Quote(variable.Name)})", 0, variable.Name));
                }
                else
                {
                    body.Add(new Line($"\t{variable.Name} := fry.Restore[{MainQualifier().Replace(variable.Type, string.Empty)}]({Quote(variable.Name)})", 0, variable.Name));
                    body.Add(new Line($"\t_ = {variable.Name}", 0, variable.Name));
                    restored.Add(variable.Name);
                }
            }

            body.Add(new Line("\t{", 0));
            var last = cell.Statements.Count > 0 ? cell.Statements[^1] : null;
            foreach (var statement in cell.Statements)
            {
                maskedCode.AppendLine(statement.Masked);
                if (showLast && ReferenceEquals(statement, last) && GoCellSplitter.IsValueExpression(statement))
                {
                    body.Add(new Line("\tfry.ShowAll(", 0));
                    var texts = statement.Text.Split('\n');
                    var masks = statement.Masked.Split('\n');
                    for (var i = 0; i < texts.Length; i++)
                    {
                        var text = texts[i];
                        if (i == texts.Length - 1)
                        {
                            var end = GoCellSplitter.CodeEnd(masks[i]);
                            text = text[..end] + "," + text[end..];
                        }

                        body.Add(new Line(text, statement.StartLine + i));
                    }

                    body.Add(new Line("\t)", 0));
                    shown = (statement.StartLine, statement.StartLine + statement.LineCount - 1);
                }
                else
                {
                    AddChunk(body, statement);
                }
            }

            // What the cell made with := goes on to the next cell, from inside the block, where those variables are.
            var persisted = new HashSet<string>(StringComparer.Ordinal);
            foreach (var name in declaredHere)
            {
                if (persisted.Add(name)) body.Add(new Line($"\t\tfry.Persist({Quote(name)}, {name})", 0));
            }

            body.Add(new Line("\t}", 0));

            // The ones it didn't make again (it may have changed them) and the variables declared at the top of the program.
            foreach (var name in restored.Where(r => !declaredHere.Contains(r)).Concat(packageVars))
            {
                if (persisted.Add(name)) body.Add(new Line($"\tfry.Persist({Quote(name)}, {name})", 0));
            }

            body.Add(new Line("}", 0));
        }

        var generated = string.Join('\n', body.Where(l => l.CellLine == 0).Select(l => l.Text));
        var header = new List<string> { "package main", string.Empty };
        var needed = Imports(imports, maskedCode.ToString() + "\n" + generated);
        if (needed.Count > 0)
        {
            header.Add("import (");
            header.AddRange(needed.Select(i => "\t" + i));
            header.Add(")");
            header.Add(string.Empty);
        }

        var source = new StringBuilder();
        var cellLines = new List<int>();
        var restoreLines = new Dictionary<int, string>();
        foreach (var text in header)
        {
            source.Append(text).Append('\n');
            cellLines.Add(0);
        }

        foreach (var line in body)
        {
            source.Append(line.Text).Append('\n');
            cellLines.Add(line.CellLine);
            if (line.Restores != null) restoreLines[cellLines.Count] = line.Restores;
        }

        return new GoCellProgram { Source = source.ToString(), CellLines = cellLines, RestoreLines = restoreLines, ShownStatement = shown };
    }

    private static void AddChunk(List<Line> body, GoChunk chunk)
    {
        var texts = chunk.Text.Split('\n');
        for (var i = 0; i < texts.Length; i++) body.Add(new Line(texts[i], chunk.StartLine + i));
    }

    private static string Quote(string name) => "\"" + name + "\"";

    // Only the imports the program uses: Go refuses an import it doesn't use.
    private static List<string> Imports(IReadOnlyList<string> candidates, string maskedProgram)
    {
        var needed = new List<string>();
        foreach (var import in candidates.Distinct(StringComparer.Ordinal))
        {
            var (alias, path) = Parse(import);
            if (alias is "_" or ".") { needed.Add(import); continue; }

            var name = alias ?? PackageName(path);
            if (Regex.IsMatch(maskedProgram, @"(?<![\w.])" + Regex.Escape(name) + @"\s*\.")) needed.Add(import);
        }

        return needed;
    }

    private static (string? Alias, string Path) Parse(string import)
    {
        var quote = import.IndexOf('"');
        var path = import[(quote + 1)..].TrimEnd('"');
        var alias = import[..quote].Trim();
        return (alias.Length == 0 ? null : alias, path);
    }

    // The name a package is used by: the last part of its path, not counting a version ("gopkg.in/yaml.v3", ".../v2").
    internal static string PackageName(string path)
    {
        var parts = path.Split('/');
        var name = parts[^1];
        if (parts.Length > 1 && Regex.IsMatch(name, @"^v\d+$")) name = parts[^2];
        name = Regex.Replace(name, @"\.v\d+$", string.Empty);
        if (name.StartsWith("go-", StringComparison.Ordinal)) name = name[3..];
        if (name.EndsWith("-go", StringComparison.Ordinal)) name = name[..^3];
        return name.Replace('-', '_').Replace('.', '_');
    }
}
