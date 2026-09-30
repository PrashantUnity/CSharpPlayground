namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;

/// <summary>
/// The MIME types that carry visual specs in a display bundle (Jupyter's vendor-tree convention; the major version is
/// part of the name, as in <c>application/vnd.vegalite.v5+json</c>).
/// </summary>
public static class VisualMimeTypes
{
    public const string Chart = "application/vnd.fry.chart.v1+json";
    public const string Plot3D = "application/vnd.fry.plot3d.v1+json";
    public const string Visualizer = "application/vnd.fry.visualizer.v1+json";

    /// <summary>The studio's table (<c>{title, columns, numeric, rows, totalRows, totalColumns}</c>).</summary>
    public const string Table = "application/vnd.fry.table+json";

    /// <summary>The first 3D format, <c>{title, type, points}</c>: still read, never written.</summary>
    public const string LegacyPlot3D = "application/vnd.fry.plot3d+json";

    /// <summary>The visual types, richest first: the order a bundle is searched in.</summary>
    public static IReadOnlyList<string> VisualTypes { get; } = [Chart, Plot3D, Visualizer];

    public static string For(VisualFamily family) => family switch
    {
        VisualFamily.Chart => Chart,
        VisualFamily.Plot3D => Plot3D,
        VisualFamily.Visualizer => Visualizer,
        _ => throw new ArgumentOutOfRangeException(nameof(family), family, null)
    };

    /// <summary>The major version of the specs this version of the studio reads and writes.</summary>
    public const int CurrentVersion = 1;

    private const string Prefix = "application/vnd.fry.";
    private const string Suffix = "+json";

    /// <summary>The family of a MIME type this version reads.</summary>
    public static bool TryGetFamily(string? mimeType, out VisualFamily family) =>
        TryParse(mimeType, out family, out var version) && version == CurrentVersion;

    /// <summary>
    /// The family and major version of any visual's MIME type (<c>application/vnd.fry.{family}.v{N}+json</c>), including
    /// versions newer than this one reads.
    /// </summary>
    public static bool TryParse(string? mimeType, out VisualFamily family, out int version)
    {
        family = default;
        version = 0;
        if (mimeType == null || !mimeType.StartsWith(Prefix, StringComparison.Ordinal) || !mimeType.EndsWith(Suffix, StringComparison.Ordinal)) return false;

        var name = mimeType.AsSpan(Prefix.Length, mimeType.Length - Prefix.Length - Suffix.Length);
        var dot = name.LastIndexOf(".v", StringComparison.Ordinal);
        if (dot <= 0 || !int.TryParse(name[(dot + 2)..], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out version) || version < 1) return false;

        switch (name[..dot])
        {
            case "chart": family = VisualFamily.Chart; return true;
            case "plot3d": family = VisualFamily.Plot3D; return true;
            case "visualizer": family = VisualFamily.Visualizer; return true;
            default: version = 0; return false;
        }
    }
}
