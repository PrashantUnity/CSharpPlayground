using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Python;

/// <summary>What to tell someone whose machine has no usable Python, in terms of their OS.</summary>
public static class PythonGuidance
{
    public static MissingToolchainGuidance NotInstalled(IHostEnvironment host, IReadOnlyList<string>? tooOld = null)
    {
        var summary = tooOld is { Count: > 0 }
            ? $"Found {string.Join(", ", tooOld)}, but the studio needs Python {PythonToolchainProvider.MinimumVersion} or newer. Install a newer one, then run again."
            : "Python files and cells run with the Python installed on this computer, so any package you install works in them. Install Python 3.9 or newer, then run again: the studio looks for it every time.";

        if (host.IsWindows)
        {
            return new MissingToolchainGuidance(
                "Python isn't installed",
                summary,
                [
                    "Download the installer from python.org and tick \"Add python.exe to PATH\"",
                    "or run: winget install Python.Python.3.13",
                    "The python.exe that opens the Microsoft Store is only a shortcut to the Store, not Python itself"
                ],
                "https://www.python.org/downloads/windows/");
        }

        if (host.IsMacOS)
        {
            return new MissingToolchainGuidance(
                "Python isn't installed",
                summary,
                [
                    "Download the installer from python.org",
                    "or, with Homebrew: brew install python"
                ],
                "https://www.python.org/downloads/macos/");
        }

        return new MissingToolchainGuidance(
            "Python isn't installed",
            summary,
            [
                "Debian or Ubuntu: sudo apt install python3 python3-venv",
                "Fedora: sudo dnf install python3",
                "Arch: sudo pacman -S python"
            ],
            "https://www.python.org/downloads/source/");
    }

    /// <summary>For a Python whose <c>venv</c> or <c>ensurepip</c> module is missing (Debian and Ubuntu split them out).</summary>
    public static string VenvMissing(IHostEnvironment host, ToolchainInfo python) =>
        host.IsLinux
            ? $"{python.DisplayName} can't create virtual environments: install its venv package (Debian or Ubuntu: sudo apt install python{python.Version.Major}.{python.Version.Minor}-venv), then try again."
            : $"{python.DisplayName} can't create virtual environments (its venv or ensurepip module is missing). Reinstall Python from python.org, or pick another Python.";
}
