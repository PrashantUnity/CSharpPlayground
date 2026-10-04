using System;
using System.Collections.Generic;

namespace FrySharp.Sdk;

/// <summary>
/// Metadata describing a parameter of an SDK method.
/// </summary>
public class ApiParameterMetadata
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsOptional { get; init; }
    public string? DefaultValue { get; init; }
}

/// <summary>
/// Metadata describing an executable method on an SDK interface.
/// </summary>
public class ApiMethodMetadata
{
    public string Name { get; init; } = string.Empty;
    public string ReturnType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<ApiParameterMetadata> Parameters { get; init; } = Array.Empty<ApiParameterMetadata>();
    public string? ExampleSnippet { get; init; }
}

/// <summary>
/// Metadata describing a property on an SDK interface.
/// </summary>
public class ApiPropertyMetadata
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool CanRead { get; init; } = true;
    public bool CanWrite { get; init; }
}

/// <summary>
/// Metadata describing an entire module or subsystem of the SDK (e.g. App.UI, App.Theme).
/// </summary>
public class ApiModuleMetadata
{
    public string Name { get; init; } = string.Empty;
    public string AccessPath { get; init; } = string.Empty;
    public string InterfaceType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<ApiPropertyMetadata> Properties { get; init; } = Array.Empty<ApiPropertyMetadata>();
    public IReadOnlyList<ApiMethodMetadata> Methods { get; init; } = Array.Empty<ApiMethodMetadata>();
}

/// <summary>
/// Metadata describing an available or custom registered command.
/// </summary>
public class CommandMetadata
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Category { get; init; } = "General";
    public string? ShortcutGesture { get; init; }
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// Metadata describing a live theme color, brush, or typography token.
/// </summary>
public class ThemeTokenMetadata
{
    public string Key { get; init; } = string.Empty;
    public string Category { get; init; } = "Color";
    public string CurrentValue { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// Metadata describing an extensible UI slot / contribution zone in the running application.
/// </summary>
public class UiSlotMetadata
{
    public string Zone { get; init; } = string.Empty;
    public string SlotId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int MountedCount { get; init; }
    public IReadOnlyList<string> MountedItemIds { get; init; } = Array.Empty<string>();
    public string RegistrationMethod { get; init; } = string.Empty;
    public string DescriptorType { get; init; } = string.Empty;
    public string ExampleSnippet { get; init; } = string.Empty;
}

/// <summary>
/// Metadata describing a lifecycle hook point that can be intercepted or listened to.
/// </summary>
public class HookMetadata
{
    public string Name { get; init; } = string.Empty;
    public string HookId { get; init; } = string.Empty;
    public string PayloadType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ExampleSnippet { get; init; } = string.Empty;
}
