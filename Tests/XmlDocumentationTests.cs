using System.Collections.Generic;
using System.IO;
using System.Linq;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>
/// XML documentation comments as the hover draws them (<see cref="XmlDocumentationParser"/>), and finding the
/// documentation files of referenced assemblies (<see cref="XmlDocumentationLookup"/>).
/// </summary>
public class XmlDocumentationTests
{
    private static string Text(IEnumerable<QuickInfoTextRun> runs) => string.Concat(runs.Select(r => r.Text));

    [Fact]
    public void Summary_CollapsesIndentationIntoSingleSpaces()
    {
        var docs = XmlDocumentationParser.Parse("""
            <summary>
                Splits a string into
                substrings.
            </summary>
            """);

        Assert.Equal("Splits a string into substrings.", Text(docs!.Summary));
    }

    [Fact]
    public void SourceMemberElement_AndMetadataFragment_ParseTheSame()
    {
        const string inner = "<summary>Adds.</summary><returns>The sum.</returns>";

        var fromMetadata = XmlDocumentationParser.Parse(inner)!;
        var fromSource = XmlDocumentationParser.Parse($"<member name=\"M:Calc.Add\">{inner}</member>")!;

        Assert.Equal(Text(fromMetadata.Summary), Text(fromSource.Summary));
        Assert.Equal("The sum.", Text(fromSource.Returns));
    }

    [Fact]
    public void References_UseTheResolverAndFallBackToAShortName()
    {
        var docs = XmlDocumentationParser.Parse(
            """<summary>Wraps <see cref="T:System.Collections.Generic.List`1"/>, not <see cref="M:Foo.Bar(System.Int32)"/>; <see langword="null"/> for <paramref name="x"/>.</summary>""",
            cref => cref.StartsWith("T:") ? new QuickInfoTextRun("List<T>", QuickInfoTextKind.Type) : null)!;

        Assert.Equal("Wraps List<T>, not Foo.Bar; null for x.", Text(docs.Summary));
        Assert.Contains(docs.Summary, r => r is { Text: "List<T>", Kind: QuickInfoTextKind.Type });
        Assert.Contains(docs.Summary, r => r is { Text: "null", Kind: QuickInfoTextKind.Keyword });
        Assert.Contains(docs.Summary, r => r is { Text: "x", Kind: QuickInfoTextKind.Variable });
    }

    [Fact]
    public void ParagraphsListsAndCodeBlocks_BecomeLineBreaks()
    {
        var docs = XmlDocumentationParser.Parse("""
            <remarks>
              First.<para>Second.</para>
              <list type="bullet">
                <item><term>A</term><description>one</description></item>
                <item><description>two</description></item>
              </list>
              <code>
                var x = 1;
                  x++;
              </code>
            </remarks>
            """)!;

        Assert.Equal("First.\n\nSecond.\n\n• A – one\n• two\n\nvar x = 1;\n  x++;", Text(docs.Remarks));
        Assert.Contains(docs.Remarks, r => r.Kind == QuickInfoTextKind.CodeBlock);
    }

    [Fact]
    public void NamedSections_KeepTheirNamesAndKinds()
    {
        var docs = XmlDocumentationParser.Parse("""
            <typeparam name="T">The element type.</typeparam>
            <param name="count">How many.</param>
            <exception cref="T:System.ArgumentNullException">When it's null.</exception>
            """)!;

        Assert.Equal(new QuickInfoTextRun("T", QuickInfoTextKind.TypeParameter), docs.TypeParameters.Single().Name);
        Assert.Equal(new QuickInfoTextRun("count", QuickInfoTextKind.Variable), docs.Parameters.Single().Name);
        Assert.Equal("ArgumentNullException", docs.Exceptions.Single().Name.Text);
        Assert.Equal("How many.", Text(docs.Parameters.Single().Text));
    }

