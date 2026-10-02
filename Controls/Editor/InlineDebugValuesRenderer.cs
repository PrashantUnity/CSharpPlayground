using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls;

public class InlineDebugValuesRenderer : IBackgroundRenderer
{
    private static readonly Typeface HintTypeface = new("JetBrains Mono, Menlo, Monaco, Consolas, monospace", FontStyle.Italic, FontWeight.Normal);
    private static readonly IBrush DarkHintBrush = new SolidColorBrush(Color.FromArgb(190, 140, 155, 175));
    private static readonly IBrush LightHintBrush = new SolidColorBrush(Color.FromArgb(200, 80, 95, 110));
    private static IBrush DefaultHintBrush => ThemeService.IsDark ? DarkHintBrush : LightHintBrush;
    private static readonly IBrush ChangedHintBrush = new SolidColorBrush(Color.FromRgb(255, 121, 198)); // Vibrant accent for changed values

    public KnownLayer Layer => KnownLayer.Selection;

    public bool IsEnabled { get; set; } = true;

    public int PausedLine { get; set; } = -1;

    public IReadOnlyList<DebugVariableItem>? Variables { get; set; }

    public TextDocument? Document { get; set; }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!IsEnabled || PausedLine <= 0 || Variables == null || Variables.Count == 0 || !textView.VisualLinesValid)
        {
            return;
        }

        var doc = Document;
        if (doc == null) return;

        foreach (var vl in textView.VisualLines)
        {
            int lineNum = vl.FirstDocumentLine.LineNumber;
            if (lineNum <= 0 || lineNum > doc.LineCount) continue;

            // Only show hints for lines near the paused line (within 5 lines) or lines referencing active variables
            if (Math.Abs(lineNum - PausedLine) > 6) continue;

            var docLine = doc.GetLineByNumber(lineNum);
            string lineText = doc.GetText(docLine.Offset, docLine.Length);
            if (string.IsNullOrWhiteSpace(lineText)) continue;

            // Find matching variables referenced on this line
            var matchedVars = new List<DebugVariableItem>();
            foreach (var v in Variables)
            {
                if (string.IsNullOrWhiteSpace(v.Name) || v.Name.StartsWith('$')) continue;
                if (IsIdentifierInLine(lineText, v.Name))
                {
                    matchedVars.Add(v);
                    if (matchedVars.Count >= 3) break;
                }
            }

            if (matchedVars.Count == 0)
            {
                if (lineNum == PausedLine && Variables.Count > 0)
                {
                    // Fallback on paused line: show first 2 locals
                    matchedVars.AddRange(Variables.Where(v => !v.Name.StartsWith('$')).Take(2));
                }
                else
                {
                    continue;
                }
            }

            if (matchedVars.Count == 0) continue;

            // Format inline display string: e.g. "user: {Age: 25}  count: 2"
            var parts = new List<string>();
            bool anyChanged = false;
            foreach (var v in matchedVars)
            {
                string val = v.ValueDisplay;
                if (val.Length > 36) val = val.Substring(0, 33) + "...";
                parts.Add($"{v.Name}: {val}");
                if (v.HasValueChanged) anyChanged = true;
            }

            string hintText = string.Join("   ", parts);
            if (lineNum == PausedLine)
            {
                hintText = "▶ " + hintText;
            }

            try
            {
                var endPos = textView.GetVisualPosition(new TextViewPosition(lineNum, docLine.Length + 1), VisualYPosition.LineBottom);
                double x = endPos.X - textView.HorizontalOffset + 28;
                double y = vl.VisualTop - textView.VerticalOffset + (vl.Height - 14) / 2;

                var brush = anyChanged ? ChangedHintBrush : DefaultHintBrush;
                var ft = new FormattedText(
                    hintText,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    HintTypeface,
                    11.0,
                    brush);

                drawingContext.DrawText(ft, new Point(x, y));
            }
            catch
            {
                // Ignore layout transient measurement during scrolling
            }
        }
    }

    private static bool IsIdentifierInLine(string lineText, string id)
    {
        int idx = 0;
        while ((idx = lineText.IndexOf(id, idx, StringComparison.Ordinal)) >= 0)
        {
            bool startOk = idx == 0 || (!char.IsLetterOrDigit(lineText[idx - 1]) && lineText[idx - 1] != '_');
            int after = idx + id.Length;
            bool endOk = after >= lineText.Length || (!char.IsLetterOrDigit(lineText[after]) && lineText[after] != '_');
            if (startOk && endOk) return true;
            idx += id.Length;
        }
        return false;
    }
}
