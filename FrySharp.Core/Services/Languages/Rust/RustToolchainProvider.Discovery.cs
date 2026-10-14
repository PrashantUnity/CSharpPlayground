using System.Runtime.CompilerServices;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;

public sealed partial class RustToolchainProvider
{
    private sealed record Candidate(string Path, string Source);

    // Order: the saved choice, then $CARGO_HOME, then whatever a terminal would find (rustup's proxies live in
    // ~/.cargo/bin), then each toolchain rustup has installed (so the picker can offer nightly next to stable).
    private async IAsyncEnumerable<Candidate> CandidatesAsync(ToolchainQuery query, [EnumeratorCancellation] CancellationToken ct)
    {
        var yielded = new HashSet<string>(_host.IsWindows || _host.IsMacOS ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var cargoName = ExecutableSearch.ExecutableName("cargo", _host.IsWindows);

        var selected = SelectedPath;
        if (!string.IsNullOrWhiteSpace(selected) && _host.FileExists(selected) && yielded.Add(selected))
        {
            yield return new Candidate(selected, "Selected");
        }

        var cargoHome = _host.GetEnvironmentVariable("CARGO_HOME");
        if (!string.IsNullOrWhiteSpace(cargoHome))
        {
            var fromCargoHome = Join(Join(cargoHome, "bin"), cargoName);
            if (_host.FileExists(fromCargoHome) && yielded.Add(fromCargoHome))
            {
                yield return new Candidate(fromCargoHome, "CARGO_HOME");
            }
        }

        var loginPath = await _host.GetLoginShellPathAsync(ct).ConfigureAwait(false);
        var folders = ExecutableSearch.SplitPath(loginPath, _host.IsWindows)
            .Concat(ExecutableSearch.SplitPath(_host.GetEnvironmentVariable("PATH"), _host.IsWindows))
            .Concat(WellKnownFolders());

        foreach (var path in ExecutableSearch.FindAll(_host, folders, ["cargo"]))
        {
            if (yielded.Add(path)) yield return new Candidate(path, "PATH");
        }

        foreach (var toolchain in RustupToolchains(cargoName))
        {
            if (yielded.Add(toolchain.Path)) yield return toolchain;
        }
    }

    private IEnumerable<Candidate> RustupToolchains(string cargoName)
    {
        var rustupHome = _host.GetEnvironmentVariable("RUSTUP_HOME");
        if (string.IsNullOrWhiteSpace(rustupHome) && !string.IsNullOrEmpty(_host.HomeDirectory))
        {
            rustupHome = Join(_host.HomeDirectory, ".rustup");
        }

        if (string.IsNullOrWhiteSpace(rustupHome)) yield break;

        var toolchains = Join(rustupHome, "toolchains");
        if (!_host.DirectoryExists(toolchains)) yield break;

        foreach (var folder in _host.GetDirectories(toolchains).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var cargo = Join(Join(folder, "bin"), cargoName);
            if (_host.FileExists(cargo)) yield return new Candidate(cargo, "rustup toolchain");
        }
    }

    private IEnumerable<string> WellKnownFolders()
    {
        var cargoHome = _host.GetEnvironmentVariable("CARGO_HOME");
        if (!string.IsNullOrWhiteSpace(cargoHome)) yield return Join(cargoHome, "bin");

        var home = _host.HomeDirectory;
        if (!string.IsNullOrEmpty(home)) yield return Join(Join(home, ".cargo"), "bin");

        if (_host.IsWindows)
        {
            var userProfile = _host.GetEnvironmentVariable("USERPROFILE");
            if (!string.IsNullOrEmpty(userProfile))
            {
                yield return Join(Join(userProfile, ".cargo"), "bin");
                yield return Join(Join(userProfile, "scoop"), "shims");
            }
        }
        else if (_host.IsMacOS)
        {
            yield return "/opt/homebrew/bin";
            yield return "/opt/homebrew/opt/rustup/bin";
            yield return "/usr/local/bin";
            yield return "/usr/local/opt/rustup/bin";
        }
        else
        {
            yield return "/usr/local/bin";
            yield return "/usr/bin";
            yield return "/snap/bin";
            if (!string.IsNullOrEmpty(home)) yield return Join(Join(home, ".nix-profile"), "bin");
        }
    }
}
