using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Python;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;

/// <summary>
/// A test that runs real Python. It's skipped on a machine without Python 3.9+, unless <c>FRY_REQUIRE_PYTHON=1</c>
/// (as in CI) makes a missing Python a failure. <c>FRY_TEST_PYTHON</c> names the interpreter to use; otherwise the
/// studio's own discovery finds one, with nothing saved and no studio environment.
/// </summary>
public sealed class PythonFactAttribute : FactAttribute
{
    public PythonFactAttribute()
    {
        if (TestPython.Toolchain == null && !TestPython.Required)
        {
            Skip = "Needs Python 3.9 or newer (install it, or set FRY_TEST_PYTHON to an interpreter).";
        }
    }
}

/// <summary>Tests that start real Python processes run one class at a time, so their timings stay predictable.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RealPythonCollection
{
    public const string Name = "RealPython";
}

public static class TestPython
{
    private static readonly Lazy<ToolchainInfo?> Found = new(Find);

    public static bool Required => Environment.GetEnvironmentVariable("FRY_REQUIRE_PYTHON") == "1";

    /// <summary>The interpreter real-Python tests use, or null when there's none.</summary>
    public static ToolchainInfo? Toolchain => Found.Value;

    public static ToolchainInfo Require() => Toolchain ?? throw new InvalidOperationException(
        "FRY_REQUIRE_PYTHON is set but no Python 3.9+ was found. Install Python or set FRY_TEST_PYTHON to its path.");

    /// <summary>Studio services over a throwaway folder, so a test never sees or changes the user's choices.</summary>
    public static StudioLanguageServices Services(string baseDirectory) => new(baseDirectory);

    private static ToolchainInfo? Find()
    {
        var scratch = Path.Combine(Path.GetTempPath(), "FryPDF_TestPython_" + Guid.NewGuid().ToString("N"));
        try
        {
            var services = new StudioLanguageServices(scratch);
            var python = (PythonLanguage)services.Registry.Get(LanguageIds.Python)!;
            var explicitPath = Environment.GetEnvironmentVariable("FRY_TEST_PYTHON");
            if (!string.IsNullOrWhiteSpace(explicitPath)) python.PythonToolchain.Select(explicitPath);

            var resolution = python.PythonToolchain.ResolveAsync(new ToolchainQuery()).GetAwaiter().GetResult();
            return resolution.Toolchain;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            try
            {
                if (Directory.Exists(scratch)) Directory.Delete(scratch, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
