using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;

/// <summary>
/// A test that runs real Rust (cargo and rustc). Skipped on a machine without Rust, unless <c>FRY_REQUIRE_RUST=1</c>.
/// </summary>
public sealed class RustFactAttribute : FactAttribute
{
    public RustFactAttribute()
    {
        if (TestRust.Toolchain == null && !TestRust.Required)
        {
            Skip = "Needs the Rust toolchain (cargo and rustc). Install it from https://rustup.rs, or set FRY_TEST_RUST to a cargo executable.";
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RealRustCollection
{
    public const string Name = "RealRust";
}

public static class TestRust
{
    private static readonly Lazy<ToolchainInfo?> Found = new(Find);

    public static bool Required => Environment.GetEnvironmentVariable("FRY_REQUIRE_RUST") == "1";

    public static ToolchainInfo? Toolchain => Found.Value;

    public static ToolchainInfo Require() => Toolchain ?? throw new InvalidOperationException(
        "FRY_REQUIRE_RUST is set but no Rust toolchain was found.");

    public static StudioLanguageServices Services(string baseDirectory) => new(baseDirectory);

    private static ToolchainInfo? Find()
    {
        var scratch = Path.Combine(Path.GetTempPath(), "FryPDF_TestRust_" + Guid.NewGuid().ToString("N"));
        try
        {
            var services = new StudioLanguageServices(scratch);
            var rust = (RustLanguage)services.Registry.Get(LanguageIds.Rust)!;
            var explicitPath = Environment.GetEnvironmentVariable("FRY_TEST_RUST");
            if (!string.IsNullOrWhiteSpace(explicitPath)) rust.RustToolchain.Select(explicitPath);

            var resolution = rust.RustToolchain.ResolveAsync(new ToolchainQuery()).GetAwaiter().GetResult();
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
