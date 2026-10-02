using Avalonia;
using Avalonia.Controls;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Common;

/// <summary>
/// Content that is built the first time it is needed. XAML builds everything it declares up front, and hiding something
/// with <c>IsVisible</c> does not stop that: a notebook cell that only printed text still built a table, a chart, a 3D plot,
/// an object inspector and an HTML view, all invisible, and that was most of the cost of showing a cell.
/// Put the heavy element in <see cref="ContentControl.ContentTemplate"/> and bind <see cref="IsActive"/> to "this kind of
/// output is showing": the template is instantiated (with the <see cref="StyledElement.DataContext"/> as its data) the first
/// time <see cref="IsActive"/> becomes true, and kept from then on, following the DataContext when the host is recycled
/// for another item.
/// </summary>
public sealed class LazyContent : ContentControl
{
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<LazyContent, bool>(nameof(IsActive));

    private bool _built;

    /// <summary>True while the content is wanted; the first time it is, the content is built.</summary>
    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsActiveProperty || change.Property == DataContextProperty) Sync();
    }

    private void Sync()
    {
        if (IsActive) _built = true;
        Content = _built ? DataContext : null;
    }
}
