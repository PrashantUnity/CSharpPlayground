using System;
using System.Globalization;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Layout;

/// <summary>How numbers on an axis are written: 1.5k, 2.3M, whole numbers without decimals.</summary>
internal static class ChartFormat
{
    public static string Tick(double value)
    {
        if (Math.Abs(value) >= 1_000_000)
            return $"{value / 1_000_000.0:0.##}M";
        if (Math.Abs(value) >= 1_000)
            return $"{value / 1_000.0:0.##}k";
        if (Math.Abs(value) < 0.01 && value != 0)
            return $"{value:0.###}";
        return Math.Abs(value - Math.Round(value)) < 1e-6 ? $"{value:N0}" : $"{value:0.##}";
    }

    public static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
