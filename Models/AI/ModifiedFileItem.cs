using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PdfEditorApp.Plugins.CSharpEditor.Models.AI;

/// <summary>
/// Tracks a file modified by the AI agent during an autonomous task, including diff metrics and review state.
/// </summary>
public partial class ModifiedFileItem : ObservableObject
{
    public string FilePath { get; init; } = string.Empty;

    public string FileName => Path.GetFileName(FilePath);

    [ObservableProperty]
    private string _relativePath = string.Empty;

    [ObservableProperty]
    private string _originalContent = string.Empty;

    [ObservableProperty]
    private string _modifiedContent = string.Empty;

    [ObservableProperty]
    private int _linesAdded;

    [ObservableProperty]
    private int _linesDeleted;

    [ObservableProperty]
    private bool _isAccepted;

    [ObservableProperty]
    private bool _isRejected;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isDiffExpanded;

    [ObservableProperty]
    private string _diffPreviewText = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<DiffLine> _diffLines = Array.Empty<DiffLine>();

    public string SummaryText => $"+{LinesAdded} -{LinesDeleted}";

    public void ToggleDiffExpanded()
    {
        IsDiffExpanded = !IsDiffExpanded;
    }

    partial void OnDiffPreviewTextChanged(string value)
    {
        if (DiffLines.Count == 0 && !string.IsNullOrWhiteSpace(value))
        {
            DiffLines = ParseDiffLines(value);
        }
    }

    private static IReadOnlyList<DiffLine> ParseDiffLines(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<DiffLine>();
        var list = new List<DiffLine>();
        var lines = text.Replace("\r\n", "\n").Split('\n');
        foreach (var line in lines)
        {
            var kind = DiffLineKind.Context;
            if (line.StartsWith("@@")) kind = DiffLineKind.Header;
            else if (line.StartsWith("+")) kind = DiffLineKind.Added;
            else if (line.StartsWith("-")) kind = DiffLineKind.Deleted;
            list.Add(new DiffLine(line, kind));
        }
        return list;
    }

    /// <summary>
    /// Computes rough lines added and deleted by line comparison, and generates a unified diff preview.
    /// </summary>
    public void CalculateLineMetrics()
    {
        var origLines = (OriginalContent ?? string.Empty).Replace("\r\n", "\n").Split('\n');
        var modLines = (ModifiedContent ?? string.Empty).Replace("\r\n", "\n").Split('\n');

        int added = 0;
        int deleted = 0;
        var sb = new System.Text.StringBuilder();

        if (string.IsNullOrEmpty(OriginalContent))
        {
            added = modLines.Length;
            sb.AppendLine($"@@ +1,{modLines.Length} (New File) @@");
            int previewCount = Math.Min(modLines.Length, 40);
            for (int i = 0; i < previewCount; i++)
            {
                sb.AppendLine($"+ {modLines[i]}");
            }
            if (modLines.Length > previewCount)
            {
                sb.AppendLine($"... ({modLines.Length - previewCount} more lines)");
            }
        }
        else
        {
            var origSet = new HashSet<string>(origLines);
            var modSet = new HashSet<string>(modLines);

            foreach (var line in modLines)
            {
                if (!origSet.Contains(line)) added++;
            }

            foreach (var line in origLines)
            {
                if (!modSet.Contains(line)) deleted++;
            }

            sb.AppendLine($"@@ -1,{origLines.Length} +1,{modLines.Length} @@");
            int max = Math.Max(origLines.Length, modLines.Length);
            int diffsPrinted = 0;
            for (int i = 0; i < max && diffsPrinted < 40; i++)
            {
                string? o = i < origLines.Length ? origLines[i] : null;
                string? m = i < modLines.Length ? modLines[i] : null;

                if (o != m)
                {
                    if (o != null)
                    {
                        sb.AppendLine($"- {o}");
                        diffsPrinted++;
                    }
                    if (m != null)
                    {
                        sb.AppendLine($"+ {m}");
                        diffsPrinted++;
                    }
                }
            }
        }

        LinesAdded = added;
        LinesDeleted = deleted;
        var diffText = sb.Length > 0 ? sb.ToString().TrimEnd() : "No visual line changes";
        DiffPreviewText = diffText;
        DiffLines = ParseDiffLines(diffText);
    }
}

public enum DiffLineKind
{
    Context,
    Header,
    Added,
    Deleted
}

public record DiffLine(string Text, DiffLineKind Kind)
{
    public string ForegroundHex => Kind switch
    {
        DiffLineKind.Header => "#38BDF8",
        DiffLineKind.Added => "#4ADE80",
        DiffLineKind.Deleted => "#F87171",
        _ => "#94A3B8"
    };

    public string BackgroundHex => Kind switch
    {
        DiffLineKind.Header => "#1E293B",
        DiffLineKind.Added => "#064E3B44",
        DiffLineKind.Deleted => "#7F1D1D44",
        _ => "Transparent"
    };
}

