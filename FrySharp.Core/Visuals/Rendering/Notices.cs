using System.Globalization;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Rendering;

/// <summary>The words a visual uses when it shows only part of its data (tables say the same: "showing the first N of M rows").</summary>
public static class Notices
{
    public static string ShowingFirst(int shown, int total, string what) =>
        string.Create(CultureInfo.InvariantCulture, $"showing the first {shown:N0} of {total:N0} {what}");
}
