using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Material.Icons;

namespace PdfEditorApp.Plugins.CSharpEditor.Models;

public enum RecentWorkspaceKind
{
    ProjectWorkspace,
    StandaloneNotebook,
    StandaloneScript
}

public class RecentWorkspaceItem : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public RecentWorkspaceKind Kind { get; set; } = RecentWorkspaceKind.ProjectWorkspace;
    public DateTime LastOpenedUtc { get; set; } = DateTime.UtcNow;

    private bool _isPinned;
    public bool IsPinned
    {
        get => _isPinned;
        set => SetProperty(ref _isPinned, value);
    }

    private string? _gitBranch;
    public string? GitBranch
    {
        get => _gitBranch;
        set => SetProperty(ref _gitBranch, value);
    }

    public bool HasGitBranch => !string.IsNullOrWhiteSpace(GitBranch);

    public bool IsNotebook => Kind == RecentWorkspaceKind.StandaloneNotebook;
    public bool IsProjectWorkspace => Kind == RecentWorkspaceKind.ProjectWorkspace;
    public bool IsScript => Kind == RecentWorkspaceKind.StandaloneScript;

    public string KindBadgeText => Kind switch
    {
        RecentWorkspaceKind.StandaloneNotebook => "NOTEBOOK",
        RecentWorkspaceKind.StandaloneScript => "SCRIPT",
        _ => "WORKSPACE"
    };

    public string Monogram => Kind switch
    {
        RecentWorkspaceKind.StandaloneNotebook => "NB",
        _ => GetMonogram(Name)
    };

    public string MonogramBgBrush => Kind switch
    {
        RecentWorkspaceKind.StandaloneNotebook => "#2E1A05",
        RecentWorkspaceKind.StandaloneScript => "#0D253F",
        _ => GetDeterministicBgColor(Name)
    };

    public string MonogramFgBrush => Kind switch
    {
        RecentWorkspaceKind.StandaloneNotebook => "#F0883E",
        RecentWorkspaceKind.StandaloneScript => "#58A6FF",
        _ => GetDeterministicFgColor(Name)
    };

    public MaterialIconKind IconKind => Kind switch
    {
        RecentWorkspaceKind.StandaloneNotebook => MaterialIconKind.NotebookOutline,
        RecentWorkspaceKind.StandaloneScript => MaterialIconKind.FileCodeOutline,
        _ => MaterialIconKind.FolderOutline
    };

    public string DisplayPath
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Path)) return string.Empty;
            try
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(home) && Path.StartsWith(home, StringComparison.OrdinalIgnoreCase))
                {
                    return "~" + Path[home.Length..];
                }
            }
            catch
            {
            }
            return Path;
        }
    }

    public string LastModifiedDisplay => FormatRelativeTime(LastOpenedUtc);

    private static string GetMonogram(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "WS";
        var clean = System.IO.Path.GetFileNameWithoutExtension(name).Trim();
        if (clean.Length == 0) return "WS";

        var parts = clean.Split(new[] { ' ', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
        }

        var upperLetters = "";
        for (int i = 0; i < clean.Length && upperLetters.Length < 2; i++)
        {
            if (char.IsUpper(clean[i])) upperLetters += clean[i];
        }
        if (upperLetters.Length >= 2) return upperLetters;

        return clean.Length >= 2
            ? clean[..2].ToUpperInvariant()
            : clean.ToUpperInvariant();
    }

    private static string GetDeterministicBgColor(string name)
    {
        var bgColors = new[]
        {
            "#162740", // Blue tinted dark bg
            "#142B1F", // Green tinted dark bg
            "#221938", // Purple tinted dark bg
            "#331616", // Red tinted dark bg
            "#2E230B", // Gold tinted dark bg
            "#0B2925", // Teal tinted dark bg
            "#2B1705", // Orange tinted dark bg
            "#2C1224"  // Pink tinted dark bg
        };
        var hash = Math.Abs(name.GetHashCode());
        return bgColors[hash % bgColors.Length];
    }

    private static string GetDeterministicFgColor(string name)
    {
        var fgColors = new[]
        {
            "#58A6FF", // Blue
            "#3FB950", // Green
            "#BC8CFF", // Purple
            "#F85149", // Red
            "#D29922", // Gold
            "#39C5BB", // Teal
            "#F0883E", // Orange
            "#F778BA"  // Pink
        };
        var hash = Math.Abs(name.GetHashCode());
        return fgColors[hash % fgColors.Length];
    }

    private static string FormatRelativeTime(DateTime timeUtc)
    {
        var span = DateTime.UtcNow - timeUtc;
        if (span.TotalMinutes < 1) return "Just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 2) return "Yesterday";
        if (span.TotalDays < 30) return $"{(int)span.TotalDays}d ago";
        return timeUtc.ToString("MMM d, yyyy");
    }
}
