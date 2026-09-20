using System;
using System.Collections.Generic;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

public class BarVisualizerItem
{
    public int Index { get; set; }
    public double Value { get; set; }
    public string DisplayValue { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string? PointerLabel { get; set; }
    public string? ColorHex { get; set; }
    public bool IsActive { get; set; }
    public bool IsPivot { get; set; }
    public bool IsSorted { get; set; }

    public BarVisualizerItem Clone()
    {
        return new BarVisualizerItem
        {
            Index = Index,
            Value = Value,
            DisplayValue = DisplayValue,
            Label = Label,
            PointerLabel = PointerLabel,
            ColorHex = ColorHex,
            IsActive = IsActive,
            IsPivot = IsPivot,
            IsSorted = IsSorted
        };
    }
}

public class BarChartVisualizerData
{
    public List<BarVisualizerItem> Items { get; set; } = new();
    public double MaxValue { get; set; } = 100.0;
    public double MinValue { get; set; } = 0.0;
    public bool ShowValues { get; set; } = true;
    public bool ShowIndices { get; set; } = true;
    public string? Description { get; set; }

    public BarChartVisualizerData() { }

    public BarChartVisualizerData(IEnumerable<double> values)
    {
        int idx = 0;
        double max = 1.0;
        double min = 0.0;
        foreach (var val in values)
        {
            if (val > max) max = val;
            if (val < min) min = val;
            Items.Add(new BarVisualizerItem
            {
                Index = idx++,
                Value = val,
                DisplayValue = val.ToString("0.##")
            });
        }
        MaxValue = max > 0 ? max : 1.0;
        MinValue = min;
    }

    public BarChartVisualizerData(IEnumerable<int> values)
    {
        int idx = 0;
        double max = 1.0;
        double min = 0.0;
        foreach (var val in values)
        {
            if (val > max) max = val;
            if (val < min) min = val;
            Items.Add(new BarVisualizerItem
            {
                Index = idx++,
                Value = val,
                DisplayValue = val.ToString()
            });
        }
        MaxValue = max > 0 ? max : 1.0;
        MinValue = min;
    }

    public BarChartVisualizerData Clone()
    {
        var clone = new BarChartVisualizerData
        {
            MaxValue = MaxValue,
            MinValue = MinValue,
            ShowValues = ShowValues,
            ShowIndices = ShowIndices,
            Description = Description
        };
        foreach (var item in Items)
        {
            clone.Items.Add(item.Clone());
        }
        return clone;
    }
}
