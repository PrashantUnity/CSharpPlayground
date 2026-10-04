using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using Xunit;

namespace CSharpEditorPlugin.Tests.TestSupport;

/// <summary>
/// A test that runs real Zig compiler (zig run / build-exe). Skipped on a machine without Zig 0.11+, unless <c>FRY_REQUIRE_ZIG=1</c>.
/// </summary>
public sealed class ZigFactAttribute : FactAttribute
{
    public ZigFactAttribute()
    {
        if (TestZig.Toolchain == null && !TestZig.Required)
        {
            Skip = "Needs the Zig compiler (zig 0.11+). Install it from https://ziglang.org/download/, or set FRY_TEST_ZIG to an executable.";
        }
    }
}

public static class TestZig
{
    private static readonly Lazy<ToolchainInfo?> Found = new(Find);

    public static bool Required => Environment.GetEnvironmentVariable("FRY_REQUIRE_ZIG") == "1";

    public static ToolchainInfo? Toolchain => Found.Value;

    public static ToolchainInfo Require() => Toolchain ?? throw new InvalidOperationException(
        "FRY_REQUIRE_ZIG is set but no Zig compiler was found.");

    private static ToolchainInfo? Find()
    {
        var explicitPath = Environment.GetEnvironmentVariable("FRY_TEST_ZIG");
        var host = new HostEnvironment();
        if (!string.IsNullOrWhiteSpace(explicitPath) && host.FileExists(explicitPath))
        {
            return ProbePath(host, explicitPath);
        }

        var candidates = new List<string>();
        if (host.IsMacOS)
        {
            candidates.Add("/opt/homebrew/bin/zig");
            candidates.Add("/usr/local/bin/zig");
        }
        else if (host.IsWindows)
        {
            candidates.Add(@"C:\Program Files\Zig\zig.exe");
            candidates.Add(@"C:\tools\zig\zig.exe");
        }
        else
        {
            candidates.Add("/usr/bin/zig");
            candidates.Add("/usr/local/bin/zig");
            candidates.Add("/snap/bin/zig");
        }

        var pathEnv = host.GetEnvironmentVariable("PATH") ?? string.Empty;
        var sep = host.IsWindows ? ';' : ':';
        var binary = host.IsWindows ? "zig.exe" : "zig";
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
            var result = host.RunAsync(path, ["version"], TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            var text = (result.StandardOutput + " " + result.StandardError).Trim();
            var match = Regex.Match(text, @"^(?<version>\d+\.\d+\.\d+)", RegexOptions.IgnoreCase);
            if (match.Success && Version.TryParse(match.Groups["version"].Value, out var ver) && ver >= new Version(0, 11, 0))
            {
                return new ToolchainInfo
                {
                    LanguageId = "zig",
                    ExecutablePath = path,
                    DisplayName = $"Zig {ver}",
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
