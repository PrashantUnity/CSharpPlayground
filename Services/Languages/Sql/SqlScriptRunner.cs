using System.Text.RegularExpressions;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;

/// <summary>
/// Executes a SQL script (.sql) using the SQLite CLI (<c>sqlite3 -header -table &lt;database&gt; ".read &lt;file.sql&gt;"</c>)
/// with formatted table output, interactive stdin streaming, and database directive resolution.
/// </summary>
public sealed partial class SqlScriptRunner : IScriptRunner
{
    [GeneratedRegex(@"^--\s*:(?:db|database)\s+(?<path>[^\r\n]+)", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex DatabaseDirectiveRegex();

    public Task<ScriptRunPlan> PlanAsync(ScriptRunContext context, CancellationToken ct = default)
    {
        var executable = context.Toolchain.ExecutablePath;
        var fileName = Path.GetFileName(context.SourceFilePath);
        var sourceDir = Path.GetDirectoryName(context.SourceFilePath) ?? context.WorkingDirectory;

        var dbPath = ResolveDatabasePath(context.SourceFilePath, sourceDir);

        var runArgs = new List<string>
        {
            "-header",
            "-table",
            dbPath,
            $".read {context.SourceFilePath}"
        };

        var workingDir = !string.IsNullOrEmpty(sourceDir) ? sourceDir : context.WorkingDirectory;

        var steps = new List<ProcessStep>
        {
            new(
                Label: $"sqlite3 {fileName}",
                Spec: new ProcessStartSpec
                {
                    FileName = executable,
                    Arguments = runArgs,
                    WorkingDirectory = workingDir,
                    Environment = new Dictionary<string, string?>(StringComparer.Ordinal)
                },
                IsBuildStep: false)
        };

        return Task.FromResult(new ScriptRunPlan(steps));
    }

    private static string ResolveDatabasePath(string scriptPath, string workingDir)
    {
        try
        {
            if (File.Exists(scriptPath))
            {
                var content = File.ReadAllText(scriptPath);
                var match = DatabaseDirectiveRegex().Match(content);
                if (match.Success)
                {
                    var customPath = match.Groups["path"].Value.Trim().Trim('\'', '"');
                    if (Path.IsPathRooted(customPath)) return customPath;
                    return Path.Combine(workingDir, customPath);
                }
            }
        }
        catch
        {
            // Fall back to :memory: if read fails
        }

        // Look for matching .db / .sqlite in the same directory
        var baseName = Path.GetFileNameWithoutExtension(scriptPath);
        var candidateDb = Path.Combine(workingDir, baseName + ".db");
        if (File.Exists(candidateDb)) return candidateDb;

        var candidateSqlite = Path.Combine(workingDir, baseName + ".sqlite");
        if (File.Exists(candidateSqlite)) return candidateSqlite;

        return ":memory:";
    }
}
