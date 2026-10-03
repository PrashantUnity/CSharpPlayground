using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;

/// <summary>
/// Guidance messages and platform-specific installation steps when the F# toolchain (.NET SDK / dotnet fsi) is missing.
/// </summary>
public static class FSharpGuidance
{
    public static MissingToolchainGuidance NotInstalled(IHostEnvironment host, IReadOnlyList<string>? tooOld = null)
    {
        var summary = tooOld is { Count: > 0 }
            ? $"F# 6.0 / .NET 6 or newer is required ({string.Join(", ", tooOld)} was found)."
            : "No .NET SDK with F# Interactive (dotnet fsi) was found on this computer.";

        var steps = new List<string>();

        if (host.IsWindows)
        {
            steps.Add("Install via winget: winget install Microsoft.DotNet.SDK.10");
            steps.Add("Or download the .NET 10 SDK installer from https://dotnet.microsoft.com/download");
            steps.Add("Ensure 'dotnet' is in your system PATH (e.g. C:\\Program Files\\dotnet).");
        }
        else if (host.IsMacOS)
        {
            steps.Add("Install via Homebrew: brew install --cask dotnet-sdk");
            steps.Add("Or download the macOS installer from https://dotnet.microsoft.com/download");
            steps.Add("Ensure '/usr/local/share/dotnet' or '/opt/homebrew/bin' is in your PATH.");
        }
        else
        {
            steps.Add("Install via apt (Ubuntu): sudo apt-get update && sudo apt-get install -y dotnet-sdk-10.0");
            steps.Add("Install via snap: sudo snap install dotnet-sdk --classic");
            steps.Add("Install via dnf (Fedora): sudo dnf install dotnet-sdk-10.0");
            steps.Add("Or download the installer script from https://dot.net/v1/dotnet-install.sh");
        }

        steps.Add("After installing, click 'Refresh' in the toolchain picker or restart FrySharp.");

        return new MissingToolchainGuidance(
            Title: "F# Interactive (.NET SDK) isn't installed",
            Summary: summary,
            Steps: steps,
            DownloadUrl: "https://dotnet.microsoft.com/download");
    }
}
