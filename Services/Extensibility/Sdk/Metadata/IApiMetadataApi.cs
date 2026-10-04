using System.Collections.Generic;

namespace FrySharp.Sdk;

/// <summary>
/// Public API for runtime reflection, discovery of commands, active theme tokens,
/// UI contribution slots, and export of API schemas for AI and external tooling.
/// </summary>
public interface IApiMetadataApi
{
    /// <summary>Returns structured schema metadata for all public SDK interfaces on App.</summary>
    IReadOnlyList<ApiModuleMetadata> GetSdkApiSchemas(string? category = null);

    /// <summary>Returns all currently registered commands with IDs, titles, and shortcut gestures.</summary>
    IReadOnlyList<CommandMetadata> GetRegisteredCommands();

    /// <summary>Returns all active theme color tokens, brushes, and typography keys with current values.</summary>
    IReadOnlyList<ThemeTokenMetadata> GetRegisteredThemeTokens();

    /// <summary>Returns all available UI contribution slots and what is currently mounted in each.</summary>
    IReadOnlyList<UiSlotMetadata> GetActiveUiSlots();

    /// <summary>Returns all available lifecycle hook points with payload types.</summary>
    IReadOnlyList<HookMetadata> GetRegisteredHooks();

    /// <summary>Serializes the entire runtime API and extension state as a JSON catalog for AI consumption.</summary>
    string ExportCatalogJson();

    /// <summary>Serializes a concise Markdown summary of available APIs and extension points.</summary>
    string ExportCatalogMarkdown();
}
