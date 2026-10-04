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
    public List<DeclarativeLanguageContribution> Languages { get; set; } = new();
}

/// <summary>
/// Language Server Protocol (LSP) configuration for external language intelligence.
/// </summary>
public class DeclarativeLspContribution
{
    /// <summary>Executable name or relative path of the language server (e.g. "zls", "gopls", "clangd").</summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>Command-line arguments passed to the language server (e.g. ["--stdio"]).</summary>
    public List<string> Args { get; set; } = new();
}

/// <summary>
/// Declarative language specification contributed via extension.json.
/// </summary>
public class DeclarativeLanguageContribution
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public List<string> Extensions { get; set; } = new();
    public List<string> Aliases { get; set; } = new();
    public string IconKind { get; set; } = "CodeBraces";
    public string AccentHex { get; set; } = "#8B949E";
    public string LineCommentPrefix { get; set; } = "//";
    public string? SyntaxFile { get; set; }
    public string? NewFileTemplate { get; set; }
    public string? RuntimeDescription { get; set; }
    public string? RunCommand { get; set; }
    public string? BuildCommand { get; set; }
    public bool IsCompiled { get; set; }
    public DeclarativeLspContribution? Lsp { get; set; }
}

