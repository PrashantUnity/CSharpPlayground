using System.Text;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;

public sealed partial class FSharpNotebookKernel
{
    private void ExtractDirectivesAndDeclarations(
        string code,
        out List<string> directives,
        out List<string> opens,
        out List<string> topLevelItems,
        out string body)
    {
        directives = new List<string>();
        opens = new List<string>();
        topLevelItems = new List<string>();
        var bodyLines = new List<string>();

        var lines = code.Split('\n');
        var currentBlock = new List<string>();
        bool isTopLevelBlock = false;

        for (int i = 0; i < lines.Length; i++)
        {
            var raw = lines[i];
            var line = raw.TrimEnd();
            var trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            // Directives: #r, #load, #I, #time
            if (trimmed.StartsWith("#r ", StringComparison.Ordinal) ||
                trimmed.StartsWith("#load ", StringComparison.Ordinal) ||
                trimmed.StartsWith("#I ", StringComparison.Ordinal))
            {
                directives.Add(trimmed);
                continue;
            }

            // Open statements
            if (trimmed.StartsWith("open ", StringComparison.Ordinal))
            {
                opens.Add(trimmed);
                continue;
            }

            int indent = line.Length - line.TrimStart().Length;

            if (indent == 0)
            {
                // Flush previous block
                if (currentBlock.Count > 0)
                {
                    if (isTopLevelBlock)
                        topLevelItems.Add(string.Join("\n", currentBlock));
                    else
                        bodyLines.AddRange(currentBlock);
                    currentBlock.Clear();
                }

                if (IsTopLevelDeclaration(trimmed))
                {
                    isTopLevelBlock = true;
                    currentBlock.Add(line);
                }
                else
                {
                    isTopLevelBlock = false;
                    currentBlock.Add(line);
                }
            }
            else
            {
                currentBlock.Add(line);
            }
        }

        if (currentBlock.Count > 0)
        {
            if (isTopLevelBlock)
                topLevelItems.Add(string.Join("\n", currentBlock));
            else
                bodyLines.AddRange(currentBlock);
        }

        body = string.Join("\n", bodyLines);
    }

    private static bool IsTopLevelDeclaration(string trimmed)
    {
        return trimmed.StartsWith("type ", StringComparison.Ordinal) ||
               trimmed.StartsWith("module ", StringComparison.Ordinal) ||
               trimmed.StartsWith("namespace ", StringComparison.Ordinal) ||
               trimmed.StartsWith("exception ", StringComparison.Ordinal);
    }

    private string BuildCellProgram(string outDir, string body, List<string> topLevelItems)
    {
        var sb = new StringBuilder();

        // 1. Directives
        var displayFsx = Path.Combine(outDir, "fry", "Display.fsx").Replace('\\', '/');
        sb.AppendLine($"#load \"{displayFsx}\"");

        foreach (var directive in _cumulativeDirectives)
        {
            sb.AppendLine(directive);
        }
        sb.AppendLine();

        // 2. Open statements
        foreach (var op in _cumulativeOpens)
        {
            sb.AppendLine(op);
        }
        sb.AppendLine("open Fry");
        sb.AppendLine();

        // 3. Cumulative top-level items
        foreach (var item in _cumulativeTopLevel.Values)
        {
            sb.AppendLine(item);
            sb.AppendLine();
        }

        // 4. This cell's top-level items
        foreach (var item in topLevelItems)
        {
            sb.AppendLine(item);
            sb.AppendLine();
        }

        // 5. Injected shared variables
        foreach (var (name, info) in _sharedVariables)
        {
            sb.AppendLine(BuildInjectedVariable(name, info.TypeName, info.JsonValue));
        }

        // 6. Cell body
        if (!string.IsNullOrWhiteSpace(body))
        {
            sb.AppendLine(body);
        }

        return sb.ToString();
    }

    private static string BuildInjectedVariable(string name, string typeName, string json)
    {
        var trimmed = json.Trim();
        if (trimmed == "true" || trimmed == "false")
            return $"let {name} : bool = {trimmed}";
        if (long.TryParse(trimmed, out var l))
            return $"let {name} : int64 = {l}L";
        if (double.TryParse(trimmed, System.Globalization.CultureInfo.InvariantCulture, out var d))
            return $"let {name} : float = {d.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        if (trimmed.StartsWith('"'))
            return $"let {name} : string = {trimmed}";

        // Complex types via JSON deserialization
        var escaped = json.Replace("\"", "\\\"");
        return $"let {name} = System.Text.Json.JsonDocument.Parse(\"{escaped}\").RootElement";
    }
}
