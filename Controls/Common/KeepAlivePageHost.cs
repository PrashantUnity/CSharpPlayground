using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Common;

/// <summary>
/// Shows one page at a time without ever tearing a page down. A page's view is built the first time its view model
/// becomes <see cref="Page"/>, then stays in the tree and is only shown or hidden afterwards, so going back to a page
/// costs a visibility flip instead of a full <c>InitializeComponent</c>, style load and editor construction (a
/// <c>ContentControl</c> with a <c>DataTemplate</c> rebuilds the view on every switch, and the views it drops stay
/// subscribed to the long-lived view models). Hidden pages are not measured, arranged or rendered.
/// </summary>
public sealed class KeepAlivePageHost : Panel, IDisposable
{
    public static readonly StyledProperty<object?> PageProperty =
        AvaloniaProperty.Register<KeepAlivePageHost, object?>(nameof(Page));

    private readonly Dictionary<Type, Func<Control>> _factories = new();
    private readonly Dictionary<object, Control> _views = new(ReferenceEqualityComparer.Instance);

    public KeepAlivePageHost()
    {
        Focusable = true;
        KeyboardNavigation.SetIsTabStop(this, false);
    }

    /// <summary>The view model of the page to show; <see langword="null"/> shows nothing.</summary>
    public object? Page
    {
        get => GetValue(PageProperty);
        set => SetValue(PageProperty, value);
    }

    /// <summary>How many page views have been built so far (one per page ever shown).</summary>
    public int BuiltViewCount => _views.Count;

    /// <summary>Says which view shows a page view model of type <typeparamref name="TViewModel"/> (or one derived from it).</summary>
    public void Register<TViewModel>(Func<Control> viewFactory) where TViewModel : class
    {
        ArgumentNullException.ThrowIfNull(viewFactory);
        _factories[typeof(TViewModel)] = viewFactory;
    }

    /// <summary>The view already built for <paramref name="page"/>, or <see langword="null"/> if it has never been shown.</summary>
    public Control? ViewFor(object page) => _views.GetValueOrDefault(page);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PageProperty) Show(change.NewValue);
    }

    private void Show(object? page)
    {
        Control? next = null;
        if (page != null && !_views.TryGetValue(page, out next))
        {
            next = Build(page);
        }

        foreach (var child in Children)
        {
            if (ReferenceEquals(child, next) || !child.IsVisible) continue;

            // A hidden control that still holds keyboard focus would keep receiving keystrokes (the old view was
            // detached, which dropped focus; a hidden one is not).
            ReleaseFocusFrom(child);
            child.IsVisible = false;
        }

        if (next != null) next.IsVisible = true;
    }

    private Control Build(object page)
    {
        var factory = FindFactory(page.GetType())
            ?? throw new InvalidOperationException($"No page view is registered for {page.GetType().FullName}.");

        var view = factory();
        view.DataContext = page;
        view.IsVisible = false;
        _views[page] = view;
        Children.Add(view);
        return view;
    }

    private Func<Control>? FindFactory(Type type)
    {
        for (var t = type; t != null; t = t.BaseType)
        {
            if (_factories.TryGetValue(t, out var factory)) return factory;
        }

        return null;
    }

    // The host itself takes the focus (it is focusable, but not a tab stop), so key events still bubble through the
    // studio (its Ctrl+, shortcut keeps working) instead of going to a control that is no longer on screen.
    private void ReleaseFocusFrom(Control view)
    {
        var focusManager = TopLevel.GetTopLevel(this)?.FocusManager;
        if (focusManager?.GetFocusedElement() is Visual focused && view.IsVisualAncestorOf(focused))
        {
            Focus();
        }
    }

    /// <summary>Disposes every page view that asks for it and forgets them all.</summary>
    public void Dispose()
    {
        foreach (var view in _views.Values)
        {
            (view as IDisposable)?.Dispose();
        }

        Children.Clear();
        _views.Clear();
    }
}
