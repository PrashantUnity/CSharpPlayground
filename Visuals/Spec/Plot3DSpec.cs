namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Spec;

/// <summary>
/// A 3D plot (<c>application/vnd.fry.plot3d.v1+json</c>). Data z points up in every kind, as in matplotlib and Plotly:
/// points and trajectories come in <see cref="Series"/>, a surface's heights in <see cref="Surface"/>, a graph in
/// <see cref="Graph"/>.
/// </summary>
public sealed class Plot3DSpec : VisualSpec
{
    public Plot3DSpec() : base(VisualFamily.Plot3D) { }

    public Plot3DType Kind { get; set; } = Plot3DType.Scatter;

    public AxisSpec XAxis { get; set; } = new();
    public AxisSpec YAxis { get; set; } = new();

    /// <summary>The upward axis.</summary>
    public AxisSpec ZAxis { get; set; } = new();

    /// <summary>The colour scale for heights and values (default: viridis).</summary>
    public ColorMapPreset? ColorMap { get; set; }

    /// <summary>The colour of the first series (a CSS colour).</summary>
    public string? Color { get; set; }

    /// <summary>Turn the camera slowly around the plot (default: off).</summary>
    public bool? AutoRotate { get; set; }

    /// <summary>The x, y and z axes (default: on).</summary>
    public bool? ShowAxes { get; set; }

    /// <summary>The floor grid and the box around the data (default: on).</summary>
    public bool? ShowGrid { get; set; }

    /// <summary>Points for scatter, voxel-bar and trajectory plots.</summary>
    public List<Plot3DSeriesSpec> Series { get; set; } = [];

    /// <summary>Heights for surface and wireframe plots.</summary>
    public SurfaceSpec? Surface { get; set; }

    /// <summary>Nodes and edges for a 3D graph.</summary>
    public GraphSpec? Graph { get; set; }
}

/// <summary>Points as columns: <see cref="X"/>, <see cref="Y"/> and <see cref="Z"/> are equally long; a point with a null coordinate is skipped.</summary>
public sealed class Plot3DSeriesSpec
{
    public string? Name { get; set; }
    public string? Color { get; set; }

    public List<double?> X { get; set; } = [];
    public List<double?> Y { get; set; } = [];
    public List<double?> Z { get; set; } = [];

    public List<string?>? Labels { get; set; }

    /// <summary>A size for each point, 1 being the usual size.</summary>
    public List<double?>? Sizes { get; set; }

    public List<string?>? Colors { get; set; }
}

/// <summary>
/// A surface z = f(x, y) sampled on a grid: <see cref="Z"/> holds one row per y value and one column per x value
/// (numpy's <c>meshgrid</c> order), spread evenly over <see cref="X"/> and <see cref="Y"/>. A null height is a hole.
/// </summary>
public sealed class SurfaceSpec
{
    public RangeSpec X { get; set; } = new(-5, 5);
    public RangeSpec Y { get; set; } = new(-5, 5);
    public List<List<double?>> Z { get; set; } = [];
}
