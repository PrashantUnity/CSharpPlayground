using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using Xunit;

namespace CSharpEditorPlugin.Tests.TestSupport;

/// <summary>
/// A test that runs real Dart SDK (dart run / JIT). Skipped on a machine without Dart 2.12+, unless <c>FRY_REQUIRE_DART=1</c>.
/// </summary>
public sealed class DartFactAttribute : FactAttribute
{
    public DartFactAttribute()
    {
        if (!TimeGate.IsEnabled && !TestDart.Required)
        {
            Skip = TimeGate.SkipReason;
            return;
        }

        if (TestDart.Toolchain == null && !TestDart.Required)
        {
            Skip = "Needs the Dart SDK (dart). Install it, or set FRY_TEST_DART to an executable.";
        }
    }
}

public static class TestDart
{
    private static readonly Lazy<ToolchainInfo?> Found = new(Find);

    public static bool Required => Environment.GetEnvironmentVariable("FRY_REQUIRE_DART") == "1";

    public static ToolchainInfo? Toolchain => Found.Value;

    public static ToolchainInfo Require() => Toolchain ?? throw new InvalidOperationException(
        "FRY_REQUIRE_DART is set but no Dart SDK was found.");

    private static ToolchainInfo? Find()
    {
        var explicitPath = Environment.GetEnvironmentVariable("FRY_TEST_DART");
        var host = new HostEnvironment();
        if (!string.IsNullOrWhiteSpace(explicitPath) && host.FileExists(explicitPath))
        {
            return ProbePath(host, explicitPath);
        }

        var candidates = new List<string>();
        if (host.IsMacOS)
        {
            candidates.Add("/opt/homebrew/bin/dart");
            candidates.Add("/usr/local/bin/dart");
        }
        else if (host.IsWindows)
        {
            candidates.Add(@"C:\tools\dart-sdk\bin\dart.exe");
            candidates.Add(@"C:\Program Files\Dart\dart-sdk\bin\dart.exe");
        }
        else
        {
            candidates.Add("/usr/bin/dart");
            candidates.Add("/usr/lib/dart/bin/dart");
        }

        var pathEnv = host.GetEnvironmentVariable("PATH") ?? string.Empty;
        var sep = host.IsWindows ? ';' : ':';
        var binary = host.IsWindows ? "dart.exe" : "dart";
        foreach (var dir in pathEnv.Split(sep, StringSplitOptions.RemoveEmptyEntries))
        {
            var p = Path.Combine(dir.Trim(), binary);
            if (host.FileExists(p)) candidates.Add(p);
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var info = ProbePath(host, candidate);
            if (info != null) return info;
        }

        return null;
    }

    private static ToolchainInfo? ProbePath(IHostEnvironment host, string path)
    {
        try
        {
            var result = host.RunAsync(path, ["--version"], TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            var text = (result.StandardOutput + " " + result.StandardError).Trim();
            var match = Regex.Match(text, @"Dart SDK version:\s+(?<version>\d+\.\d+\.\d+)", RegexOptions.IgnoreCase);
            if (match.Success && Version.TryParse(match.Groups["version"].Value, out var ver) && ver >= new Version(2, 12, 0))
            {
                return new ToolchainInfo
                {
                    LanguageId = "dart",
                    ExecutablePath = path,
                    DisplayName = $"Dart {ver}",
                    Version = ver,
                    Source = "System"
                };
            }
        }
        catch
        {
            // Ignore probe failure
        }

        return null;
    }
}
