using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.JavaScript;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;

/// <summary>
/// A test that runs real Node.js. Skipped on a machine without Node.js 18+, unless <c>FRY_REQUIRE_JAVASCRIPT=1</c>.
/// </summary>
public sealed class JavaScriptFactAttribute : FactAttribute
{
    public JavaScriptFactAttribute()
    {
        if (TestJavaScript.Toolchain == null && !TestJavaScript.Required)
        {
            Skip = "Needs Node.js 18 or newer (install it, or set FRY_TEST_NODE to an executable).";
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RealJavaScriptCollection
{
    public const string Name = "RealJavaScript";
}

public static class TestJavaScript
{
    private static readonly Lazy<ToolchainInfo?> Found = new(Find);

    public static bool Required => Environment.GetEnvironmentVariable("FRY_REQUIRE_JAVASCRIPT") == "1";

    public static ToolchainInfo? Toolchain => Found.Value;

    public static ToolchainInfo Require() => Toolchain ?? throw new InvalidOperationException(
        "FRY_REQUIRE_JAVASCRIPT is set but no Node.js 18+ was found.");

    public static StudioLanguageServices Services(string baseDirectory) => new(baseDirectory);

    private static ToolchainInfo? Find()
    {
        var scratch = Path.Combine(Path.GetTempPath(), "FryPDF_TestJS_" + Guid.NewGuid().ToString("N"));
        try
        {
            var services = new StudioLanguageServices(scratch);
            var js = (JavaScriptLanguage)services.Registry.Get(LanguageIds.JavaScript)!;
            var explicitPath = Environment.GetEnvironmentVariable("FRY_TEST_NODE");
            if (!string.IsNullOrWhiteSpace(explicitPath)) js.JavaScriptToolchain.Select(explicitPath);

            var resolution = js.JavaScriptToolchain.ResolveAsync(new ToolchainQuery()).GetAwaiter().GetResult();
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
