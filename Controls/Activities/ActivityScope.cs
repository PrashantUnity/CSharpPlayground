using Avalonia;
using Avalonia.Controls;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Common;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Activities;

/// <summary>
/// Hands the studio's <see cref="ActivityPresenterViewModel"/> down the visual tree. The host sets it once on its root;
/// every progress line and status item below finds it by inheritance, so page view models never carry it and a page
/// shown in another host (a test, a tool) simply shows nothing.
/// </summary>
public sealed class ActivityScope : AvaloniaObject
{
    public static readonly AttachedProperty<ActivityPresenterViewModel?> PresenterProperty =
        AvaloniaProperty.RegisterAttached<ActivityScope, Control, ActivityPresenterViewModel?>("Presenter", inherits: true);

    public static ActivityPresenterViewModel? GetPresenter(Control control) => control.GetValue(PresenterProperty);

    public static void SetPresenter(Control control, ActivityPresenterViewModel? value) => control.SetValue(PresenterProperty, value);
}
