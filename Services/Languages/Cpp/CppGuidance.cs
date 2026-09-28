using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;

/// <summary>What to tell someone whose machine has no usable C++ compiler (clang++, g++, or cl.exe).</summary>
public static class CppGuidance
{
    public static MissingToolchainGuidance NotInstalled(IHostEnvironment host, IReadOnlyList<string>? tooOld = null)
    {
        string summary;
        if (tooOld is { Count: > 0 })
        {
            summary = $"Found {string.Join(", ", tooOld)}, but the studio needs a modern C++ compiler (supporting C++17 or C++20). Update your compiler, then run again.";
        }
        else
        {
            summary = "Running C++ files requires a C++ compiler (clang++, g++, or MSVC cl.exe) installed on this computer. Install a compiler, then run again: the studio detects it automatically.";
        }

        if (host.IsWindows)
        {
            return new MissingToolchainGuidance(
                "C++ compiler isn't installed",
                summary,
                [
                    "Install LLVM Clang via winget: winget install LLVM.LLVM",
                    "or Visual Studio C++ Build Tools: winget install Microsoft.VisualStudio.2022.BuildTools",
                    "or MinGW-w64 via MSYS2: winget install MSYS2.MSYS2"
                ],
                "https://visualstudio.microsoft.com/visual-cpp-build-tools/");
        }

        if (host.IsMacOS)
        {
            return new MissingToolchainGuidance(
                "C++ compiler isn't installed",
                summary,
                [
                    "Install Apple Command Line Tools: xcode-select --install",
                    "or LLVM Clang via Homebrew: brew install llvm",
                    "or GCC via Homebrew: brew install gcc"
                ],
                "https://developer.apple.com/xcode/");
        }

        return new MissingToolchainGuidance(
            "C++ compiler isn't installed",
            summary,
            [
                "Debian or Ubuntu: sudo apt update && sudo apt install build-essential clang",
                "Fedora: sudo dnf groupinstall \"Development Tools\" && sudo dnf install clang",
                "Arch: sudo pacman -S base-devel clang"
            ],
            "https://gcc.gnu.org/");
    }
}
