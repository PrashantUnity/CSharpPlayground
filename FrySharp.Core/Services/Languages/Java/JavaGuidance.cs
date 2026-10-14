using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;

/// <summary>What to tell someone whose machine has no usable Java Development Kit (JDK), in terms of their OS.</summary>
public static class JavaGuidance
{
    public static MissingToolchainGuidance NotInstalled(IHostEnvironment host, IReadOnlyList<string>? tooOld = null, bool missingCompiler = false)
    {
        string summary;
        if (tooOld is { Count: > 0 })
        {
            summary = $"Found {string.Join(", ", tooOld)}, but the studio needs Java {JavaToolchainProvider.MinimumVersion} or newer. Install a newer JDK, then run again.";
        }
        else if (missingCompiler)
        {
            summary = "A Java Runtime (JRE) was found, but compiling Java files requires a Java Development Kit (JDK) with the 'javac' compiler. Install a JDK and run again.";
        }
        else
        {
            summary = "Java files require a Java Development Kit (JDK 11 or newer) installed on this computer. Install a JDK, then run again: the studio looks for it every time.";
        }

        if (host.IsWindows)
        {
            return new MissingToolchainGuidance(
                "JDK isn't installed",
                summary,
                [
                    "Install with winget: winget install Microsoft.OpenJDK.21",
                    "or with Eclipse Temurin: winget install EclipseAdoptium.Temurin.21.JDK",
                    "or download from https://adoptium.net/"
                ],
                "https://adoptium.net/");
        }

        if (host.IsMacOS)
        {
            return new MissingToolchainGuidance(
                "JDK isn't installed",
                summary,
                [
                    "With Homebrew: brew install openjdk",
                    "or download Eclipse Temurin from https://adoptium.net/",
                    "or Oracle JDK from https://www.oracle.com/java/technologies/downloads/"
                ],
                "https://adoptium.net/");
        }

        return new MissingToolchainGuidance(
            "JDK isn't installed",
            summary,
            [
                "Debian or Ubuntu: sudo apt update && sudo apt install default-jdk",
                "Fedora: sudo dnf install java-latest-openjdk-devel",
                "Arch: sudo pacman -S jdk-openjdk"
            ],
            "https://adoptium.net/");
    }
}
