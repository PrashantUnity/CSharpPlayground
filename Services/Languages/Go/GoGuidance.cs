using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;

/// <summary>
/// Guidance messages and platform-specific installation steps when the Go toolchain is missing or outdated.
/// </summary>
public static class GoGuidance
{
    public static MissingToolchainGuidance NotInstalled(IHostEnvironment host, IReadOnlyList<string>? tooOld = null)
    {
        var summary = tooOld is { Count: > 0 }
            ? $"Go 1.18 or newer is required ({string.Join(", ", tooOld)} was found)."
            : "No Go toolchain was found on this computer.";

        var steps = new List<string>();

        if (host.IsWindows)
        {
            steps.Add("Install via winget: winget install GoLang.Go");
            steps.Add("Install via Chocolatey: choco install golang");
            steps.Add("Or download the MSI installer from https://go.dev/dl/");
            steps.Add("Ensure 'go' is in your system PATH (e.g. C:\\Program Files\\Go\\bin).");
        }
        else if (host.IsMacOS)
        {
            steps.Add("Install via Homebrew: brew install go");
            steps.Add("Or download the macOS pkg installer from https://go.dev/dl/");
            steps.Add("Ensure '/opt/homebrew/bin' or '/usr/local/go/bin' is in your PATH.");
        }
        else
        {
            steps.Add("Install via apt (Debian/Ubuntu): sudo apt update && sudo apt install golang-go");
            steps.Add("Install via snap: sudo snap install --classic go");
            steps.Add("Install via dnf (Fedora): sudo dnf install golang");
            steps.Add("Or download official tarball from https://go.dev/dl/ and unpack into /usr/local/go");
        }

        steps.Add("After installing, click 'Refresh' in the toolchain picker or restart FrySharp.");

        return new MissingToolchainGuidance(
            Title: "Go toolchain isn't installed",
            Summary: summary,
            Steps: steps,
            DownloadUrl: "https://go.dev/dl/");
    }
}
