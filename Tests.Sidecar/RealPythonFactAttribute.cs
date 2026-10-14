using Xunit;

namespace FrySharp.Sidecar.Tests;

/// <summary>
/// A test that runs real Python through the sidecar. Skipped on a machine without <c>python3</c> on its PATH, unless
/// <c>FRY_REQUIRE_PYTHON=1</c>, which makes a missing Python a failure.
/// </summary>
public sealed class RealPythonFactAttribute : FactAttribute
{
    public RealPythonFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("FRY_REQUIRE_PYTHON") == "1") return;
        if (!OnPath("python3") && !OnPath("python")) Skip = "Needs Python on the PATH (or set FRY_REQUIRE_PYTHON=1 to make this a failure).";
    }

    private static bool OnPath(string program)
    {
        var names = OperatingSystem.IsWindows() ? new[] { program + ".exe", program + ".cmd" } : new[] { program };
        return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(folder => names.Any(name => File.Exists(Path.Combine(folder, name))));
    }
}
