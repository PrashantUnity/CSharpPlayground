using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.JavaScript;

/// <summary>What to tell someone whose machine has no usable Node.js, in terms of their OS.</summary>
public static class JavaScriptGuidance
{
    public static MissingToolchainGuidance NotInstalled(IHostEnvironment host, IReadOnlyList<string>? tooOld = null)
    {
        var summary = tooOld is { Count: > 0 }
            ? $"Found {string.Join(", ", tooOld)}, but the studio needs Node.js {JavaScriptToolchainProvider.MinimumVersion} or newer. Install a newer one, then run again."
            : "JavaScript files and notebook cells run with the Node.js installed on this computer, so any package you install works in them. Install Node.js 18 or newer, then run again: the studio looks for it every time.";

        if (host.IsWindows)
        {
            return new MissingToolchainGuidance(
                "Node.js isn't installed",
                summary,
                [
                    "Download the installer from nodejs.org and run it",
                    "or run: winget install OpenJS.NodeJS.LTS",
                    "or, with Chocolatey: choco install nodejs-lts"
                ],
                "https://nodejs.org/en/download/");
        }

        if (host.IsMacOS)
        {
            return new MissingToolchainGuidance(
                "Node.js isn't installed",
                summary,
                [
                    "Download the installer from nodejs.org",
                    "or, with Homebrew: brew install node"
                ],
                "https://nodejs.org/en/download/");
        }

        return new MissingToolchainGuidance(
            "Node.js isn't installed",
            summary,
            [
                "Debian or Ubuntu: sudo apt update && sudo apt install nodejs npm",
                "Fedora: sudo dnf install nodejs npm",
                "Arch: sudo pacman -S nodejs npm"
            ],
            "https://nodejs.org/en/download/package-manager");
    }
}
