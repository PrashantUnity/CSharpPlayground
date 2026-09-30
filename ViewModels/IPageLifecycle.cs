namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

/// <summary>
/// A page view model that hears when its page is shown and hidden. Pages are built once and kept in the visual tree
/// (see <see cref="Controls.KeepAlivePageHost"/>), so leaving a page no longer detaches it: anything that should only
/// run while the page is on screen (timers, polling, animations) starts in <see cref="OnActivated"/> and stops in
/// <see cref="OnDeactivated"/>.
/// </summary>
public interface IPageLifecycle
{
    /// <summary>The page has just become the visible one.</summary>
    void OnActivated();

    /// <summary>The page has just been replaced by another; it stays alive, hidden.</summary>
    void OnDeactivated();
}
