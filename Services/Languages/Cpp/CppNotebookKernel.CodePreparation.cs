using System.Text;
using System.Text.Json;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;

public sealed partial class CppNotebookKernel
{
    private void ExtractIncludesAndTopLevel(string code, out List<string> includes, out List<string> topLevelItems, out string body)
    {
        includes = new List<string>();
        topLevelItems = new List<string>();
        var bodyLines = new List<string>();

        if (code.Contains("int main(") || code.Contains("int main ()") || code.Contains("void main("))
        {
            var codeLines = code.Split('\n');
            foreach (var raw in codeLines)
            {
                var trimmed = raw.Trim();
                if (trimmed.StartsWith("#include", StringComparison.Ordinal))
                    includes.Add(trimmed);
                else
                    bodyLines.Add(raw);
            }
            body = string.Join("\n", bodyLines);
            return;
        }

        var lines = code.Split('\n');
        var currentBlock = new List<string>();
        bool isTopLevelBlock = false;
        int braceDepth = 0;

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            var trimmed = line.Trim();

            if (trimmed.StartsWith("#include", StringComparison.Ordinal) && braceDepth == 0)
            {
                includes.Add(trimmed);
                continue;
            }

            if (braceDepth == 0)
            {
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }

                if (IsStatement(trimmed))
                {
                    isTopLevelBlock = false;
                    bodyLines.Add(line);
                }
                else
                {
                    isTopLevelBlock = true;
                    currentBlock.Add(line);
                }
            }
            else
            {
                if (isTopLevelBlock)
                {
                    currentBlock.Add(line);
                }
                else
                {
                    bodyLines.Add(line);
                }
            }

            foreach (var ch in trimmed)
            {
                if (ch == '{') braceDepth++;
                else if (ch == '}') braceDepth = Math.Max(0, braceDepth - 1);
            }

            if (braceDepth == 0 && isTopLevelBlock && currentBlock.Count > 0)
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

