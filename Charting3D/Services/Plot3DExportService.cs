using System;
using System.Globalization;
using System.Text;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Services;

public static class Plot3DExportService
{
    public static string ToCsv(Plot3DOptions options)
    {
        var sb = new StringBuilder();
        if (options.Surface != null && options.Surface.ZValues != null)
        {
            sb.AppendLine("Row,Col,X,Y,Z");
            var grid = options.Surface.ZValues;
            int rows = grid.GetLength(0);
            int cols = grid.GetLength(1);
            double minX = options.Surface.MinX, maxX = options.Surface.MaxX;
            double minY = options.Surface.MinY, maxY = options.Surface.MaxY;
            for (int r = 0; r < rows; r++)
            {
                double y = rows > 1 ? minY + (r / (double)(rows - 1)) * (maxY - minY) : minY;
                for (int c = 0; c < cols; c++)
                {
                    double x = cols > 1 ? minX + (c / (double)(cols - 1)) * (maxX - minX) : minX;
                    double z = grid[r, c];
                    sb.Append(r.ToString(CultureInfo.InvariantCulture)).Append(',')
                      .Append(c.ToString(CultureInfo.InvariantCulture)).Append(',')
                      .Append(Number(x)).Append(',')
                      .Append(Number(y)).Append(',')
                      .Append(Number(z)).AppendLine();
                }
            }
            return sb.ToString();
        }

        if (options.Graph != null)
        {
            sb.AppendLine("Type,Id,X,Y,Z,Label");
            for (int i = 0; i < options.Graph.Nodes.Count; i++)
            {
                var n = options.Graph.Nodes[i];
                sb.Append("Node,").Append(Quoted(n.Id ?? i.ToString(CultureInfo.InvariantCulture))).Append(',')
                  .Append(Number(n.X)).Append(',')
                  .Append(Number(n.Y)).Append(',')
                  .Append(Number(n.Z)).Append(',')
                  .Append(Quoted(n.Label)).AppendLine();
            }
            foreach (var e in options.Graph.Edges)
            {
                sb.Append("Edge,").Append(Quoted($"{e.FromId}->{e.ToId}")).Append(",,,,")
                  .Append(Quoted(e.Weight?.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty)).AppendLine();
            }
            return sb.ToString();
        }

        sb.AppendLine("Series,Index,Label,X,Y,Z");
        foreach (var s in options.Series)
        {
            for (int i = 0; i < s.Points.Count; i++)
            {
                var p = s.Points[i];
                sb.Append(Quoted(s.Name)).Append(',')
                  .Append(i.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(Quoted(p.Label)).Append(',')
                  .Append(Number(p.X)).Append(',')
                  .Append(Number(p.Y)).Append(',')
                  .Append(Number(p.Z)).AppendLine();
            }
        }
        return sb.ToString();
    }

    private static string Quoted(string? text) => $"\"{(text ?? string.Empty).Replace("\"", "\"\"")}\"";
    private static string Number(double value) => double.IsFinite(value) ? value.ToString("R", CultureInfo.InvariantCulture) : string.Empty;
}
