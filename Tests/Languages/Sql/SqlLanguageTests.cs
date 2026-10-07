using AvaloniaEdit;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;
using Xunit;

namespace CSharpEditorPlugin.Tests;

// Reads the shared highlighting definitions, which the theme tests recolour: never at the same time as them.
[Collection("SettingsTests")]
public class SqlLanguageTests : IDisposable
{
    private readonly string _tempDir;
    private readonly StudioLanguageServices _services;

    public SqlLanguageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FryPDF_SqlLangTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _services = new StudioLanguageServices(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void SqlLanguage_IsRegisteredInLanguageServices()
    {
        var lang = _services.Registry.Get(LanguageIds.Sql);
        Assert.NotNull(lang);
        Assert.IsType<SqlLanguage>(lang);
        Assert.Equal("SQL", lang.DisplayName);
        Assert.Equal(LanguageIds.Sql, lang.Id);
    }

    [Fact]
    public void SqlLanguage_ResolvesByAliasesAndExtensions()
    {
        var bySqlExt = _services.Registry.FindByExtension(".sql");
        var byUpper = _services.Registry.FindByExtension(".SQL");
        var byId = _services.Registry.Get("sql");
        var bySqlite = _services.Registry.Get("sqlite");
        var bySqlite3 = _services.Registry.Get("sqlite3");

        Assert.NotNull(bySqlExt);
        Assert.NotNull(byUpper);
        Assert.NotNull(byId);
        Assert.NotNull(bySqlite);
        Assert.NotNull(bySqlite3);

        Assert.Same(byId, bySqlExt);
        Assert.Same(byId, byUpper);
        Assert.Same(byId, bySqlite);
        Assert.Same(byId, bySqlite3);
    }

    [Fact]
    public void SqlLanguage_DeclaresCorrectCapabilities()
    {
        var lang = _services.Registry.Get(LanguageIds.Sql)!;
        Assert.True(lang.Capabilities.HasFlag(LanguageCapabilities.StandardInput));
        Assert.True(lang.Capabilities.HasFlag(LanguageCapabilities.QuickInfo));
        Assert.True(lang.Capabilities.HasFlag(LanguageCapabilities.Completion));
        Assert.True(lang.Capabilities.HasFlag(LanguageCapabilities.NotebookCells));
        Assert.True(lang.Capabilities.HasFlag(LanguageCapabilities.ValueSharing));
        Assert.True(lang.Capabilities.HasFlag(LanguageCapabilities.Packages));
        Assert.True(lang.Capabilities.HasFlag(LanguageCapabilities.Formatting));
        Assert.False(lang.Capabilities.HasFlag(LanguageCapabilities.Debugging));
    }

    [Fact]
    public void SqlLanguage_ProvidesSyntaxHighlightingForDarkAndLight()
    {
        var lang = _services.Registry.Get(LanguageIds.Sql)!;
        var dark = lang.GetHighlighting(isDark: true);
        var light = lang.GetHighlighting(isDark: false);

        Assert.NotNull(dark);
        Assert.NotNull(light);
        Assert.Equal("SQL Dark", dark.Name);
        Assert.Equal("SQL Light", light.Name);
    }

    [Fact]
    public void SqlLanguage_ProvidesIndentationAndFolding()
    {
        var lang = _services.Registry.Get(LanguageIds.Sql)!;
        var indent = lang.CreateIndentationStrategy(new TextEditorOptions());
        var folding = lang.Folding;

        Assert.NotNull(indent);
        Assert.NotNull(folding);
    }

    [Fact]
    public void SqlLanguage_ProvidesNewFileTemplate()
    {
        var lang = _services.Registry.Get(LanguageIds.Sql)!;
        Assert.False(string.IsNullOrWhiteSpace(lang.NewFileTemplate));
        Assert.Contains("CREATE TABLE", lang.NewFileTemplate);
        Assert.Contains("SELECT", lang.NewFileTemplate);
    }

    [Fact]
    public void Sql_WorkspaceItemSummary_MapsToDatabaseIconAndOrangeColor()
    {
        var summary = new WorkspaceItemSummary
        {
            Title = "query.sql",
            LanguageId = LanguageIds.Sql
        };

        Assert.Equal(MaterialIconKind.Database, summary.IconKind);
        Assert.Equal("#F29111", summary.IconForeground);
    }
}
