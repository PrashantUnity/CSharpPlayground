using Xunit;

namespace FrySharp.Core.Tests;

/// <summary>
/// A test that runs the real SQLite CLI. Skipped on a machine without <c>sqlite3</c> on its PATH, unless
/// <c>FRY_REQUIRE_SQL=1</c>, which makes a missing one a failure.
/// </summary>
public sealed class RealSqliteFactAttribute : FactAttribute
{
    public RealSqliteFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("FRY_REQUIRE_SQL") == "1") return;
        if (!OnPath("sqlite3")) Skip = "Needs sqlite3 on the PATH (or set FRY_REQUIRE_SQL=1 to make this a failure).";
    }

    private static bool OnPath(string program)
    {
        var names = OperatingSystem.IsWindows() ? new[] { program + ".exe" } : new[] { program };
        return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(folder => names.Any(name => File.Exists(Path.Combine(folder, name))));
    }
}
