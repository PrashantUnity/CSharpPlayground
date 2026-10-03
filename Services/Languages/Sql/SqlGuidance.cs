using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;

/// <summary>
/// Guidance messages and platform-specific installation steps when the SQLite CLI toolchain (sqlite3) is missing.
/// </summary>
public static class SqlGuidance
{
    public static MissingToolchainGuidance NotInstalled(IHostEnvironment host, IReadOnlyList<string>? tooOld = null)
    {
        var summary = tooOld is { Count: > 0 }
            ? $"SQLite 3.0 or newer is required ({string.Join(", ", tooOld)} was found)."
            : "No SQLite 3 CLI (sqlite3) was found on this computer.";

        var steps = new List<string>();

        if (host.IsWindows)
        {
            steps.Add("Install via winget: winget install SQLite.SQLite");
            steps.Add("Or install via chocolatey: choco install sqlite");
            steps.Add("Or download sqlite-tools-win-x64 from https://www.sqlite.org/download.html and add it to PATH.");
        }
        else if (host.IsMacOS)
        {
            steps.Add("SQLite 3 is usually pre-installed on macOS at /usr/bin/sqlite3.");
            steps.Add("Install latest via Homebrew: brew install sqlite");
            steps.Add("Ensure '/usr/bin' or '/opt/homebrew/bin' is in your PATH.");
        }
        else
        {
            steps.Add("Install via apt (Ubuntu/Debian): sudo apt-get update && sudo apt-get install -y sqlite3");
            steps.Add("Install via dnf (Fedora): sudo dnf install -y sqlite");
            steps.Add("Install via pacman (Arch): sudo pacman -S sqlite");
        }

        steps.Add("After installing, click 'Refresh' in the toolchain picker or restart FrySharp.");

        return new MissingToolchainGuidance(
            Title: "SQLite 3 CLI (sqlite3) isn't installed",
            Summary: summary,
            Steps: steps,
            DownloadUrl: "https://www.sqlite.org/download.html");
    }
}
