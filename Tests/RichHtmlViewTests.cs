using System.Linq;
using Avalonia.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Views;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class RichHtmlViewTests
{
    private static StackPanel GetRootPanel(RichHtmlView view) =>
        view.FindControl<StackPanel>("RootPanel")!;

    [Fact]
    public void Construction_WithNoDataContext_DoesNotThrowAndRendersNothing()
    {
        var view = new RichHtmlView();
        Assert.Empty(GetRootPanel(view).Children);
    }

    [Fact]
    public void SetDataContext_NullOrWhitespace_RendersNothing()
    {
        var view = new RichHtmlView { DataContext = "   " };
        Assert.Empty(GetRootPanel(view).Children);
    }

    [Fact]
    public void SimpleParagraph_RendersOneBlock()
    {
        var view = new RichHtmlView { DataContext = "<p>Hello world</p>" };
        Assert.Single(GetRootPanel(view).Children);
    }

    [Fact]
    public void HeadingsAndParagraphs_RenderOneBlockEach()
    {
        var view = new RichHtmlView
        {
            DataContext = "<h1>Title</h1><p>First paragraph.</p><p>Second paragraph.</p>"
        };
        Assert.Equal(3, GetRootPanel(view).Children.Count);
    }

    [Fact]
    public void UnorderedList_RendersListPanelWithOneLinePerItem()
    {
        var view = new RichHtmlView
        {
            DataContext = "<ul><li>One</li><li>Two</li><li>Three</li></ul>"
        };
        var root = GetRootPanel(view);
        var listPanel = Assert.IsType<StackPanel>(Assert.Single(root.Children));
        Assert.Equal(3, listPanel.Children.Count);
    }

    [Fact]
    public void InlineFormatting_BoldItalicCodeAndLink_DoesNotThrow()
    {
        var view = new RichHtmlView
        {
            DataContext = "<p>Some <b>bold</b>, <i>italic</i>, <code>code</code> and a " +
                          "<a href=\"https://example.com\">link</a>.</p>"
        };
        Assert.Single(GetRootPanel(view).Children);
    }

    [Fact]
    public void LineBreak_InsideParagraph_DoesNotThrow()
    {
        var view = new RichHtmlView { DataContext = "<p>Line one<br/>Line two</p>" };
        Assert.Single(GetRootPanel(view).Children);
    }

    [Fact]
    public void LooseTextWithNoBlockTags_StillRendersAsParagraph()
    {
        var view = new RichHtmlView { DataContext = "Just plain text, no tags at all." };
        Assert.Single(GetRootPanel(view).Children);
    }

    [Fact]
    public void UnclosedAndMismatchedTags_DoNotThrow()
    {
        var view = new RichHtmlView
        {
            DataContext = "<p>Unclosed bold: <b>oops <i>nested</p><div>stray block tag</div>"
        };
        var exception = Record.Exception(() => GetRootPanel(view));
        Assert.Null(exception);
    }

    [Fact]
    public void UnknownTags_AreSwallowedNotLeakedAsText()
    {
        var view = new RichHtmlView
        {
            DataContext = "<p>Before <span class=\"x\">middle</span> after</p>"
        };
        var root = GetRootPanel(view);
        var tb = Assert.IsType<SelectableTextBlock>(Assert.Single(root.Children));
        var text = string.Concat(tb.Inlines!.Select(i => (i as Avalonia.Controls.Documents.Run)?.Text));
        Assert.DoesNotContain("span", text);
        Assert.Contains("middle", text);
    }

    [Fact]
    public void MalformedInput_NeverThrows_EvenIfParsingFails()
    {
        var pathological = "<h1>Unclosed<p><li>orphan li outside ul<b><i><code>" +
                            new string('<', 50) + new string('>', 5);
        var exception = Record.Exception(() => new RichHtmlView { DataContext = pathological });
        Assert.Null(exception);
    }

    [Fact]
    public void ReassigningDataContext_RebuildsFromScratch()
    {
        var view = new RichHtmlView { DataContext = "<p>First</p><p>Second</p>" };
        Assert.Equal(2, GetRootPanel(view).Children.Count);

        view.DataContext = "<p>Only one now</p>";
        Assert.Single(GetRootPanel(view).Children);

        view.DataContext = null;
        Assert.Empty(GetRootPanel(view).Children);
    }
}
