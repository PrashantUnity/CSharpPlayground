using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>The Polyglot-style lines at the top of a notebook cell.</summary>
public class NotebookCellDirectivesTests
{
    private static readonly LanguageRegistry Registry = CreateRegistry();

    private static LanguageRegistry CreateRegistry()
    {
        var registry = new LanguageRegistry();
        registry.Register(new CSharpLanguage());
        FakeLanguage.RegisterIn(registry);
        return registry;
    }

    private static CellDirectives Parse(string source, string defaultLanguage = LanguageIds.CSharp) =>
        NotebookCellDirectives.Parse(source, Registry, defaultLanguage);

    private static int LineCount(string text) => text.Split('\n').Length;

    [Fact]
    public void LanguageLine_PicksTheCellsLanguage_ByIdOrAlias()
    {
        Assert.Equal(FakeLanguage.LanguageId, Parse("#!fakelang\nprint hi").LanguageId);
        Assert.Equal(FakeLanguage.LanguageId, Parse("#!fk\nprint hi").LanguageId);
        Assert.Equal(LanguageIds.CSharp, Parse("#!c#\nvar x = 1;", FakeLanguage.LanguageId).LanguageId);
    }

    [Fact]
    public void DirectiveLines_AreBlanked_SoLineNumbersStillMatch()
    {
        var source = "#!fakelang\n#!share --from csharp nums\nshow nums";
        var directives = Parse(source);

        Assert.Equal(LineCount(source), LineCount(directives.Code));
        Assert.Equal("\n\nshow nums", directives.Code);
    }

    [Fact]
    public void CrLfCells_KeepTheirLineEndings()
    {
        var directives = Parse("#!fakelang\r\nprint hi\r\n");

        Assert.Equal("\r\nprint hi\r\n", directives.Code);
    }

    [Theory]
    [InlineData("#!share --from csharp nums")]
    [InlineData("#!share nums --from csharp")]
    public void Share_ReadsTheSourceAndTheName_InEitherOrder(string line)
    {
        var share = Assert.Single(Parse(line).Shares);

        Assert.Equal("csharp", share.FromLanguage);
        Assert.Equal("nums", share.Name);
        Assert.Null(share.As);
        Assert.Equal("nums", share.TargetName);
        Assert.Equal(1, share.LineNumber);
    }

    [Fact]
    public void Share_CanRenameTheCopy()
    {
        var share = Assert.Single(Parse("#!share --from fakelang total --as sum").Shares);

        Assert.Equal("sum", share.TargetName);
    }

    [Theory]
    [InlineData("#!share nums")]
    [InlineData("#!share --from csharp")]
    [InlineData("#!share --from csharp a b")]
    [InlineData("#!share --from csharp a --colour red")]
    public void MalformedShare_IsAnError_NotSilentlyIgnored(string line) =>
        Assert.Single(Parse(line).Errors);

    [Fact]
    public void UnknownDirective_IsAnError()
    {
        var error = Assert.Single(Parse("#!ruby\nputs 1").Errors);

        Assert.Contains("#!ruby", error);
        Assert.Contains("#!csharp", error);
    }

    [Fact]
    public void Shebang_IsJustAComment()
    {
        var directives = Parse("#!/usr/bin/env fakelang\nprint hi", FakeLanguage.LanguageId);

        Assert.Empty(directives.Errors);
        Assert.Null(directives.LanguageId);
        Assert.Equal("#!/usr/bin/env fakelang\nprint hi", directives.Code);
    }

    [Fact]
    public void OnlyTheLeadingLines_AreDirectives()
    {
        var directives = Parse("var x = 1;\n#!fakelang");

        Assert.Null(directives.LanguageId);
        Assert.Empty(directives.Errors);
        Assert.Equal("var x = 1;\n#!fakelang", directives.Code);
    }

    [Fact]
    public void CommentsAndBlankLines_MaySitBetweenDirectives()
    {
        var directives = Parse("// set up\n\n#!share --from fakelang a\n// more\n#!share --from fakelang b\nConsole.WriteLine(a + b);");

        Assert.Equal(["a", "b"], directives.Shares.Select(s => s.Name));
    }

    [Fact]
    public void PackageCommands_FollowTheCellsLanguage()
    {
        var inFakeCell = Parse("#!fakelang\n%fakepkg install left-pad\nprint ok");
        var command = Assert.Single(inFakeCell.PackageCommands);
        Assert.Equal(["install", "left-pad"], command.Arguments);
        Assert.Equal("\n\nprint ok", inFakeCell.Code);

        // C# has no package manager here, so the same line is just (broken) code for the C# kernel to report.
        var inCSharpCell = Parse("%fakepkg install left-pad");
        Assert.Empty(inCSharpCell.PackageCommands);
        Assert.Equal("%fakepkg install left-pad", inCSharpCell.Code);
    }

    [Fact]
    public void LanguageOf_ReadsTheLeadingDirectives_AsParseDoes()
    {
        Assert.Equal(FakeLanguage.LanguageId, NotebookCellDirectives.LanguageOf("\n  #!fk\nprint 1", Registry, LanguageIds.CSharp));
        Assert.Equal(FakeLanguage.LanguageId, NotebookCellDirectives.LanguageOf("#!share --from csharp x\n#!fk\nprint 1", Registry, LanguageIds.CSharp));
        Assert.Equal(FakeLanguage.LanguageId, NotebookCellDirectives.LanguageOf("-- a comment\n%fakepkg add x\n#!fk", Registry, FakeLanguage.LanguageId));
        Assert.Null(NotebookCellDirectives.LanguageOf("print 1\n#!fk", Registry, LanguageIds.CSharp));
        Assert.Null(NotebookCellDirectives.LanguageOf("#!share --from csharp x", Registry, LanguageIds.CSharp));
        Assert.Null(NotebookCellDirectives.LanguageOf(null, Registry, LanguageIds.CSharp));
        foreach (var source in new[] { "#!fk\nx", "#!share --from csharp x\n#!fk\n", "-- c\n#!csharp\n" })
        {
            Assert.Equal(NotebookCellDirectives.Parse(source, Registry, FakeLanguage.LanguageId).LanguageId, NotebookCellDirectives.LanguageOf(source, Registry, FakeLanguage.LanguageId));
        }
    }
}
