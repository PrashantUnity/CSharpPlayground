using System.Text.RegularExpressions;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;

/// <summary>
/// Finds LLDB's Debug Adapter Protocol server (<c>lldb-dap</c>, once called <c>lldb-vscode</c>), which debugs any native
/// program with DWARF or PDB debug info: C++ and Rust here. It looks next to the toolchain, then where Homebrew, Xcode,
/// the distributions and the LLVM installers put it.
/// </summary>
public static partial class LldbDapLocator
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    [GeneratedRegex(@"(?:version\s+|lldb\s+)(?<ver>\d+(?:\.\d+)+)", RegexOptions.IgnoreCase)]
    private static partial Regex VersionRegex();

    [GeneratedRegex(@"^llvm-(?<major>\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex LlvmFolderRegex();

    /// <param name="siblingFolders">Folders to look in first: where the toolchain's own tools are (clang's folder brings lldb-dap).</param>
    /// <param name="missing">What to say when none is found.</param>
    public static async Task<DebuggerResolution> ResolveAsync(
        IHostEnvironment host,
        IEnumerable<string?> siblingFolders,
        MissingToolchainGuidance missing,
        CancellationToken ct = default)
    {
        foreach (var path in await CandidatesAsync(host, siblingFolders, ct).ConfigureAwait(false))
        {
            try
            {
                var result = await host.RunAsync(path, ["--version"], ProbeTimeout, ct).ConfigureAwait(false);
                if (result.ExitCode == 0 || !string.IsNullOrWhiteSpace(result.StandardOutput))
                {
                    var versionText = (result.StandardOutput + " " + result.StandardError).Trim();
                    var match = VersionRegex().Match(versionText);
                    return new DebuggerResolution(
                        IsAvailable: true,
                        DebuggerName: "lldb-dap",
                        ExecutablePath: path,
                        Version: match.Success ? match.Groups["ver"].Value : "1.0.0",
                        MissingGuidance: null);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Try the next candidate.
            }
        }

        return new DebuggerResolution(IsAvailable: false, DebuggerName: "lldb-dap", ExecutablePath: null, Version: null, MissingGuidance: missing);
    }

    /// <summary>Every place to try, best first, ending with the bare names a PATH lookup would find.</summary>
    public static async Task<IReadOnlyList<string>> CandidatesAsync(IHostEnvironment host, IEnumerable<string?> siblingFolders, CancellationToken ct = default)
    {
        var names = new[] { ExecutableSearch.ExecutableName("lldb-dap", host.IsWindows), ExecutableSearch.ExecutableName("lldb-vscode", host.IsWindows) };
        var candidates = new List<string>();

        void In(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) return;
            foreach (var name in names) candidates.Add(folder.TrimEnd('/', '\\') + (host.IsWindows ? "\\" : "/") + name);
        }

        foreach (var folder in siblingFolders) In(folder ?? string.Empty);

        if (host.IsMacOS)
        {
            In("/opt/homebrew/opt/llvm/bin");
            In("/usr/local/opt/llvm/bin");

            // Xcode and the Command Line Tools ship one that is only reachable through xcrun.
            if (host.FileExists("/usr/bin/xcrun"))
            {
                var found = await host.RunAsync("/usr/bin/xcrun", ["-f", "lldb-dap"], ProbeTimeout, ct).ConfigureAwait(false);
                var path = found.StandardOutput.Trim();
                if (found.ExitCode == 0 && path.Length > 0) candidates.Add(path);
            }

            In("/usr/bin");
            In("/opt/homebrew/bin");
        }
        else if (host.IsWindows)
        {
            var programFiles = host.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
            In(programFiles + @"\LLVM\bin");
        }
        else
        {
            In("/usr/bin");
            In("/usr/local/bin");

            // Debian and Ubuntu install lldb-NN under /usr/lib/llvm-NN, newest first.
            foreach (var folder in host.GetDirectories("/usr/lib")
                         .Select(f => (Folder: f, Match: LlvmFolderRegex().Match(f[(f.LastIndexOfAny(['/', '\\']) + 1)..])))
                         .Where(f => f.Match.Success)
                         .OrderByDescending(f => int.Parse(f.Match.Groups["major"].Value)))
            {
                In(folder.Folder + "/bin");
            }
        }

        candidates.AddRange(names);
        return candidates.Distinct(StringComparer.Ordinal).ToList();
    }
}
