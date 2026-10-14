using System.Text;
using System.Text.RegularExpressions;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;

/// <summary>A declaration an earlier notebook cell made (a function, a type, a using, a constant, a macro), kept so later cells can use it.</summary>
internal sealed record CppItem(string Kind, IReadOnlyList<string> Keys, IReadOnlyList<string> Names, string Text, string Masked, IReadOnlyList<string> Members)
{
    public static CppItem From(CppChunk chunk) => new(chunk.ItemKind!, chunk.Keys, chunk.Names, chunk.Text, chunk.Masked, chunk.Members);
}

/// <summary>A variable a cell made and the notebook kept: its type as C++ writes it, and its value as JSON.</summary>
internal sealed record CppVariable(string Name, string Type, string Json);

/// <summary>The declarations the notebook's cells have made, in order. A cell that declares a name again takes the place of the older one.</summary>
internal sealed class CppItemStore
{
    private readonly List<CppItem> _items = [];

    public IReadOnlyList<CppItem> Items => _items;

    public void Clear() => _items.Clear();

    public void Apply(IEnumerable<CppChunk> cellItems)
    {
        foreach (var chunk in cellItems)
        {
            var item = CppItem.From(chunk);
            if (item.Kind == "function" && item.Names.Contains("main")) continue; // a cell that is a whole program isn't kept
            _items.RemoveAll(existing => existing.Keys.Any(item.Keys.Contains));
            _items.Add(item);
        }
    }
}

/// <summary>A cell made into a C++ program, and the way back from the program's lines to the cell's.</summary>
internal sealed class CppCellProgram
{
    public required string Source { get; init; }

    /// <summary>For each line of the program (index 0 is line 1): the cell's line it is, or 0 for a line the cell didn't write.</summary>
    public required IReadOnlyList<int> CellLines { get; init; }

    /// <summary>The program's lines that give a variable back its value, and the variable each one gives.</summary>
    public IReadOnlyDictionary<int, string> RestoreLines { get; init; } = new Dictionary<int, string>();

    /// <summary>The program's lines of the last statement, with the <c>fry::show(</c> around it, when the program shows its value, else null.</summary>
    public (int First, int Last)? ShownGenerated { get; init; }

    /// <summary>Why the cell can't be made a program (it has its own <c>main</c> and statements too); null when it can.</summary>
    public string? Error { get; init; }

    public int? CellLineOf(int generatedLine) => generatedLine >= 1 && generatedLine <= CellLines.Count && CellLines[generatedLine - 1] > 0 ? CellLines[generatedLine - 1] : null;
}

/// <summary>
/// Lays a cell out as a C++ program: the includes, every declaration the notebook has so far, how the notebook can show each struct (its public data
/// members), then <c>main</c>, which gives the variables of earlier cells back their values, runs the cell's statements (in a block of their own,
/// so <c>int x = 5;</c> may make a variable that an earlier cell made too) and hands the variables on to the next cell. The statements go in
/// unchanged, so a compiler message names the line and column the cell has. The last expression, when it has no <c>;</c>, is shown.
/// </summary>
internal static class CppCellProgramBuilder
{
    // What a cell may use without including it.
    public static readonly string[] DefaultIncludes =
    [
        "#include <iostream>", "#include <vector>", "#include <string>", "#include <memory>", "#include <algorithm>", "#include <map>", "#include <set>",
        "#include <cmath>", "#include <sstream>", "#include <optional>", "#include <functional>", "#include <numeric>"
    ];

    private sealed record Line(string Text, int CellLine, string? Restores = null);

