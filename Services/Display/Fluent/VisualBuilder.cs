using PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

/// <summary>
/// A visual being put together one setting at a time: <c>Charts.Line(sales).Title("Sales").YLabel("USD").Show()</c>. It
/// builds the same spec <see cref="Display"/> does, so what it draws is what the same call on <see cref="Display"/> draws.
/// <see cref="Show"/> draws it; so does returning it as a cell's last value, or <c>Display.Show(builder)</c>. Once drawn it
/// can't be changed: use the handle's <c>Update</c> for that.
/// </summary>
/// <typeparam name="TBuilder">The builder, so each setting returns it.</typeparam>
/// <typeparam name="TSpec">The spec being built.</typeparam>
public abstract class VisualBuilder<TBuilder, TSpec> : IVisualSource
    where TBuilder : VisualBuilder<TBuilder, TSpec>
    where TSpec : VisualSpec
{
    private readonly List<Action<TSpec>> _configure = [];
    private bool _shown;

    private protected VisualBuilder(TSpec spec) => Spec = spec;

    /// <summary>The spec so far. Read it; the settings change it.</summary>
    public TSpec Spec { get; }

    /// <summary>The title above the visual.</summary>
    public TBuilder Title(string title) => Edit(s => s.Title = title);

    /// <summary>A line under the title.</summary>
    public TBuilder Subtitle(string subtitle) => Edit(s => s.Subtitle = subtitle);

    /// <summary>The widest and the tallest the drawing is, in pixels.</summary>
    public TBuilder Size(double width, double height) => Edit(s => (s.Width, s.Height) = (width, height));

    /// <summary>The widest the visual is drawn, in pixels.</summary>
    public TBuilder Width(double width) => Edit(s => s.Width = width);

    /// <summary>The drawing's height in pixels.</summary>
    public TBuilder Height(double height) => Edit(s => s.Height = height);

    /// <summary>Changes anything in the spec the settings don't reach; runs last, when the visual is drawn.</summary>
    public TBuilder Configure(Action<TSpec> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        EnsureOpen();
        _configure.Add(configure);
        return This;
    }

    /// <summary>
    /// Draws the visual and returns its handle: <c>handle.Update(...)</c>, <c>handle.OnClick(...)</c>. A visual that can't be
    /// drawn as described shows an error that says where, and the handle's <c>IsShown</c> is false.
    /// </summary>
    public DisplayHandle<TSpec> Show() => Display.Show(Close());

    VisualSpec IVisualSource.ToVisualSpec() => Close();

    private protected TBuilder This => (TBuilder)this;

    private protected TBuilder Edit(Action<TSpec> change)
    {
        EnsureOpen();
        change(Spec);
        return This;
    }

    private protected void EnsureOpen()
    {
        if (_shown) throw new InvalidOperationException("This visual is already shown, so it can't be changed here: use the handle's Update to change what is drawn.");
    }

    private TSpec Close()
    {
        if (_shown) return Spec;
        foreach (var configure in _configure) configure(Spec);
        _shown = true;
        return Spec;
    }
}
