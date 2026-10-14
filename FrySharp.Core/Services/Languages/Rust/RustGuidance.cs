using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;

/// <summary>
/// What to tell someone when the Rust toolchain (cargo and rustc) is missing, too old, or installed without a toolchain.
/// </summary>
public static class RustGuidance
{
    public const string DownloadUrl = "https://rustup.rs";

    public static MissingToolchainGuidance NotInstalled(IHostEnvironment host, IReadOnlyList<string>? tooOld = null, bool rustupWithoutToolchain = false)
    {
        var summary = rustupWithoutToolchain
            ? "rustup is installed but no Rust toolchain is set up yet."
            : tooOld is { Count: > 0 }
                ? $"Rust {RustToolchainProvider.MinimumVersion.ToString(2)} or newer is required ({string.Join(", ", tooOld)} was found)."
                : "No Rust toolchain (cargo and rustc) was found on this computer.";

        var steps = new List<string>();

        if (rustupWithoutToolchain)
        {
            steps.Add("Install the stable toolchain: rustup default stable");
        }

        if (host.IsWindows)
        {
            steps.Add("Install via winget: winget install Rustlang.Rustup");
            steps.Add("Rust links with the MSVC linker: install Visual Studio Build Tools via winget install Microsoft.VisualStudio.2022.BuildTools --override \"--passive --wait --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended\"");
            steps.Add("Or download rustup-init.exe from https://rustup.rs");
        }
        else if (host.IsMacOS)
        {
            steps.Add("Install with rustup: curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh");
            steps.Add("Or with Homebrew: brew install rustup, then rustup default stable");
            steps.Add("Rust links with Apple's tools: xcode-select --install");
        }
        else
        {
            steps.Add("Install with rustup: curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh");
            steps.Add("Or with your package manager: sudo apt install rustc cargo (Debian/Ubuntu), sudo dnf install rust cargo (Fedora)");
            steps.Add("Rust needs a C linker: sudo apt install build-essential (or your distribution's gcc).");
        }

        steps.Add("After installing, click 'Refresh' in the toolchain picker or restart FrySharp.");

        return new MissingToolchainGuidance(
            Title: "Rust isn't installed",
            Summary: summary,
            Steps: steps,
            DownloadUrl: DownloadUrl);
    }

    /// <summary>The hint shown when a build fails because there is no system linker (every OS in one message).</summary>
    public static string LinkerHint(string linker) =>
        $"Rust couldn't find the linker '{linker}'. Install a C toolchain: on Windows, Visual Studio Build Tools with the " +
        "\"Desktop development with C++\" workload; on macOS, run xcode-select --install; on Linux, sudo apt install build-essential.";
}
