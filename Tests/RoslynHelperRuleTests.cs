using AvaloniaEdit;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// The editor's C# (Roslyn) completion and hover must not run on a language that brings its own, or both would pop up on
/// the same text; and must still run on C# and on a language that merely says it has completion.
/// </summary>
public class RoslynHelperRuleTests
{
    private sealed class TestLanguage(LanguageCapabilities capabilities, IEditorAssistantFactory? assistants) : LanguageDefinition
    {
        public override string Id => "test";
        public override string DisplayName => "Test";
        public override IReadOnlyList<string> FileExtensions => [".test"];
        public override LanguageCapabilities Capabilities => capabilities;
        public override IEditorAssistantFactory? EditorAssistants => assistants;
    }

    private sealed class NoAssistants : IEditorAssistantFactory
    {
        public IDisposable Attach(TextEditor editor, EditorAssistantContext context) => throw new NotSupportedException();
    }

    private readonly LanguageRegistry _registry =
        new StudioLanguageServices(Path.Combine(Path.GetTempPath(), "FryPDF_RoslynRule_" + Guid.NewGuid().ToString("N"))).Registry;

    [Theory]
    [InlineData(LanguageCapabilities.Completion)]
    [InlineData(LanguageCapabilities.QuickInfo)]
    public void NoLanguageAtAll_IsTheDefault_CSharp(LanguageCapabilities capability) =>
        Assert.True(((ILanguageDefinition?)null).UsesRoslynHelper(capability));

    [Theory]
    [InlineData(LanguageCapabilities.Completion)]
    [InlineData(LanguageCapabilities.QuickInfo)]
    public void CSharp_UsesTheBuiltInHelpers(LanguageCapabilities capability) =>
        Assert.True(_registry.Get(LanguageIds.CSharp).UsesRoslynHelper(capability));

    [Theory]
    [InlineData(LanguageIds.Python)]
    [InlineData(LanguageIds.JavaScript)]
    public void ALanguageWithoutTheCapability_DoesNot(string languageId)
    {
        var language = _registry.Get(languageId);

        Assert.False(language.UsesRoslynHelper(LanguageCapabilities.Completion));
        Assert.False(language.UsesRoslynHelper(LanguageCapabilities.QuickInfo));
    }

    [Theory]
    [InlineData(LanguageIds.Cpp)]
    [InlineData(LanguageIds.Go)]
    [InlineData(LanguageIds.Java)]
    [InlineData(LanguageIds.Rust)]
    public void ALanguageThatBringsItsOwnAssistants_HasTheBuiltInHelpersStepAside(string languageId)
    {
        var language = _registry.Get(languageId)!;

        Assert.NotNull(language.EditorAssistants);
        Assert.False(language.UsesRoslynHelper(LanguageCapabilities.Completion));
        Assert.False(language.UsesRoslynHelper(LanguageCapabilities.QuickInfo));
    }

    [Fact]
    public void ALanguageThatOnlyClaimsCompletion_KeepsTheBuiltInHelper_UntilItBringsItsOwn()
    {
        var claims = new TestLanguage(LanguageCapabilities.Completion, assistants: null);
        var brings = new TestLanguage(LanguageCapabilities.Completion, new NoAssistants());
        var neither = new TestLanguage(LanguageCapabilities.None, assistants: null);

        Assert.True(claims.UsesRoslynHelper(LanguageCapabilities.Completion));
        Assert.False(claims.UsesRoslynHelper(LanguageCapabilities.QuickInfo));
        Assert.False(brings.UsesRoslynHelper(LanguageCapabilities.Completion));
        Assert.False(neither.UsesRoslynHelper(LanguageCapabilities.Completion));
    }
}
