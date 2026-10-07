using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Settings;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming;

/// <summary>One theme in the user's library: its colours, its name, and how it was made (so it can be edited later).</summary>
public sealed class SavedTheme
{
    public int Version { get; set; } = 1;
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public DateTime ModifiedUtc { get; set; }

    /// <summary>generated, imported or duplicate.</summary>
    public string Source { get; set; } = "generated";

    /// <summary>The theme as it is applied.</summary>
    public ThemeDefinition Theme { get; set; } = new();

    /// <summary>The harmony controls it was made with, if it came from the theme studio.</summary>
    public HarmonyStudioSettings? Harmony { get; set; }

    /// <summary>The palette (sections, locks, engine, seed) it was made from, as the palette generator wrote it.</summary>
    public JsonElement? Palette { get; set; }
}

/// <summary>
/// The user's own themes: as many as they like, each named when saved. One file per theme
/// (<c>themes/&lt;id&gt;.frytheme.json</c>), written atomically, so a library of hundreds of themes stays fast to change
/// and one damaged file never loses the others.
/// </summary>
public sealed class ThemeLibraryStore
{
    public const string FileSuffix = ".frytheme.json";
    public const string IdPrefix = "user-";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly object _gate = new();

    public ThemeLibraryStore(string folder)
    {
        Folder = folder;
    }

    public string Folder { get; }

    /// <summary>A theme was saved, renamed, duplicated or deleted.</summary>
    public event Action? Changed;

    /// <summary>Whether <paramref name="themeId"/> names a theme of this library (as opposed to a built-in one).</summary>
    public static bool IsLibraryId(string? themeId) => themeId != null && themeId.StartsWith(IdPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Every saved theme, most recently changed first. Reads every file: call it off the UI thread.</summary>
    public IReadOnlyList<SavedTheme> List()
    {
        if (!Directory.Exists(Folder)) return Array.Empty<SavedTheme>();
        var themes = new List<SavedTheme>();
        foreach (var file in Directory.EnumerateFiles(Folder, "*" + FileSuffix))
        {
            if (Read(file) is { } theme) themes.Add(theme);
        }

        return themes.OrderByDescending(t => t.ModifiedUtc).ToList();
    }

    public SavedTheme? Get(string id) => IsLibraryId(id) ? Read(PathOf(id)) : null;

    /// <summary>Saves <paramref name="theme"/> as a new library theme called <paramref name="name"/>.</summary>
    public SavedTheme Save(string name, ThemeDefinition theme, HarmonyStudioSettings? harmony = null, JsonElement? palette = null, string source = "generated")
    {
        ArgumentNullException.ThrowIfNull(theme);
        var now = DateTime.UtcNow;
        var id = IdPrefix + Guid.NewGuid().ToString("N")[..12];
        var saved = new SavedTheme
        {
            Id = id,
            Name = CleanName(name),
            CreatedUtc = now,
            ModifiedUtc = now,
            Source = source,
            Theme = WithIdentity(theme, id, CleanName(name)),
            Harmony = harmony,
            Palette = palette,
        };
        Write(saved);
        return saved;
    }

    /// <summary>Replaces a saved theme's colours (and how it was made), keeping its id and name.</summary>
    public SavedTheme? Overwrite(string id, ThemeDefinition theme, HarmonyStudioSettings? harmony = null, JsonElement? palette = null)
    {
        var existing = Get(id);
        if (existing == null) return null;
        existing.Theme = WithIdentity(theme, existing.Id, existing.Name);
        existing.Harmony = harmony ?? existing.Harmony;
        existing.Palette = palette ?? existing.Palette;
        existing.ModifiedUtc = DateTime.UtcNow;
        Write(existing);
        return existing;
    }

    public SavedTheme? Rename(string id, string newName)
    {
        var existing = Get(id);
        if (existing == null) return null;
        existing.Name = CleanName(newName);
        existing.Theme = WithIdentity(existing.Theme, existing.Id, existing.Name);
        existing.ModifiedUtc = DateTime.UtcNow;
        Write(existing);
        return existing;
    }

    /// <summary>A copy of a saved theme, or of any theme (a built-in one, say) under a new name.</summary>
    public SavedTheme Duplicate(ThemeDefinition theme, string newName, HarmonyStudioSettings? harmony = null, JsonElement? palette = null) =>
        Save(newName, theme, harmony, palette, source: "duplicate");

    public bool Delete(string id)
    {
        if (!IsLibraryId(id)) return false;
        var path = PathOf(id);
        lock (_gate)
        {
            if (!File.Exists(path)) return false;
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"[ThemeLibraryStore] Couldn't delete '{path}': {ex.Message}");
                return false;
            }
        }

        Changed?.Invoke();
        return true;
    }

    private string PathOf(string id) => Path.Combine(Folder, id + FileSuffix);

    private static string CleanName(string name) => string.IsNullOrWhiteSpace(name) ? "Untitled theme" : name.Trim();

    private static ThemeDefinition WithIdentity(ThemeDefinition theme, string id, string name) => new()
    {
        Id = id,
        Name = name,
        Description = theme.Description,
        IsDark = theme.IsDark,
        Colors = new Dictionary<string, string>(theme.Colors, StringComparer.OrdinalIgnoreCase),
        Fonts = new Dictionary<string, string>(theme.Fonts, StringComparer.OrdinalIgnoreCase),
        Numbers = new Dictionary<string, double>(theme.Numbers, StringComparer.OrdinalIgnoreCase),
    };

    private void Write(SavedTheme theme)
    {
        lock (_gate)
        {
            StudioSettingsStore.WriteAtomically(PathOf(theme.Id), JsonSerializer.Serialize(theme, JsonOptions));
        }

        Changed?.Invoke();
    }

    private static SavedTheme? Read(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var theme = JsonSerializer.Deserialize<SavedTheme>(File.ReadAllText(path));
            if (theme == null || string.IsNullOrWhiteSpace(theme.Id)) return null;
            // JSON gives plain dictionaries back: restore the case-insensitive keys tokens are looked up with.
            theme.Theme = WithIdentity(theme.Theme ?? new ThemeDefinition(), theme.Id, theme.Name);
            return theme;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Debug.WriteLine($"[ThemeLibraryStore] Skipping unreadable theme '{path}': {ex.Message}");
            return null;
        }
    }
}
