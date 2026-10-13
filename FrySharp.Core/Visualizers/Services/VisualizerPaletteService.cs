using System;
using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

public static class VisualizerPaletteService
{
    private static readonly string[] IslandColors = new[]
    {
        "#10b981", // Emerald
        "#f97316", // Amber-Orange
        "#06b6d4", // Cyan
        "#ec4899", // Pink
        "#8b5cf6", // Violet
        "#eab308", // Yellow
        "#14b8a6", // Teal
        "#f43f5e", // Rose
        "#6366f1", // Indigo
        "#84cc16", // Lime
        "#d946ef", // Fuchsia
        "#0ea5e9"  // Sky
    };

    public static string WaterColor => "#111c30";
    public static string WaterBorderColor => "#1e3a5f";
    public static string WaterTextColor => "#94a3b8";
    public static string UnvisitedLandColor => "#334155";
    public static string UnvisitedLandBorderColor => "#475569";
    public static string VisitedNeutralColor => "#475569";
    public static string ActiveCellGlowColor => "#fbbf24"; // Bright Amber / Gold
    public static string ActiveCellBorderColor => "#f59e0b";
    public static string WallColor => "#18181b";
    public static string PathColor => "#eab308"; // Gold / Amber
    public static string StartColor => "#10b981"; // Emerald Green
    public static string TargetColor => "#ef4444"; // Crimson Red
    public static string VisitedColor => "#1e3a5f"; // Deep Slate Blue
    public static string FrontierColor => "#4f46e5"; // Indigo / Open set
    public static string BacktrackColor => "#4a1525"; // Muted dark wine
    public static string CandidateColor => "#0d9488"; // Teal

    public static (string Fill, string Border, string Text) GetStateStyling(GridCellState state) => state switch
    {
        GridCellState.Start => (StartColor, "#059669", "#ffffff"),
        GridCellState.Target => (TargetColor, "#dc2626", "#ffffff"),
        GridCellState.Wall => (WallColor, "#27272a", "#71717a"),
        GridCellState.Current => (ActiveCellGlowColor, ActiveCellBorderColor, "#0f172a"),
        GridCellState.Visited => (VisitedColor, "#2563eb", "#e2e8f0"),
        GridCellState.Frontier => (FrontierColor, "#6366f1", "#ffffff"),
        GridCellState.Path => (PathColor, "#ca8a04", "#000000"),
        GridCellState.Backtracked => (BacktrackColor, "#9f1239", "#fda4af"),
        GridCellState.Candidate => (CandidateColor, "#14b8a6", "#ffffff"),
        _ => ("#1e293b", "#334155", "#f8fafc")
    };

    public static (string Fill, string Border, string Text) GetTreeNodeStyling(TreeNodeState state) => state switch
    {
        TreeNodeState.Current => (ActiveCellGlowColor, ActiveCellBorderColor, "#0f172a"),
        TreeNodeState.Target => (TargetColor, "#dc2626", "#ffffff"),
        TreeNodeState.Matched => (StartColor, "#059669", "#ffffff"),
        TreeNodeState.Path => (PathColor, "#ca8a04", "#000000"),
        TreeNodeState.Visited => (VisitedColor, "#3b82f6", "#e2e8f0"),
        TreeNodeState.Candidate => (CandidateColor, "#14b8a6", "#ffffff"),
        TreeNodeState.Swapped => ("#ec4899", "#f472b6", "#ffffff"),
        TreeNodeState.Backtracked => (BacktrackColor, "#9f1239", "#fda4af"),
        TreeNodeState.Pruned => ("#27272a", "#3f3f46", "#71717a"),
        _ => ("#1e293b", "#64748b", "#ffffff")
    };

    public static (string Fill, string Border, string Text) GetGraphNodeStyling(GraphNodeState state) => state switch
    {
        GraphNodeState.Current => (ActiveCellGlowColor, ActiveCellBorderColor, "#0f172a"),
        GraphNodeState.Target => (TargetColor, "#dc2626", "#ffffff"),
        GraphNodeState.Path => (PathColor, "#ca8a04", "#000000"),
        GraphNodeState.Visited => (VisitedColor, "#2563eb", "#e2e8f0"),
        GraphNodeState.Frontier => (FrontierColor, "#6366f1", "#ffffff"),
        GraphNodeState.Relaxed => ("#06b6d4", "#0891b2", "#ffffff"),
        GraphNodeState.Cycle => ("#e11d48", "#be123c", "#ffffff"),
        GraphNodeState.Unreachable => ("#27272a", "#3f3f46", "#71717a"),
        _ => ("#1e293b", "#475569", "#ffffff")
    };

    public static (string Color, double Thickness, bool IsDashed) GetGraphEdgeStyling(GraphEdgeState state, bool isActive) => state switch
    {
        GraphEdgeState.Path => (PathColor, 2.5, false),
        GraphEdgeState.Relaxed => ("#06b6d4", 2.2, false),
        GraphEdgeState.Rejected => ("#e11d48", 1.5, true),
        GraphEdgeState.Active => ("#38bdf8", 2.0, false),
        GraphEdgeState.Visited => ("#64748b", 1.2, false),
        _ => (isActive ? "#38bdf8" : "#475569", isActive ? 2.0 : 1.2, false)
    };

    public static string GetHeatmapColor(double heat)
    {
        double t = Math.Clamp(heat, 0.0, 1.0);
        // Cool dark blue (0.0) -> Cyan (0.33) -> Orange/Amber (0.66) -> Yellow/White (1.0)
        int r, g, b;
        if (t < 0.33)
        {
            double f = t / 0.33;
            r = (int)(24 + f * (14 - 24));
            g = (int)(40 + f * (165 - 40));
            b = (int)(85 + f * (233 - 85));
        }
        else if (t < 0.66)
        {
            double f = (t - 0.33) / 0.33;
            r = (int)(14 + f * (249 - 14));
            g = (int)(165 + f * (115 - 165));
            b = (int)(233 + f * (22 - 233));
        }
        else
        {
            double f = (t - 0.66) / 0.34;
            r = (int)(249 + f * (255 - 249));
            g = (int)(115 + f * (245 - 115));
            b = (int)(22 + f * (150 - 22));
        }
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    public static string GetIslandColor(int islandIndex)
    {
        if (islandIndex <= 0) return UnvisitedLandColor;
        int idx = (islandIndex - 1) % IslandColors.Length;
        return IslandColors[idx];
    }

    public static string GetPointerColor(string pointerName)
    {
        var lower = pointerName.ToLowerInvariant();
        if (lower.Contains("left") || lower == "l" || lower.Contains("start") || lower == "slow")
            return "#10b981"; // Green
        if (lower.Contains("right") || lower == "r" || lower.Contains("end") || lower == "fast")
            return "#f43f5e"; // Red/Rose
        if (lower.Contains("mid") || lower == "m")
            return "#fbbf24"; // Gold
        if (lower.Contains("target") || lower.Contains("curr") || lower == "i")
            return "#38bdf8"; // Sky Blue
        if (lower == "j" || lower.Contains("next"))
            return "#a855f7"; // Purple

        return "#f97316"; // Orange default
    }
}
