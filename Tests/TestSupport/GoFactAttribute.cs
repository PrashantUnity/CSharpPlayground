using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using Xunit;

namespace CSharpEditorPlugin.Tests.TestSupport;

/// <summary>
/// A test that runs real Go (go compiler / runtime). Skipped on a machine without Go, unless <c>FRY_REQUIRE_GO=1</c>.
/// </summary>
public sealed class GoFactAttribute : FactAttribute
{
    public GoFactAttribute()
    {
        if (!TimeGate.IsEnabled && !TestGo.Required)
        {
            Skip = TimeGate.SkipReason;
            return;
        }

        if (TestGo.Toolchain == null && !TestGo.Required)
        {
            Skip = "Needs a Go compiler (go). Install it, or set FRY_TEST_GO to an executable.";
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RealGoCollection
{
    public const string Name = "RealGo";
}

public static class TestGo
{
    private static readonly Lazy<ToolchainInfo?> Found = new(Find);

    public static bool Required => Environment.GetEnvironmentVariable("FRY_REQUIRE_GO") == "1";

    public static ToolchainInfo? Toolchain => Found.Value;

    public static ToolchainInfo Require() => Toolchain ?? throw new InvalidOperationException(
        "FRY_REQUIRE_GO is set but no Go toolchain was found.");

    public static StudioLanguageServices Services(string baseDirectory) => new(baseDirectory);

    private static ToolchainInfo? Find()
    {
        var scratch = Path.Combine(Path.GetTempPath(), "FryPDF_TestGo_" + Guid.NewGuid().ToString("N"));
        try
        {
            var services = new StudioLanguageServices(scratch);
            var go = (GoLanguage)services.Registry.Get(LanguageIds.Go)!;
            var explicitPath = Environment.GetEnvironmentVariable("FRY_TEST_GO");
            if (!string.IsNullOrWhiteSpace(explicitPath)) go.GoToolchain.Select(explicitPath);

            var resolution = go.GoToolchain.ResolveAsync(new ToolchainQuery()).GetAwaiter().GetResult();
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
