using Xunit;

namespace FrySharp.Core.Tests;

/// <summary>
/// A test that runs the real Go toolchain (<c>go</c>, which builds each cell). Skipped on a machine without it, unless
/// <c>FRY_REQUIRE_GO=1</c>, which makes a missing toolchain a failure.
/// </summary>
public sealed class RealGoFactAttribute : FactAttribute
{
    public RealGoFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("FRY_REQUIRE_GO") == "1") return;
        if (!HasGo()) Skip = "Needs the Go toolchain (go) on the PATH (or set FRY_REQUIRE_GO=1 to make this a failure).";
    }

    private static bool HasGo()
    {
        var name = OperatingSystem.IsWindows() ? "go.exe" : "go";
        var folders = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).ToList();
        folders.AddRange(["/usr/local/go/bin", "/opt/homebrew/bin", "/usr/lib/go/bin"]);
        return folders.Any(folder => File.Exists(Path.Combine(folder, name)));
    }
}

/// <summary>A real-Go test that downloads a module: needs the network, so it is skipped unless <c>FRY_TEST_NETWORK=1</c>.</summary>
public sealed class RealGoNetworkFactAttribute : FactAttribute
{
    public RealGoNetworkFactAttribute()
    {
        var go = new RealGoFactAttribute();
        if (go.Skip is { } reason) { Skip = reason; return; }
        if (Environment.GetEnvironmentVariable("FRY_TEST_NETWORK") != "1") Skip = "Downloads a module: needs the network (set FRY_TEST_NETWORK=1).";
    }
}
