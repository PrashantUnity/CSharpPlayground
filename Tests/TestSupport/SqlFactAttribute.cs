using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;

/// <summary>
/// A test that runs real SQLite CLI (sqlite3). Skipped on a machine without sqlite3, unless <c>FRY_REQUIRE_SQL=1</c>.
/// </summary>
public sealed class SqlFactAttribute : FactAttribute
{
    public SqlFactAttribute()
    {
        if (TestSql.Toolchain == null && !TestSql.Required)
        {
            Skip = "Needs SQLite CLI (sqlite3). Install sqlite3, or set FRY_TEST_SQL to an executable.";
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RealSqlCollection
{
    public const string Name = "RealSql";
}

public static class TestSql
{
    private static readonly Lazy<ToolchainInfo?> Found = new(Find);

    public static bool Required => Environment.GetEnvironmentVariable("FRY_REQUIRE_SQL") == "1";

    public static ToolchainInfo? Toolchain => Found.Value;

    public static ToolchainInfo Require() => Toolchain ?? throw new InvalidOperationException(
        "FRY_REQUIRE_SQL is set but no SQLite CLI toolchain was found.");

    public static StudioLanguageServices Services(string baseDirectory) => new(baseDirectory);

    private static ToolchainInfo? Find()
    {
        var scratch = Path.Combine(Path.GetTempPath(), "FryPDF_TestSql_" + Guid.NewGuid().ToString("N"));
        try
        {
            var services = new StudioLanguageServices(scratch);
            var sql = (SqlLanguage)services.Registry.Get(LanguageIds.Sql)!;
            var explicitPath = Environment.GetEnvironmentVariable("FRY_TEST_SQL");
            if (!string.IsNullOrWhiteSpace(explicitPath)) sql.SqlToolchain.Select(explicitPath);

            var resolution = sql.SqlToolchain.ResolveAsync(new ToolchainQuery()).GetAwaiter().GetResult();
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
            catch (IOException) { }
        }
    }
}