    /// <param name="includes">The notebook's includes so far, in order.</param>
    /// <param name="restore">The variables earlier cells made, to give back.</param>
    /// <param name="showLast">Show the value of the last statement when it is an expression.</param>
    public static CppCellProgram Build(CppCell cell, IReadOnlyList<CppItem> earlierItems, IReadOnlyList<string> includes, IReadOnlyList<CppVariable> restore, bool showLast)
    {
        var cellKeys = cell.Items.SelectMany(i => i.Keys).ToHashSet(StringComparer.Ordinal);
        var kept = earlierItems.Where(i => !i.Keys.Any(cellKeys.Contains)).ToList();
        var all = kept.Concat(cell.Items.Select(CppItem.From)).ToList();
        var body = new List<Line>();

        foreach (var item in kept)
        {
            foreach (var text in item.Text.Split('\n')) body.Add(new Line(text, 0));
            body.Add(new Line(string.Empty, 0));
        }

        foreach (var item in cell.Items)
        {
            AddChunk(body, item);
            body.Add(new Line(string.Empty, 0));
        }

        // how the notebook shows each struct: its data members, by name
        foreach (var type in all.Where(i => i.Kind == "type" && i.Names.Count == 1 && i.Members.Count > 0))
        {
            body.Add(new Line(Registration(type.Names[0], type.Members), 0));
        }

        body.Add(new Line(string.Empty, 0));
        (int First, int Last)? shown = null;

        if (cell.DefinesMain)
        {
            if (cell.Statements.Count > 0)
            {
                return new CppCellProgram { Source = string.Empty, CellLines = [], Error = "A cell that defines main is a whole program: it can't also have statements outside the functions." };
            }
        }
        else
        {
            var globalVars = all.Where(i => i.Kind == "var" && !Regex.IsMatch(i.Masked, @"\b(?:const|constexpr|consteval|constinit|extern)\b")).SelectMany(i => i.Names).ToHashSet(StringComparer.Ordinal);
            var declaredHere = cell.Statements.SelectMany(s => s.Names).ToHashSet(StringComparer.Ordinal);
            var varItemsHere = cell.Items.Where(i => i.ItemKind == "var").SelectMany(i => i.Names).ToHashSet(StringComparer.Ordinal);

            body.Add(new Line("int main() {", 0));
            // output reaches the notebook as it is written (a pipe would hold it back), so what the cell prints and what it prints to
            // the error stream come in the order they ran
            body.Add(new Line("    std::cout << std::unitbuf; std::setvbuf(stdout, nullptr, _IONBF, 0);", 0));
            var restored = new List<string>();
            foreach (var variable in restore.Where(v => !varItemsHere.Contains(v.Name)))
            {
                if (globalVars.Contains(variable.Name))
                {
                    body.Add(new Line($"    fry::restore_into({variable.Name}, {Quote(variable.Name)});", 0, variable.Name));
                }
                else
                {
                    body.Add(new Line($"    [[maybe_unused]] {variable.Type} {variable.Name} = fry::restore<{variable.Type}>({Quote(variable.Name)});", 0, variable.Name));
                    restored.Add(variable.Name);
                }
            }

            body.Add(new Line("    {", 0));
            var last = cell.Statements.Count > 0 ? cell.Statements[^1] : null;
            foreach (var statement in cell.Statements)
            {
                var isLast = ReferenceEquals(statement, last);
                if (isLast && showLast && CppCellSplitter.IsValueExpression(statement))
                {
                    var firstLine = body.Count + 1;
                    body.Add(new Line("    fry::show(", 0));
                    AddChunk(body, statement);
                    body.Add(new Line("    );", 0));
                    shown = (firstLine, body.Count); // body lines: the header comes before them, so these are shifted below
                }
                else if (isLast && !statement.EndsWithSemicolon && statement.Masked.Trim().Length > 0)
                {
                    // an expression that isn't shown (a print, a call that returns nothing) still needs its ; (after a block it is an empty statement)
                    AddChunk(body, statement, closeWithSemicolon: true);
                }
                else
                {
                    AddChunk(body, statement);
                }
            }

            // What the cell made goes on to the next cell, from inside the block, where those variables are.
            var persisted = new HashSet<string>(StringComparer.Ordinal);
            foreach (var name in declaredHere)
            {
                if (persisted.Add(name)) body.Add(new Line($"        fry::persist({Quote(name)}, {name});", 0));
            }

            body.Add(new Line("    }", 0));

            // The ones it didn't make again (it may have changed them) and the statics declared at the top of the program.
            foreach (var name in restored.Where(r => !declaredHere.Contains(r)).Concat(globalVars))
            {
                if (persisted.Add(name)) body.Add(new Line($"    fry::persist({Quote(name)}, {name});", 0));
            }

            body.Add(new Line("    return 0;", 0));
            body.Add(new Line("}", 0));
        }

        var source = new StringBuilder();
        var cellLines = new List<int>();
        var restoreLines = new Dictionary<int, string>();
        var header = new List<string>();
        foreach (var include in includes.Concat(DefaultIncludes).Concat(["#include <fry/display.hpp>", "#include <fry/values.hpp>"]).Distinct(StringComparer.Ordinal)) header.Add(include);
        header.Add(string.Empty);
        header.Add("using namespace fry;");
        header.Add("using namespace fry::display;");
        header.Add(string.Empty);

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

        (int, int)? shownGenerated = shown is { } range ? (range.First + header.Count, range.Last + header.Count) : null;
        return new CppCellProgram { Source = source.ToString(), CellLines = cellLines, RestoreLines = restoreLines, ShownGenerated = shownGenerated };
    }

    private static void AddChunk(List<Line> body, CppChunk chunk, bool closeWithSemicolon = false)
    {
        var texts = chunk.Text.Split('\n');
        var masks = chunk.Masked.Split('\n');
        for (var i = 0; i < texts.Length; i++)
        {
            var text = texts[i];
            if (closeWithSemicolon && i == texts.Length - 1)
            {
                var end = CppCellSplitter.CodeEnd(masks[i]);
                text = text[..end] + ";" + text[end..];
            }

            body.Add(new Line(text, chunk.StartLine + i));
        }
    }

    // template <> struct fry::fields<Person> { ... }: how fry shows and keeps a struct, by naming its members
    private static string Registration(string type, IReadOnlyList<string> members)
    {
        var names = string.Join(", ", members.Select(m => "\"" + m + "\""));
        var calls = string.Join(" ", members.Select(m => $"f(value.{m});"));
        return $"namespace fry {{ template <> struct fields<::{type}> {{ static constexpr const char* names[] = {{{names}}}; " +
               $"template <class F> static void each(::{type}& value, F&& f) {{ {calls} }} " +
               $"template <class F> static void each(const ::{type}& value, F&& f) {{ {calls} }} }}; }}";
    }

    private static string Quote(string name) => "\"" + name + "\"";
}
