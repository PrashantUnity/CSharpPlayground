using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>Rust's <c>#![allow(unused)]</c> starts with <c>#!</c> but is code, not one of the notebook's <c>#!</c> directives.</summary>
public class NotebookCellDirectivesRustAttributeTests
{
    private readonly LanguageRegistry _registry =
        new StudioLanguageServices(Path.Combine(Path.GetTempPath(), "FryPDF_RustDirectives_" + Guid.NewGuid().ToString("N"))).Registry;

    [Fact]
    public void AnInnerAttribute_IsCode_NotAnUnknownDirective()
    {
        const string source = "#![allow(unused)]\nfn main() {}";

        var directives = NotebookCellDirectives.Parse(source, _registry, LanguageIds.Rust);

        Assert.Empty(directives.Errors);
        Assert.Null(directives.LanguageId);
        Assert.Equal(source, directives.Code);
    }

    [Fact]
    public void AnInnerAttribute_EndsTheDirectiveLines()
    {
        // The #!python after the attribute is a comment-looking line of code, not a language switch.
        const string source = "#![allow(unused)]\n#!python\nfn main() {}";

        var directives = NotebookCellDirectives.Parse(source, _registry, LanguageIds.Rust);

        Assert.Empty(directives.Errors);
        Assert.Null(directives.LanguageId);
        Assert.Null(NotebookCellDirectives.LanguageOf(source, _registry, LanguageIds.Rust));
    }

    [Fact]
    public void ADirectiveBeforeTheAttribute_StillWorks_AndThePackageLineIsBlanked()
    {
        var directives = NotebookCellDirectives.Parse("#!rust\n%cargo add rand\n#![allow(unused)]\nfn main() {}", _registry, LanguageIds.CSharp);

        Assert.Empty(directives.Errors);
        Assert.Equal(LanguageIds.Rust, directives.LanguageId);
        Assert.Equal(["add", "rand = \"*\""], Assert.Single(directives.PackageCommands).Arguments);
        Assert.Equal("\n\n#![allow(unused)]\nfn main() {}", directives.Code);
    }

    [Fact]
    public void AnUnknownDirective_IsStillAnError_AndAShebangStillIgnored()
    {
        Assert.Single(NotebookCellDirectives.Parse("#!nonsense\nx = 1", _registry, LanguageIds.Python).Errors);
        Assert.Empty(NotebookCellDirectives.Parse("#!/usr/bin/env python\nx = 1", _registry, LanguageIds.Python).Errors);
    }
}
