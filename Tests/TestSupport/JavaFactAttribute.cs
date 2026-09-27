using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;

/// <summary>
/// A test that runs real Java JDK (javac + java). Skipped on a machine without JDK 11+, unless <c>FRY_REQUIRE_JAVA=1</c>.
/// </summary>
public sealed class JavaFactAttribute : FactAttribute
{
    public JavaFactAttribute()
    {
        if (TestJava.Toolchain == null && !TestJava.Required)
        {
            Skip = "Needs Java JDK 11 or newer (install it, or set FRY_TEST_JAVA to an executable).";
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RealJavaCollection
{
    public const string Name = "RealJava";
}

public static class TestJava
{
    private static readonly Lazy<ToolchainInfo?> Found = new(Find);

    public static bool Required => Environment.GetEnvironmentVariable("FRY_REQUIRE_JAVA") == "1";

    public static ToolchainInfo? Toolchain => Found.Value;

    public static ToolchainInfo Require() => Toolchain ?? throw new InvalidOperationException(
        "FRY_REQUIRE_JAVA is set but no Java JDK 11+ was found.");

    public static StudioLanguageServices Services(string baseDirectory) => new(baseDirectory);

    private static ToolchainInfo? Find()
    {
        var scratch = Path.Combine(Path.GetTempPath(), "FryPDF_TestJava_" + Guid.NewGuid().ToString("N"));
        try
        {
            var services = new StudioLanguageServices(scratch);
            var java = (JavaLanguage)services.Registry.Get(LanguageIds.Java)!;
            var explicitPath = Environment.GetEnvironmentVariable("FRY_TEST_JAVA");
            if (!string.IsNullOrWhiteSpace(explicitPath)) java.JavaToolchain.Select(explicitPath);

            var resolution = java.JavaToolchain.ResolveAsync(new ToolchainQuery()).GetAwaiter().GetResult();
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
