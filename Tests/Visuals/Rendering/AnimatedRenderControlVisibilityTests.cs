using Avalonia.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Controls.Visuals;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class AnimatedRenderControlVisibilityTests
{
    [Fact]
    public void ANewControl_DoesNotTickUntilItIsOnScreen()
    {
        var control = new AnimatedRenderControl((_, _) => { }, interval: TimeSpan.FromMilliseconds(50));

        Assert.False(control.IsTicking);
        control.Dispose();
    }

    [Fact]
    public void Dispose_StopsItForGood_EvenIfItIsShownAgain()
    {
        var control = new AnimatedRenderControl((_, _) => { }, interval: TimeSpan.FromMilliseconds(50));

        control.Dispose();
        control.IsVisible = false;
        control.IsVisible = true;

        Assert.False(control.IsTicking);
    }

    [Fact]
    public void HidingItsPage_DoesNotStartAnything()
    {
        // A hidden (kept-alive) page must not wake its animations up: hiding and showing while detached stays idle.
        var page = new Panel();
        var control = new AnimatedRenderControl((_, _) => { }, interval: TimeSpan.FromMilliseconds(50));
        page.Children.Add(control);

        page.IsVisible = false;
        page.IsVisible = true;

        Assert.False(control.IsTicking);
        control.Dispose();
    }
}
