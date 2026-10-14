using Xunit;

namespace FrySharp.Core.Tests;

/// <summary>
/// A test that runs the real Rust toolchain (<c>cargo</c>, which builds each cell with <c>rustc</c>). Skipped on a machine without it, unless
/// <c>FRY_REQUIRE_RUST=1</c>, which makes a missing toolchain a failure.
/// </summary>
public sealed class RealRustFactAttribute : FactAttribute
{
    public RealRustFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("FRY_REQUIRE_RUST") == "1") return;
        if (!HasCargo()) Skip = "Needs the Rust toolchain (cargo) on the PATH or in ~/.cargo/bin (or set FRY_REQUIRE_RUST=1 to make this a failure).";
    }

    private static bool HasCargo()
    {
        var name = OperatingSystem.IsWindows() ? "cargo.exe" : "cargo";
        var folders = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).ToList();
        folders.Add(Path.Combine(Environment.GetEnvironmentVariable("CARGO_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cargo"), "bin"));
        return folders.Any(folder => File.Exists(Path.Combine(folder, name)));
    }
}

/// <summary>A real-Rust test that downloads a crate: needs the network, so it is skipped unless <c>FRY_TEST_NETWORK=1</c>.</summary>
public sealed class RealRustNetworkFactAttribute : FactAttribute
{
    public RealRustNetworkFactAttribute()
    {
        var rust = new RealRustFactAttribute();
        if (rust.Skip is { } reason) { Skip = reason; return; }
        if (Environment.GetEnvironmentVariable("FRY_TEST_NETWORK") != "1") Skip = "Downloads a crate: needs the network (set FRY_TEST_NETWORK=1).";
    }
}
