using Avalonia.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class KeepAlivePageHostTests
{
    private class PageA;
    private sealed class PageB;
    private sealed class DerivedPageA : PageA;

    private sealed class FakeView : Control, IDisposable
    {
        public bool WasDisposed { get; private set; }
        public void Dispose() => WasDisposed = true;
    }

    private sealed class Counter
    {
        public int Built;
        public Func<Control> Factory => () => { Built++; return new FakeView(); };
    }

    private static (KeepAlivePageHost Host, Counter A, Counter B) NewHost()
    {
        var host = new KeepAlivePageHost();
        var a = new Counter();
        var b = new Counter();
        host.Register<PageA>(a.Factory);
        host.Register<PageB>(b.Factory);
        return (host, a, b);
    }

    [Fact]
    public void ShowingAPage_BuildsItsViewOnce_AndReusesItOnEveryLaterVisit()
    {
        var (host, a, b) = NewHost();
        var pageA = new PageA();
        var pageB = new PageB();

        host.Page = pageA;
        var firstView = host.ViewFor(pageA);
        host.Page = pageB;
        host.Page = pageA;
        host.Page = pageB;
        host.Page = pageA;

        Assert.Equal(1, a.Built);
        Assert.Equal(1, b.Built);
        Assert.Equal(2, host.BuiltViewCount);
        Assert.Same(firstView, host.ViewFor(pageA));
    }

    [Fact]
    public void OnlyTheCurrentPageIsVisible_TheRestStayInTheTreeHidden()
    {
        var (host, _, _) = NewHost();
        var pageA = new PageA();
        var pageB = new PageB();

        host.Page = pageA;
        host.Page = pageB;

        Assert.Equal(2, host.Children.Count);
        Assert.False(host.ViewFor(pageA)!.IsVisible);
        Assert.True(host.ViewFor(pageB)!.IsVisible);

        host.Page = pageA;

        Assert.True(host.ViewFor(pageA)!.IsVisible);
        Assert.False(host.ViewFor(pageB)!.IsVisible);
    }

    [Fact]
    public void TheViewGetsItsPageViewModelAsDataContext()
    {
        var (host, _, _) = NewHost();
        var pageA = new PageA();

        host.Page = pageA;

        Assert.Same(pageA, host.ViewFor(pageA)!.DataContext);
    }

    [Fact]
    public void ANullPageHidesEverything_WithoutBuildingAnything()
    {
        var (host, _, _) = NewHost();
        var pageA = new PageA();
        host.Page = pageA;

        host.Page = null;

        Assert.False(host.ViewFor(pageA)!.IsVisible);
        Assert.Equal(1, host.BuiltViewCount);
    }

    [Fact]
    public void APageWithNoRegisteredView_Throws_InsteadOfShowingNothingSilently()
    {
        var host = new KeepAlivePageHost();

        var ex = Assert.Throws<InvalidOperationException>(() => host.Page = new PageB());

        Assert.Contains(nameof(PageB), ex.Message);
    }

    [Fact]
    public void ADerivedViewModel_IsShownByItsBaseTypesView()
    {
        var (host, a, _) = NewHost();

        host.Page = new DerivedPageA();

        Assert.Equal(1, a.Built);
    }

    [Fact]
    public void Dispose_DisposesEveryDisposableView_AndForgetsThemAll()
    {
        var (host, _, _) = NewHost();
        var pageA = new PageA();
        var pageB = new PageB();
        host.Page = pageA;
        host.Page = pageB;
        var views = new[] { host.ViewFor(pageA)!, host.ViewFor(pageB)! };

        host.Dispose();

        Assert.All(views, v => Assert.True(((FakeView)v).WasDisposed));
        Assert.Empty(host.Children);
        Assert.Equal(0, host.BuiltViewCount);
    }
}

public class PageLifecycleTests
{
    private sealed class FakePage : IPageLifecycle
    {
        public int Activated;
        public int Deactivated;
        public void OnActivated() => Activated++;
        public void OnDeactivated() => Deactivated++;
    }

    private sealed class NoProgress : IBlindProgressService
    {
        public event Action<int, bool>? SolvedStatusChanged { add { } remove { } }
        public event Action<int, bool>? BookmarkStatusChanged { add { } remove { } }
        public Task<IReadOnlySet<int>> GetSolvedProblemNumbersAsync() => Task.FromResult<IReadOnlySet<int>>(new HashSet<int>());
        public Task<IReadOnlySet<int>> GetBookmarkedProblemNumbersAsync() => Task.FromResult<IReadOnlySet<int>>(new HashSet<int>());
        public Task SetProblemSolvedAsync(int problemNumber, bool isSolved) => Task.CompletedTask;
        public Task SetProblemBookmarkedAsync(int problemNumber, bool isBookmarked) => Task.CompletedTask;
        public bool IsProblemSolved(int problemNumber) => false;
        public bool IsProblemBookmarked(int problemNumber) => false;
    }

    [Fact]
    public void ChangingTheCurrentPage_DeactivatesThePageLeftAndActivatesThePageShown()
    {
        var host = new CSharpStudioHostViewModel(blindProgress: new NoProgress());
        var first = new FakePage();
        var second = new FakePage();

        host.CurrentPage = first;
        host.CurrentPage = second;
        host.CurrentPage = first;

        Assert.Equal(2, first.Activated);
        Assert.Equal(1, first.Deactivated);
        Assert.Equal(1, second.Activated);
        Assert.Equal(1, second.Deactivated);
    }

    [Fact]
    public void ReselectingTheSamePage_DoesNotRestartIt()
    {
        var host = new CSharpStudioHostViewModel(blindProgress: new NoProgress());
        var page = new FakePage();
        host.CurrentPage = page;

        host.CurrentPage = page;

        Assert.Equal(1, page.Activated);
        Assert.Equal(0, page.Deactivated);
    }

    [Fact]
    public void TheHubViewModel_TakesPartInTheLifecycle()
    {
        var host = new CSharpStudioHostViewModel(blindProgress: new NoProgress());

        Assert.IsAssignableFrom<IPageLifecycle>(host.ManagerViewModel);
        // Leaving and coming back must not throw whether or not a dispatcher timer could be created.
        host.NavigateToDocs();
        host.NavigateToManager();
        Assert.Same(host.ManagerViewModel, host.CurrentPage);
    }
}
