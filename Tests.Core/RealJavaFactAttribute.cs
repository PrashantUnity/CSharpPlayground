using Xunit;

namespace FrySharp.Core.Tests;

/// <summary>
/// A test that runs the real JDK (<c>java</c> and <c>javac</c>; the notebook kernel is JShell). Skipped on a machine without
/// them on its PATH, unless <c>FRY_REQUIRE_JAVA=1</c>, which makes a missing JDK a failure.
/// </summary>
public sealed class RealJavaFactAttribute : FactAttribute
{
    public RealJavaFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("FRY_REQUIRE_JAVA") == "1") return;
        if (!OnPath("java") || !OnPath("javac")) Skip = "Needs a JDK (java and javac) on the PATH (or set FRY_REQUIRE_JAVA=1 to make this a failure).";
    }

    private static bool OnPath(string program)
    {
        var names = OperatingSystem.IsWindows() ? new[] { program + ".exe" } : new[] { program };
        return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(folder => names.Any(name => File.Exists(Path.Combine(folder, name))));
    }
}

/// <summary>A real-JDK test that downloads a Maven artifact: needs the network, so it is skipped unless <c>FRY_TEST_NETWORK=1</c>.</summary>
public sealed class RealJavaNetworkFactAttribute : FactAttribute
{
    public RealJavaNetworkFactAttribute()
    {
        var jdk = new RealJavaFactAttribute();
        if (jdk.Skip is { } reason) { Skip = reason; return; }
        if (Environment.GetEnvironmentVariable("FRY_TEST_NETWORK") != "1") Skip = "Downloads a Maven artifact: needs the network (set FRY_TEST_NETWORK=1).";
    }
}
