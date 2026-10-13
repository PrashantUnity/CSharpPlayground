using System.Collections.Generic;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

public class IslandData
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Color { get; set; } = "#22c55e";
    public List<(int Row, int Col)> Cells { get; set; } = new();
    public int Area => Cells.Count;
    public int Perimeter { get; set; }
    public (int MinRow, int MinCol, int MaxRow, int MaxCol) BoundingBox { get; set; }

    public IslandData() { }

    public IslandData(int id, string label, string color)
    {
        Id = id;
        Label = label;
        Color = color;
    }
}
