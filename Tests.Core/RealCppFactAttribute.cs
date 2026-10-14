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

    private static readonly Lazy<bool> WorkingCompiler = new(CheckWorkingCompiler);
    private static bool HasCompiler() => WorkingCompiler.Value;

    private static bool CheckWorkingCompiler()
    {
        var names = OperatingSystem.IsWindows() ? new[] { "clang++.exe", "g++.exe", "cl.exe" } : new[] { "clang++", "g++" };
        var folders = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).ToList();
        folders.AddRange(["/usr/bin", "/usr/local/bin", "/opt/homebrew/bin", "/opt/homebrew/opt/llvm/bin"]);
        var compiler = folders.SelectMany(folder => names.Select(name => Path.Combine(folder, name))).FirstOrDefault(File.Exists);
        if (compiler == null) return false;

        if (!OperatingSystem.IsWindows()) return true;

        // On Windows, verify standard headers work (LLVM clang++ requires Visual Studio C++ Build Tools or MinGW)
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "fry_cpp_smoke_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(tempDir);
            var src = Path.Combine(tempDir, "smoke.cpp");
            var bin = Path.Combine(tempDir, "smoke.exe");
            File.WriteAllText(src, "#include <iostream>\nint main() { return 0; }\n");

            var isCl = compiler.EndsWith("cl.exe", StringComparison.OrdinalIgnoreCase);
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = compiler,
                Arguments = isCl ? $"/Fe:\"{bin}\" \"{src}\"" : $"\"{src}\" -o \"{bin}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null) return false;
            var exited = proc.WaitForExit(5000);
            if (!exited) { try { proc.Kill(); } catch { } return false; }
            var success = proc.ExitCode == 0;
            try { Directory.Delete(tempDir, recursive: true); } catch { }
            return success;
        }
        catch
        {
            return false;
        }
    }
}
