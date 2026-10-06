using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Styling;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming;

/// <summary>
/// The theme engine's layer of resources in a studio root. Its whole content is replaced at once and announced with
/// <b>one</b> change: swapping a <see cref="ResourceDictionary"/> in a root's merged dictionaries announces a removal and
/// then an addition, and in between every dynamic resource falls back to the palette for a moment (twice the work, and a
/// flash of the old colours).
/// </summary>
internal sealed class ThemeLayerProvider : ResourceProvider
{
    private IReadOnlyDictionary<object, object?> _values = new Dictionary<object, object?>();

    public override bool HasResources => _values.Count > 0;

    public override bool TryGetResource(object key, ThemeVariant? theme, out object? value) => _values.TryGetValue(key, out value);

    /// <summary>Replaces everything this layer holds, with one change notification.</summary>
    public void Replace(IReadOnlyDictionary<object, object?> values)
    {
        _values = values;
        RaiseResourcesChanged();
    }
}
