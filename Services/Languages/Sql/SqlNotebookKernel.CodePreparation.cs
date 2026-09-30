using System.Text;
using System.Text.Json;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;

public sealed partial class SqlNotebookKernel
{
    private static string CleanCode(string code, out List<string> shareDirectives)
    {
        shareDirectives = new List<string>();
        var sb = new StringBuilder();
        var lines = code.Split('\n');

        foreach (var raw in lines)
        {
            var trimmed = raw.Trim();
            if (trimmed.StartsWith("#!share", StringComparison.OrdinalIgnoreCase))
            {
                shareDirectives.Add(trimmed);
                continue;
            }
            sb.AppendLine(raw);
        }

        return sb.ToString().Trim();
    }

    private static string GenerateSharedTableSql(string varName, string jsonValue)
    {
        if (string.IsNullOrWhiteSpace(jsonValue)) return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(jsonValue);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                var count = root.GetArrayLength();
                if (count == 0) return string.Empty;

                var first = root[0];
                if (first.ValueKind == JsonValueKind.Object)
                {
                    // Table with multiple columns
                    var columns = new List<(string Name, string Type)>();
                    foreach (var prop in first.EnumerateObject())
                    {
                        var colType = prop.Value.ValueKind switch
                        {
                            JsonValueKind.Number => prop.Value.TryGetInt64(out _) ? "INTEGER" : "REAL",
                            JsonValueKind.True or JsonValueKind.False => "BOOLEAN",
                            _ => "TEXT"
                        };
                        columns.Add((prop.Name, colType));
                    }

                    var colDefs = string.Join(", ", columns.Select(c => $"\"{c.Name}\" {c.Type}"));
                    var colNames = string.Join(", ", columns.Select(c => $"\"{c.Name}\""));

                    var sb = new StringBuilder();
                    sb.AppendLine($"DROP TABLE IF EXISTS \"{varName}\";");
                    sb.AppendLine($"CREATE TABLE \"{varName}\" ({colDefs});");

                    var insertRows = new List<string>();
                    foreach (var row in root.EnumerateArray())
                    {
                        if (row.ValueKind != JsonValueKind.Object) continue;
                        var vals = new List<string>();
                        foreach (var col in columns)
                        {
                            if (row.TryGetProperty(col.Name, out var val))
                            {
                                vals.Add(FormatSqlValue(val));
                            }
                            else
                            {
                                vals.Add("NULL");
                            }
                        }
                        insertRows.Add($"({string.Join(", ", vals)})");
                    }

                    if (insertRows.Count > 0)
                    {
                        sb.AppendLine($"INSERT INTO \"{varName}\" ({colNames}) VALUES");
                        sb.AppendLine(string.Join(",\n", insertRows) + ";");
                    }
                    return sb.ToString();
                }
                else
                {
                    // Single column array: [88, 95, 72]
                    var colType = first.ValueKind switch
                    {
                        JsonValueKind.Number => first.TryGetInt64(out _) ? "INTEGER" : "REAL",
                        JsonValueKind.True or JsonValueKind.False => "BOOLEAN",
                        _ => "TEXT"
                    };

                    var sb = new StringBuilder();
                    sb.AppendLine($"DROP TABLE IF EXISTS \"{varName}\";");
                    sb.AppendLine($"CREATE TABLE \"{varName}\" (value {colType});");

                    var vals = new List<string>();
                    foreach (var elem in root.EnumerateArray())
                    {
                        vals.Add($"({FormatSqlValue(elem)})");
                    }

                    if (vals.Count > 0)
                    {
                        sb.AppendLine($"INSERT INTO \"{varName}\" (value) VALUES");
                        sb.AppendLine(string.Join(",\n", vals) + ";");
                    }
                    return sb.ToString();
                }
            }
        }
        catch
        {
            // Ignore JSON parse errors for variable sharing
        }

        return string.Empty;
    }

    private static string FormatSqlValue(JsonElement elem)
    {
        return elem.ValueKind switch
        {
            JsonValueKind.Number => elem.GetRawText(),
            JsonValueKind.True => "1",
            JsonValueKind.False => "0",
            JsonValueKind.Null => "NULL",
            _ => "'" + elem.GetString()?.Replace("'", "''") + "'"
        };
    }
}
