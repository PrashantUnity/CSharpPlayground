using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

// Bars: the recorder's factory and step for it, and its typed recorder.
public partial class VisualizerRecorder
{
    public static BarVisualizerRecorder CreateBars(
        BarChartVisualizerData bars,
        string? title = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var options = new VisualizerOptions
        {
            Title = title ?? "Sorting & Array Bars",
            Kind = VisualizerKind.Bars,
            BarData = bars
        };
        var recorder = new BarVisualizerRecorder(options);
        recorder.Step("Initial Bar State", sourceLine: sourceLine, sourceFile: sourceFile);
        return recorder;
    }

    /// <summary>Records numbers as bars. Steps re-read a live array or list, so updates the algorithm makes show up.</summary>
    public static BarVisualizerRecorder CreateBars(
        IEnumerable<int> values,
        string? title = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var bars = new BarChartVisualizerData(values) { Source = values as System.Collections.IList };
        return CreateBars(bars, title, sourceLine, sourceFile);
    }

    public static BarVisualizerRecorder CreateBars(
        IEnumerable<double> values,
        string? title = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var bars = new BarChartVisualizerData(values) { Source = values as System.Collections.IList };
        return CreateBars(bars, title, sourceLine, sourceFile);
    }

    public VisualizerRecorder StepBars(
        string description,
        Action<BarChartVisualizerData>? updateBars = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        if (Options.BarData != null)
        {
            Options.BarData.RefreshFromSource();
            updateBars?.Invoke(Options.BarData);

            var step = NewStep(description, sourceLine, sourceFile);
            step.Snapshot = Options.BarData.Clone();
            Sequence.AddStep(step);
        }
        return this;
    }
}

public class BarVisualizerRecorder : VisualizerRecorder
{
    public BarVisualizerRecorder(VisualizerOptions options) : base(options) { }

    /// <summary>
    /// Records the bars with named pointers under them (<c>pointers: new { left, right }</c>), extra bars to light up,
    /// and an optional shaded block, such as the water between two walls.
    /// </summary>
    public BarVisualizerRecorder Step(
        string description,
        object? pointers = null,
        IEnumerable<int>? highlight = null,
        BarShade? shade = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        StepBars(description, bars =>
        {
            foreach (var item in bars.Items)
            {
                item.PointerLabel = null;
                item.IsActive = false;
            }

            var lit = new HashSet<int>(highlight ?? Array.Empty<int>());
            if (pointers != null)
            {
                foreach (var prop in pointers.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (prop.GetValue(pointers) is not int index || index < 0 || index >= bars.Items.Count) continue;
                    var item = bars.Items[index];
                    item.PointerLabel = item.PointerLabel == null ? prop.Name : $"{item.PointerLabel},{prop.Name}";
                    lit.Add(index);
                }
            }

            foreach (int index in lit)
            {
                if (index >= 0 && index < bars.Items.Count) bars.Items[index].IsActive = true;
            }
            bars.Shade = shade;
        }, sourceLine, sourceFile);
        return this;
    }

    public BarVisualizerRecorder Step(
        string description,
        Action<BarChartVisualizerData> updateBars,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        StepBars(description, updateBars, sourceLine, sourceFile);
        return this;
    }
}
