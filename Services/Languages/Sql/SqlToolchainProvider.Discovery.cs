using System.Runtime.CompilerServices;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;

public sealed partial class SqlToolchainProvider
{
    private sealed record Candidate(string Path, string Source);

    private async IAsyncEnumerable<Candidate> CandidatesAsync(ToolchainQuery query, [EnumeratorCancellation] CancellationToken ct)
    {
        var yielded = new HashSet<string>(_host.IsWindows || _host.IsMacOS ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        var selected = SelectedPath;
        if (!string.IsNullOrWhiteSpace(selected) && _host.FileExists(selected) && yielded.Add(selected))
        {
            yield return new Candidate(selected, "Selected");
        }

        var customEnv = _host.GetEnvironmentVariable("SQLITE3_PATH");
        if (!string.IsNullOrWhiteSpace(customEnv) && _host.FileExists(customEnv) && yielded.Add(customEnv))
        {
            yield return new Candidate(customEnv, "SQLITE3_PATH");
        }

        var names = _host.IsWindows ? new[] { "sqlite3.exe", "sqlite.exe" } : new[] { "sqlite3", "sqlite" };
        var loginPath = await _host.GetLoginShellPathAsync(ct).ConfigureAwait(false);
        var folders = ExecutableSearch.SplitPath(loginPath, _host.IsWindows)
            .Concat(ExecutableSearch.SplitPath(_host.GetEnvironmentVariable("PATH"), _host.IsWindows))
            .Concat(WellKnownFolders());

        foreach (var path in ExecutableSearch.FindAll(_host, folders, names))
        {
            if (yielded.Add(path)) yield return new Candidate(path, "PATH");
        }
    }

    private IEnumerable<string> WellKnownFolders()
    {
        if (_host.IsWindows)
        {
            var programFiles = _host.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
            yield return Path.Combine(programFiles, "SQLite");

            var programData = _host.GetEnvironmentVariable("ProgramData") ?? @"C:\ProgramData";
            yield return Path.Combine(programData, "chocolatey", "bin");
            yield return @"C:\sqlite";
        }
        else if (_host.IsMacOS)
        {
            yield return "/usr/bin";
            yield return "/opt/homebrew/bin";
            yield return "/usr/local/bin";
        }
        else
        {
            yield return "/usr/bin";
            yield return "/usr/local/bin";
            yield return "/snap/bin";
        }
    }
}
