namespace CSharpEditorPlugin.Tests.TestSupport;

/// <summary>
/// Controls whether time-intensive tests (real toolchains, full documentation snippet execution,
/// and slow timeout tests) should be executed or skipped.
/// By default, these tests are skipped to keep test runs fast (seconds instead of minutes).
/// Pass argument "time" / "--time" or set environment variable TIME=1 (or FRY_TEST_TIME=1) to run them.
/// </summary>
public static class TimeGate
{
    private static readonly Lazy<bool> EnabledLazy = new(CheckEnabled);

    /// <summary>
    /// True when time-intensive tests are enabled.
    /// </summary>
    public static bool IsEnabled => EnabledLazy.Value;

    public const string SkipReason =
        "Time-intensive test skipped by default. Pass 'time' in args or set TIME=1 to run.";

    private static bool CheckEnabled()
    {
        var env = Environment.GetEnvironmentVariable("TIME")
               ?? Environment.GetEnvironmentVariable("RUN_TIME_TESTS")
               ?? Environment.GetEnvironmentVariable("FRY_TEST_TIME");

        if (env is "1" || string.Equals(env, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var args = Environment.GetCommandLineArgs();
        return args.Any(a => string.Equals(a, "time", StringComparison.OrdinalIgnoreCase)
                          || string.Equals(a, "--time", StringComparison.OrdinalIgnoreCase)
                          || string.Equals(a, "-time", StringComparison.OrdinalIgnoreCase)
                          || string.Equals(a, "time=1", StringComparison.OrdinalIgnoreCase));
    }
}
