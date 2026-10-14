using Xunit;

namespace FrySharp.Core.Tests;

/// <summary>
/// A test that runs a real C++ compiler (clang++ or g++, which builds each cell). Skipped on a machine without one, unless
/// <c>FRY_REQUIRE_CPP=1</c>, which makes a missing compiler a failure.
/// </summary>
public sealed class RealCppFactAttribute : FactAttribute
{
    public RealCppFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("FRY_REQUIRE_CPP") == "1") return;
        if (!HasCompiler()) Skip = "Needs a C++ compiler (clang++ or g++) on the PATH (or set FRY_REQUIRE_CPP=1 to make this a failure).";
    }

    private static bool HasCompiler()
    {
        var names = OperatingSystem.IsWindows() ? new[] { "clang++.exe", "g++.exe", "cl.exe" } : new[] { "clang++", "g++" };
        var folders = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).ToList();
        folders.AddRange(["/usr/bin", "/usr/local/bin", "/opt/homebrew/bin", "/opt/homebrew/opt/llvm/bin"]);
        return folders.Any(folder => names.Any(name => File.Exists(Path.Combine(folder, name))));
    }
}
