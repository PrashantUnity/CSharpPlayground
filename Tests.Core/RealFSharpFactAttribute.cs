using Xunit;

namespace FrySharp.Core.Tests;

/// <summary>
/// A test that runs the real F# Interactive (<c>dotnet fsi</c>). Skipped on a machine without <c>dotnet</c> on its PATH,
/// unless <c>FRY_REQUIRE_FSHARP=1</c>, which makes a missing one a failure.
/// </summary>
public sealed class RealFSharpFactAttribute : FactAttribute
{
    public RealFSharpFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("FRY_REQUIRE_FSHARP") == "1") return;
        if (!OnPath("dotnet")) Skip = "Needs dotnet (the SDK, for dotnet fsi) on the PATH (or set FRY_REQUIRE_FSHARP=1 to make this a failure).";
    }

    private static bool OnPath(string program)
    {
        var names = OperatingSystem.IsWindows() ? new[] { program + ".exe" } : new[] { program };
        return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(folder => names.Any(name => File.Exists(Path.Combine(folder, name))));
    }
}

/// <summary>
/// A test that runs the real F# Interactive and restores a NuGet package, so it needs the network. Skipped unless
/// <c>FRY_TEST_NETWORK=1</c> (a machine that is offline would fail it for no fault of the code).
/// </summary>
public sealed class RealFSharpNuGetFactAttribute : FactAttribute
{
    public RealFSharpNuGetFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("FRY_TEST_NETWORK") != "1") Skip = "Restores a NuGet package: needs the network (set FRY_TEST_NETWORK=1).";
    }
}
