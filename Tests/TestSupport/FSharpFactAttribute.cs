using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using Xunit;

namespace CSharpEditorPlugin.Tests.TestSupport;

/// <summary>
/// A test that runs real F# (dotnet fsi / fsi). Skipped on a machine without F#, unless <c>FRY_REQUIRE_FSHARP=1</c>.
/// </summary>
public sealed class FSharpFactAttribute : FactAttribute
{
    public FSharpFactAttribute()
    {
        if (TestFSharp.Toolchain == null && !TestFSharp.Required)
        {
            Skip = "Needs F# Interactive (dotnet fsi). Install .NET SDK with F#, or set FRY_TEST_FSHARP to an executable.";
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RealFSharpCollection
{
    public const string Name = "RealFSharp";
}

public static class TestFSharp
{
    private static readonly Lazy<ToolchainInfo?> Found = new(Find);

    public static bool Required => Environment.GetEnvironmentVariable("FRY_REQUIRE_FSHARP") == "1";

    public static ToolchainInfo? Toolchain => Found.Value;

    public static ToolchainInfo Require() => Toolchain ?? throw new InvalidOperationException(
        "FRY_REQUIRE_FSHARP is set but no F# toolchain was found.");

    public static StudioLanguageServices Services(string baseDirectory) => new(baseDirectory);

    private static ToolchainInfo? Find()
    {
        var scratch = Path.Combine(Path.GetTempPath(), "FryPDF_TestFSharp_" + Guid.NewGuid().ToString("N"));
        try
        {
            var services = new StudioLanguageServices(scratch);
            var fs = (FSharpLanguage)services.Registry.Get(LanguageIds.FSharp)!;
            var explicitPath = Environment.GetEnvironmentVariable("FRY_TEST_FSHARP");
            if (!string.IsNullOrWhiteSpace(explicitPath)) fs.FSharpToolchain.Select(explicitPath);

            var resolution = fs.FSharpToolchain.ResolveAsync(new ToolchainQuery()).GetAwaiter().GetResult();
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
                if (Directory.Exists(scratch)) Directory.Delete(scratch, true);
            }
            catch (IOException)
            {
            }
        }
    }
}
