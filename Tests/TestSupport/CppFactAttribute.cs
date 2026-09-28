using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;

/// <summary>
/// A test that runs a real C++ compiler (clang++ or g++). Skipped on a machine without a compiler, unless <c>FRY_REQUIRE_CPP=1</c>.
/// </summary>
public sealed class CppFactAttribute : FactAttribute
{
    public CppFactAttribute()
    {
        if (TestCpp.Toolchain == null && !TestCpp.Required)
        {
            Skip = "Needs a C++ compiler (clang++, g++, or cl.exe). Install it, or set FRY_TEST_CPP to an executable.";
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RealCppCollection
{
    public const string Name = "RealCpp";
}

public static class TestCpp
{
    private static readonly Lazy<ToolchainInfo?> Found = new(Find);

    public static bool Required => Environment.GetEnvironmentVariable("FRY_REQUIRE_CPP") == "1";

    public static ToolchainInfo? Toolchain => Found.Value;

    public static ToolchainInfo Require() => Toolchain ?? throw new InvalidOperationException(
        "FRY_REQUIRE_CPP is set but no C++ compiler was found.");

    public static StudioLanguageServices Services(string baseDirectory) => new(baseDirectory);

    private static ToolchainInfo? Find()
    {
        var scratch = Path.Combine(Path.GetTempPath(), "FryPDF_TestCpp_" + Guid.NewGuid().ToString("N"));
        try
        {
            var services = new StudioLanguageServices(scratch);
            var cpp = (CppLanguage)services.Registry.Get(LanguageIds.Cpp)!;
            var explicitPath = Environment.GetEnvironmentVariable("FRY_TEST_CPP");
            if (!string.IsNullOrWhiteSpace(explicitPath)) cpp.CppToolchain.Select(explicitPath);

            var resolution = cpp.CppToolchain.ResolveAsync(new ToolchainQuery()).GetAwaiter().GetResult();
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
