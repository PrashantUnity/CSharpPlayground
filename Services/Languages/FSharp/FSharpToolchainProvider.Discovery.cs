using System.Runtime.CompilerServices;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;

public sealed partial class FSharpToolchainProvider
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

        var dotnetRoot = _host.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrWhiteSpace(dotnetRoot))
        {
            var rootDotnet = Path.Combine(dotnetRoot, _host.IsWindows ? "dotnet.exe" : "dotnet");
            if (_host.FileExists(rootDotnet) && yielded.Add(rootDotnet))
            {
                yield return new Candidate(rootDotnet, "DOTNET_ROOT");
            }
        }

        var names = _host.IsWindows ? new[] { "dotnet.exe", "fsi.exe", "dotnet", "fsi" } : new[] { "dotnet", "fsi" };
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
            yield return Path.Combine(programFiles, "dotnet");

            var userProfile = _host.GetEnvironmentVariable("USERPROFILE");
            if (!string.IsNullOrEmpty(userProfile))
            {
                yield return Path.Combine(userProfile, ".dotnet");
            }
        }
        else if (_host.IsMacOS)
        {
            yield return "/usr/local/share/dotnet";
            yield return "/opt/homebrew/bin";
            yield return "/usr/local/bin";
            yield return "/usr/bin";

            var home = _host.HomeDirectory;
            if (!string.IsNullOrEmpty(home))
            {
                yield return Path.Combine(home, ".dotnet");
            }
        }
        else
        {
            yield return "/usr/share/dotnet";
            yield return "/usr/bin";
            yield return "/snap/bin";

            var home = _host.HomeDirectory;
            if (!string.IsNullOrEmpty(home))
            {
                yield return Path.Combine(home, ".dotnet");
            }
        }
    }
}
