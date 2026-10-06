using Avalonia.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Controls.Docs;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// The Code Studio keeps a Markdown preview bound to the open document's text, hidden unless the document is Markdown.
/// A hidden preview must not render: it used to render every opened CSV or log (and every keystroke in it) as Markdown,
/// which took minutes for a 1 MB CSV.
/// </summary>
public class HiddenPreviewTests
{
    [Fact]
    public void AHiddenMarkdownView_RendersNothing_WhileItIsHidden()
    {
        var host = new Panel { IsVisible = false };
        var view = new MarkdownView();
        host.Children.Add(view);

        for (int i = 0; i < 5; i++) view.Markdown = $"# Heading {i}\n\n" + string.Join("\n", Enumerable.Range(0, 2000).Select(n => $"{n},value {n}"));
        Assert.Equal(0, view.RenderCount);

        // Shown again (in the studio the effective-visibility change renders it; outside a window the next text does).
        host.IsVisible = true;
        view.Markdown = "# Visible now";
        Assert.Equal(1, view.RenderCount);
        Assert.IsType<StackPanel>(view.Content);
    }

    [Fact]
    public void AMarkdownView_InsideAHiddenControlThatWasNeverLaidOut_RendersNothing()
    {
        // The studio's preview: a hidden control whose ScrollViewer never got its template, so the view has a logical
        // parent but no visual one.
        var scroller = new ScrollViewer();
        var hidden = new ContentControl { IsVisible = false, Content = scroller };
        var view = new MarkdownView();
        scroller.Content = view;

        view.Markdown = string.Join("\n", Enumerable.Range(0, 3000).Select(n => $"{n},value {n}"));

        Assert.Equal(0, view.RenderCount);
        Assert.NotNull(hidden);
    }

    [Fact]
    public void AVisibleMarkdownView_StillRendersAtOnce()
    {
        var view = new MarkdownView { Markdown = "**bold**" };
        Assert.Equal(1, view.RenderCount);
        Assert.IsType<StackPanel>(view.Content);
    }
}