    private static bool IsStatement(string trimmed)
    {
        if (trimmed.StartsWith("std::cout", StringComparison.Ordinal) ||
            trimmed.StartsWith("cout", StringComparison.Ordinal) ||
            trimmed.StartsWith("std::cin", StringComparison.Ordinal) ||
            trimmed.StartsWith("cin", StringComparison.Ordinal) ||
            trimmed.StartsWith("std::cerr", StringComparison.Ordinal) ||
            trimmed.StartsWith("cerr", StringComparison.Ordinal) ||
            trimmed.StartsWith("printf", StringComparison.Ordinal) ||
            trimmed.StartsWith("puts", StringComparison.Ordinal) ||
            trimmed.StartsWith("fry::", StringComparison.Ordinal) ||
            trimmed.StartsWith("Display::", StringComparison.Ordinal) ||
            trimmed.StartsWith("display(", StringComparison.Ordinal) ||
            trimmed.StartsWith("for ", StringComparison.Ordinal) ||
            trimmed.StartsWith("for(", StringComparison.Ordinal) ||
            trimmed.StartsWith("while ", StringComparison.Ordinal) ||
            trimmed.StartsWith("while(", StringComparison.Ordinal) ||
            trimmed.StartsWith("if ", StringComparison.Ordinal) ||
            trimmed.StartsWith("if(", StringComparison.Ordinal) ||
            trimmed.StartsWith("else", StringComparison.Ordinal) ||
            trimmed.StartsWith("switch", StringComparison.Ordinal) ||
            trimmed.StartsWith("return", StringComparison.Ordinal) ||
            trimmed.StartsWith("break;", StringComparison.Ordinal) ||
            trimmed.StartsWith("continue;", StringComparison.Ordinal) ||
            trimmed.StartsWith("try", StringComparison.Ordinal) ||
            trimmed.StartsWith("catch", StringComparison.Ordinal) ||
            trimmed.StartsWith("assert", StringComparison.Ordinal))
        {
            return true;
        }

        var endOfWord = trimmed.IndexOfAny(new[] { ' ', '.', '-', '[', '+', '=', '(' });
        if (endOfWord > 0)
        {
            var firstWord = trimmed[..endOfWord];
            if (IsValidCppIdentifier(firstWord) && !IsTypeKeyword(firstWord))
            {
                var remainder = trimmed[endOfWord..].TrimStart();
                if (remainder.StartsWith('.') || remainder.StartsWith("->") || remainder.StartsWith('[') ||
                    remainder.StartsWith("++") || remainder.StartsWith("--") || remainder.StartsWith('='))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsTypeKeyword(string word) => word switch
    {
        "int" or "double" or "float" or "char" or "bool" or "long" or "short" or "unsigned" or "signed" or
        "auto" or "const" or "static" or "constexpr" or "consteval" or "size_t" or "void" or
        "string" or "vector" or "map" or "set" or "pair" or "tuple" or "array" or
        "uint8_t" or "uint16_t" or "uint32_t" or "uint64_t" or
        "int8_t" or "int16_t" or "int32_t" or "int64_t" => true,
        _ => false
    };

    private static bool IsValidCppIdentifier(string token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        if (!char.IsLetter(token[0]) && token[0] != '_') return false;
        return token.All(c => char.IsLetterOrDigit(c) || c == '_');
    }

    private static string ExtractDeclarationKey(string item)
    {
        var trimmed = item.Trim();
        if (trimmed.StartsWith("#define", StringComparison.Ordinal))
        {
            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 1 ? parts[1] : item;
        }
        if (trimmed.StartsWith("using", StringComparison.Ordinal))
        {
            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1 && parts[1] != "namespace") return parts[1];
            return item;
        }
        if (trimmed.StartsWith("struct ", StringComparison.Ordinal) ||
            trimmed.StartsWith("class ", StringComparison.Ordinal) ||
            trimmed.StartsWith("enum ", StringComparison.Ordinal))
        {
            var parts = trimmed.Split(new[] { ' ', '{', ':', ';' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 1 ? parts[1] : item;
        }

        var endIdx = trimmed.IndexOfAny(new[] { '=', ';', '(' });
        if (endIdx > 0)
        {
            var prefix = trimmed[..endIdx].Trim();
            var tokens = prefix.Split(new[] { ' ', '*', '&', '>', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length > 0)
            {
                var lastToken = tokens[^1].Trim();
                if (IsValidCppIdentifier(lastToken))
                {
                    return lastToken;
                }
            }
        }
        return item;
    }

    private string BuildCellProgram(string body, IEnumerable<string>? currentCellTopLevel = null)
    {
        var sb = new StringBuilder();
        foreach (var inc in _cumulativeIncludes)
        {
            sb.AppendLine(inc);
        }
        sb.AppendLine();
        sb.AppendLine("using namespace fry;");
        sb.AppendLine("using namespace fry::display;");
        sb.AppendLine();

        // Emit shared variable declarations
        foreach (var (name, (type, json)) in _sharedVariables)
        {
            sb.AppendLine($"// Shared variable: {name}");
            sb.AppendLine(GenerateVariableDeclaration(name, type, json));
        }

        foreach (var top in _cumulativeTopLevel.Values)
        {
            sb.AppendLine(top);
        }

        if (currentCellTopLevel != null)
        {
            foreach (var top in currentCellTopLevel)
            {
                var key = ExtractDeclarationKey(top);
                if (!_cumulativeTopLevel.ContainsKey(key))
                {
                    sb.AppendLine(top);
                }
            }
        }

        // Check if body already has main
        if (body.Contains("int main(") || body.Contains("int main ()") || body.Contains("void main("))
        {
            sb.AppendLine(body);
        }
        else
        {
            sb.AppendLine("int main() {");
            sb.AppendLine(body);
            sb.AppendLine("    return 0;");
            sb.AppendLine("}");
        }

        return sb.ToString();
    }

    private static string GenerateVariableDeclaration(string name, string typeName, string json)
    {
        var trimmed = json.Trim();
        if (trimmed == "true" || trimmed == "false") return $"bool {name} = {trimmed};";
        if (long.TryParse(trimmed, out var l)) return $"long long {name} = {l}LL;";
        if (double.TryParse(trimmed, System.Globalization.CultureInfo.InvariantCulture, out var d)) return $"double {name} = {d};";
        if (trimmed.StartsWith('"') && trimmed.EndsWith('"')) return $"std::string {name} = {trimmed};";
        if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
        {
            var items = trimmed[1..^1].Trim();
            if (items.Length == 0) return $"std::vector<long long> {name};";
            if (long.TryParse(items.Split(',')[0].Trim(), out _))
            {
                return $"std::vector<long long> {name} = {{ {items} }};";
            }
            if (double.TryParse(items.Split(',')[0].Trim(), System.Globalization.CultureInfo.InvariantCulture, out _))
            {
                return $"std::vector<double> {name} = {{ {items} }};";
            }
        }
        return $"// Complex shared object\nstd::string {name}_json = {JsonSerializer.Serialize(json)};";
    }
}
