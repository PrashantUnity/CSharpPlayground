using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;

public sealed partial class GoNotebookKernel
{
    private void ExtractImportsAndTopLevel(string code, out List<string> imports, out List<string> topLevelItems, out string body)
    {
        imports = new List<string>();
        topLevelItems = new List<string>();
        var bodyLines = new List<string>();

        // If the code already contains func main(), treat as a full program
        if (code.Contains("func main(") || code.Contains("func main ()"))
        {
            var codeLines = code.Split('\n');
            foreach (var raw in codeLines)
            {
                var trimmed = raw.Trim();
                if (trimmed.StartsWith("package ", StringComparison.Ordinal)) continue;
                bodyLines.Add(raw);
            }
            body = string.Join("\n", bodyLines);
            return;
        }

        var lines = code.Split('\n');
        var currentBlock = new List<string>();
        bool isTopLevelBlock = false;
        int braceDepth = 0;
        int parenDepth = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            var raw = lines[i];
            var line = raw.TrimEnd();
            var trimmed = line.Trim();

            if (trimmed.StartsWith("package ", StringComparison.Ordinal)) continue;

            // Handle import lines at top level
            if (trimmed.StartsWith("import ", StringComparison.Ordinal) || trimmed == "import (")
            {
                imports.Add(trimmed);
                if (trimmed.EndsWith('('))
                {
                    // Multi-line import block
                    i++;
                    while (i < lines.Length)
                    {
                        var impLine = lines[i].Trim();
                        imports.Add(impLine);
                        if (impLine.StartsWith(')')) break;
                        i++;
                    }
                }
                continue;
            }

            if (braceDepth == 0 && parenDepth == 0)
            {
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                if (IsTopLevelDeclaration(trimmed))
                {
                    isTopLevelBlock = true;
                    currentBlock.Add(line);
                }
                else
                {
                    isTopLevelBlock = false;
                    bodyLines.Add(line);
                }
            }
            else
            {
                if (isTopLevelBlock)
                    currentBlock.Add(line);
                else
                    bodyLines.Add(line);
            }

            foreach (var ch in trimmed)
            {
                if (ch == '{') braceDepth++;
                else if (ch == '}') braceDepth = Math.Max(0, braceDepth - 1);
                else if (ch == '(') parenDepth++;
                else if (ch == ')') parenDepth = Math.Max(0, parenDepth - 1);
            }

            if (braceDepth == 0 && parenDepth == 0 && isTopLevelBlock && currentBlock.Count > 0)
            {
                topLevelItems.Add(string.Join("\n", currentBlock));
                currentBlock.Clear();
                isTopLevelBlock = false;
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
        return trimmed.StartsWith("func ", StringComparison.Ordinal) ||
               trimmed.StartsWith("type ", StringComparison.Ordinal) ||
               trimmed.StartsWith("const ", StringComparison.Ordinal) ||
               trimmed.StartsWith("var ", StringComparison.Ordinal);
    }

    private string BuildCellProgram(string body, List<string> topLevelItems)
    {
        // If body already has func main(), build directly
        if (body.Contains("func main(") || body.Contains("func main ()"))
        {
            var sbFull = new StringBuilder();
            sbFull.AppendLine("package main");
            sbFull.AppendLine();
            sbFull.AppendLine(body);
            return sbFull.ToString();
        }

        var sb = new StringBuilder();
        sb.AppendLine("package main");
        sb.AppendLine();

        // Collect all code in this compilation unit to inspect package usage
        var codeInspect = new StringBuilder();
        foreach (var item in _cumulativeTopLevel.Values) codeInspect.AppendLine(item);
        foreach (var item in topLevelItems) codeInspect.AppendLine(item);
        foreach (var (name, info) in _sharedVariables) codeInspect.AppendLine(BuildInjectedVariable(name, info.TypeName, info.JsonValue));
        codeInspect.AppendLine(body);
        var fullCode = codeInspect.ToString();

        // Filter cumulative imports so only referenced packages (or blank/dot imports) are emitted,
        // preventing Go compiler errors ("imported and not used").
        var neededImports = new List<string>();
        foreach (var imp in _cumulativeImports)
        {
            var clean = imp.Trim();
            if (clean.StartsWith("import ", StringComparison.Ordinal))
            {
                clean = clean["import ".Length..].Trim();
            }
            if (clean == "(" || clean == ")") continue;

            if (IsImportUsed(clean, fullCode))
            {
                neededImports.Add(clean);
            }
        }

        if (neededImports.Count > 0)
        {
            sb.AppendLine("import (");
            foreach (var imp in neededImports)
            {
                sb.AppendLine($"\t{imp}");
            }
            sb.AppendLine(")");
            sb.AppendLine();
        }

        // Top level items
        foreach (var item in _cumulativeTopLevel.Values)
        {
            sb.AppendLine(item);
            sb.AppendLine();
        }
        foreach (var item in topLevelItems)
        {
            sb.AppendLine(item);
            sb.AppendLine();
        }

        // func main()
        sb.AppendLine("func main() {");

        // Injected variables
        foreach (var (name, info) in _sharedVariables)
        {
            sb.AppendLine(BuildInjectedVariable(name, info.TypeName, info.JsonValue));
        }

        if (!string.IsNullOrWhiteSpace(body))
        {
            sb.AppendLine(body);
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string BuildInjectedVariable(string name, string typeName, string json)
    {
        var trimmed = json.Trim();
        if (trimmed == "true" || trimmed == "false")
            return $"\tvar {name} bool = {trimmed}";
        if (long.TryParse(trimmed, out var l))
            return $"\tvar {name} int64 = {l}";
        if (double.TryParse(trimmed, System.Globalization.CultureInfo.InvariantCulture, out var d))
            return $"\tvar {name} float64 = {d.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        if (trimmed.StartsWith('"'))
            return $"\tvar {name} string = {trimmed}";

        // Complex types via json unmarshal
        var escaped = json.Replace("`", "` + \"`\" + `");
        return $"\tvar {name} interface{{}}\n\t_ = json.Unmarshal([]byte(`{escaped}`), &{name})";
    }

    private static bool IsImportUsed(string cleanImport, string fullCode)
    {
        if (string.IsNullOrWhiteSpace(cleanImport)) return false;

        // Strip trailing inline comments
        var commentIdx = cleanImport.IndexOf("//", StringComparison.Ordinal);
        if (commentIdx >= 0)
        {
            cleanImport = cleanImport[..commentIdx].Trim();
        }

        // Side-effect import: _ "pkg"
        if (cleanImport.StartsWith("_ ", StringComparison.Ordinal) || cleanImport.StartsWith("_\"", StringComparison.Ordinal))
        {
            return true;
        }

        // Dot import: . "pkg"
        if (cleanImport.StartsWith(". ", StringComparison.Ordinal) || cleanImport.StartsWith(".\"", StringComparison.Ordinal))
        {
            return true;
        }

        // Aliased import: alias "pkg/path"
        var parts = cleanImport.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && parts[1].StartsWith('"'))
        {
            var alias = parts[0];
            return Regex.IsMatch(fullCode, $@"\b{Regex.Escape(alias)}\.");
        }

        // Standard import: "pkg" or "path/to/pkg"
        var path = cleanImport.Trim('"', ' ', '\t', ';');
        var lastSlash = path.LastIndexOf('/');
        var pkg = lastSlash >= 0 ? path[(lastSlash + 1)..] : path;

        return Regex.IsMatch(fullCode, $@"\b{Regex.Escape(pkg)}\.");
    }
}
