using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Output;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Rendering;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.Notebooks;

/// <summary>
/// A cell's charts, 3D plots and visualizers: every one it showed, in order, each drawn by the shared visual view and
/// saved with the notebook as its spec.
/// </summary>
public partial class NotebookCellViewModel
{
    // How many visuals the cell produced, including any past the limit.
    private int _visualsProduced;

    /// <summary>Every visual, in the order the cell showed them.</summary>
    public ObservableCollection<VisualOutput> Visuals { get; } = [];

    public ObservableCollection<VisualOutput> ChartVisuals { get; } = [];
    public ObservableCollection<VisualOutput> Plot3DVisuals { get; } = [];
    public ObservableCollection<VisualOutput> VisualizerVisuals { get; } = [];

    /// <summary>Said when the cell showed more visuals than it keeps.</summary>
    [ObservableProperty]
    private string? _visualsNotice;

    public bool HasChartOutput => ChartVisuals.Count > 0;
    public bool HasPlot3DOutput => Plot3DVisuals.Count > 0;
    public bool HasVisualizerOutput => VisualizerVisuals.Count > 0;

    /// <summary>Shows a visual (a cell keeps at most <see cref="VisualLimits.MaxVisualsPerCell"/>) and saves it with the notebook.</summary>
    public void AddVisual(VisualOutput visual)
    {
        ArgumentNullException.ThrowIfNull(visual);
        if (!Keep(visual)) return;

        (Model.Visuals ??= []).Add(VisualOutputSnapshot.From(visual));
        // Written out now in the background, so saving the notebook finds it ready.
        _ = Task.Run(visual.PrepareSavedJson);
        HasOutput = true;
        SelectedOutputTab = TabFor(visual.Family);
        NotifyOutputTabStateChanged();
    }

    private bool Keep(VisualOutput visual)
    {
        _visualsProduced++;
        if (Visuals.Count >= VisualLimits.MaxVisualsPerCell)
        {
            VisualsNotice = Notices.ShowingFirst(VisualLimits.MaxVisualsPerCell, _visualsProduced, "visuals");
            return false;
        }

        Visuals.Add(visual);
        ListFor(visual.Family).Add(visual);
        return true;
    }

    // A reopened notebook shows the visuals it was saved with.
    private void RestoreVisuals(NotebookCellItem model)
    {
        foreach (var saved in model.Visuals ?? [])
        {
            if (saved.ToOutput() is { } visual) Keep(visual);
        }

        if (Visuals.Count > 0) HasOutput = true;
    }

    private void ClearVisuals()
    {
        Visuals.Clear();
        ChartVisuals.Clear();
        Plot3DVisuals.Clear();
        VisualizerVisuals.Clear();
        VisualsNotice = null;
        _visualsProduced = 0;
        Model.Visuals = null;
    }

    private ObservableCollection<VisualOutput> ListFor(VisualFamily family) => family switch
    {
        VisualFamily.Chart => ChartVisuals,
        VisualFamily.Plot3D => Plot3DVisuals,
        _ => VisualizerVisuals
    };

    private static CellOutputTab TabFor(VisualFamily family) => family switch
    {
        VisualFamily.Chart => CellOutputTab.Chart,
        VisualFamily.Plot3D => CellOutputTab.Plot3D,
        _ => CellOutputTab.Visualizer
    };

    private static string? FirstTitle(ObservableCollection<VisualOutput> visuals) =>
        visuals.Count > 0 && !string.IsNullOrWhiteSpace(visuals[0].Spec.Title) ? visuals[0].Spec.Title : null;
}
