using System;
using System.Collections.Generic;

namespace FrySharp.Sdk;

/// <summary>
/// Definition of a setting contributed by an extension.
/// </summary>
public class ExtensionSettingDefinition
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = "string"; // string, boolean, integer, select
    public object? DefaultValue { get; set; }
    public List<string>? Options { get; set; }
}

/// <summary>
/// Manifest definition loaded from extension.json.
/// Declares extension metadata, dependencies, contribution points, and settings.
/// </summary>
public class ExtensionManifest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? MainEntryClass { get; set; }
    public List<string> SourceFiles { get; set; } = new();
    public Dictionary<string, ExtensionSettingDefinition> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