    [Fact]
    public void InheritDoc_IsReported_AndMergeFillsOnlyMissingSections()
    {
        var own = XmlDocumentationParser.Parse("<inheritdoc cref=\"M:Base.Run\"/><returns>Mine.</returns>")!;
        var inherited = XmlDocumentationParser.Parse("<summary>From the base.</summary><returns>Theirs.</returns>")!;

        var merged = XmlDocumentationParser.Merge(own, inherited);

        Assert.Equal("M:Base.Run", own.InheritDocCref);
        Assert.Equal("From the base.", Text(merged.Summary));
        Assert.Equal("Mine.", Text(merged.Returns));
        Assert.Equal(string.Empty, XmlDocumentationParser.Parse("<inheritdoc/>")!.InheritDocCref);
        Assert.Null(XmlDocumentationParser.Parse("<summary>No inheritance.</summary>")!.InheritDocCref);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<summary>unclosed")]
    public void EmptyOrMalformedXml_ParsesToNull(string? xml)
    {
        Assert.Null(XmlDocumentationParser.Parse(xml));
    }

    [Theory]
    [InlineData("T:System.Collections.Generic.List`1", "List<T>")]
    [InlineData("T:System.Collections.Generic.Dictionary`2", "Dictionary<T1, T2>")]
    [InlineData("M:System.String.Split(System.Char[])", "String.Split")]
    [InlineData("M:System.String.#ctor(System.Char[])", "String")]
    [InlineData("M:System.Linq.Enumerable.Where``1(System.Func{``0,System.Boolean})", "Enumerable.Where<T>")]
    [InlineData("P:System.Collections.Generic.List`1.Count", "List<T>.Count")]
    [InlineData("!:Unresolved", "Unresolved")]
    public void CrefToDisplayName_ShortensDocumentationIds(string cref, string expected)
    {
        Assert.Equal(expected, XmlDocumentationParser.CrefToDisplayName(cref));
    }

    [Theory]
    [InlineData("M:System.Collections.Generic.List`1.Add(`0)", "T:System.Collections.Generic.List`1")]
    [InlineData("M:System.Decimal.op_Implicit(System.Byte)~System.Decimal", "T:System.Decimal")]
    [InlineData("F:System.Environment.SpecialFolder.Desktop", "T:System.Environment.SpecialFolder")]
    [InlineData("T:System.String", "T:System.String")]
    [InlineData("N:System.Linq", null)]
    public void ContainingTypeId_FindsTheDeclaringType(string documentationId, string? expected)
    {
        Assert.Equal(expected, XmlDocumentationLookup.ContainingTypeId(documentationId));
    }

    [Fact]
    public void FrameworkTypesImplementedInCoreLib_FindTheirDocumentationInTheReferencePack()
    {
        // List<T> is compiled into System.Private.CoreLib but documented in System.Collections.xml.
        var coreLib = typeof(object).Assembly.Location;

        var xml = XmlDocumentationLookup.FindDocumentation(coreLib, "M:System.Collections.Generic.List`1.Add(`0)");

        Assert.NotNull(xml);
        Assert.Contains("<summary>", xml);
    }

    [Fact]
    public void SiblingXmlFile_IsUsedForTheAssemblyNextToIt()
    {
        var plugin = typeof(ScriptHelpers).Assembly.Location;
        Assert.True(File.Exists(Path.ChangeExtension(plugin, ".xml")), "The plugin should ship CSharpEditorPlugin.xml next to its DLL.");

        var xml = XmlDocumentationLookup.FindDocumentation(plugin, "T:PdfEditorApp.Plugins.CSharpEditor.Services.ScriptHelpers");

        Assert.Contains("using static", xml);
    }

    [Fact]
    public void UnknownMember_HasNoDocumentation()
    {
        Assert.Null(XmlDocumentationLookup.FindDocumentation(typeof(object).Assembly.Location, "M:System.NotAType.NotAMember"));
    }
}
