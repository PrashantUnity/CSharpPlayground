using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Services.Settings;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Layout;

/// <summary>One layout in the user's library.</summary>
public sealed class SavedLayout
{
    public int Version { get; set; } = 1;
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public DateTime ModifiedUtc { get; set; }

    /// <summary>The layout, as <see cref="LayoutSpec.ToJson"/> writes it.</summary>
    public JsonElement Spec { get; set; }

    public LayoutSpec Layout => LayoutSpec.FromJson(Spec) ?? LayoutSpec.Default;
}

/// <summary>
/// The user's own layouts, as many as they like, each named when saved: one file per layout
/// (<c>layouts/&lt;id&gt;.frylayout.json</c>), written atomically, like the theme library.
/// </summary>
public sealed class LayoutLibraryStore
{
    public const string FileSuffix = ".frylayout.json";
    public const string IdPrefix = "layout-user-";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly object _gate = new();

    public LayoutLibraryStore(string folder)
    {
        Folder = folder;
    }

    public string Folder { get; }

    public static bool IsLibraryId(string? id) => id != null && id.StartsWith(IdPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Every saved layout, most recently changed first. Reads every file: call it off the UI thread.</summary>
    public IReadOnlyList<SavedLayout> List()
    {
        if (!Directory.Exists(Folder)) return Array.Empty<SavedLayout>();
        var layouts = new List<SavedLayout>();
        foreach (var file in Directory.EnumerateFiles(Folder, "*" + FileSuffix))
        {
            if (Read(file) is { } layout) layouts.Add(layout);
        }

        return layouts.OrderByDescending(l => l.ModifiedUtc).ToList();
    }

    public SavedLayout? Get(string id) => IsLibraryId(id) ? Read(PathOf(id)) : null;

    public SavedLayout Save(string name, LayoutSpec layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var now = DateTime.UtcNow;
        var id = IdPrefix + Guid.NewGuid().ToString("N")[..12];
        var saved = new SavedLayout
        {
            Id = id,
            Name = CleanName(name),
            CreatedUtc = now,
            ModifiedUtc = now,
            Spec = (layout with { PresetId = id }).ToJson(),
        };
        Write(saved);
        return saved;
    }

    public SavedLayout? Overwrite(string id, LayoutSpec layout)
    {
        var existing = Get(id);
        if (existing == null) return null;
        existing.Spec = (layout with { PresetId = existing.Id }).ToJson();
        existing.ModifiedUtc = DateTime.UtcNow;
        Write(existing);
        return existing;
    }

    public SavedLayout? Rename(string id, string newName)
    {
        var existing = Get(id);
        if (existing == null) return null;
        existing.Name = CleanName(newName);
        existing.ModifiedUtc = DateTime.UtcNow;
        Write(existing);
        return existing;
    }

    public SavedLayout Duplicate(LayoutSpec layout, string newName) => Save(newName, layout);

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
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"[LayoutLibraryStore] Couldn't delete '{path}': {ex.Message}");
                return false;
            }
        }
    }

    private string PathOf(string id) => Path.Combine(Folder, id + FileSuffix);

    private static string CleanName(string name) => string.IsNullOrWhiteSpace(name) ? "Untitled layout" : name.Trim();

    private void Write(SavedLayout layout)
    {
        lock (_gate)
        {
            StudioSettingsStore.WriteAtomically(PathOf(layout.Id), JsonSerializer.Serialize(layout, JsonOptions));
        }
    }

    private static SavedLayout? Read(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var layout = JsonSerializer.Deserialize<SavedLayout>(File.ReadAllText(path));
            return layout == null || string.IsNullOrWhiteSpace(layout.Id) ? null : layout;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Debug.WriteLine($"[LayoutLibraryStore] Skipping unreadable layout '{path}': {ex.Message}");
            return null;
        }
    }
}
