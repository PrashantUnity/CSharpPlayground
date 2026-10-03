using System;
using System.Collections.Generic;

namespace FrySharp.Sdk;

/// <summary>
/// Context passed through the command middleware pipeline when a command is executed.
/// </summary>
public class CommandInvocationContext
{
    public string CommandId { get; init; } = string.Empty;
    public object? Parameter { get; init; }
    public bool IsCancelled { get; set; }
    public string? CancellationReason { get; set; }
    public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Detailed descriptor for registering a custom command into the IDE.
/// </summary>
public class CustomCommandDescriptor
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Category { get; init; }
    public string? Description { get; init; }
    public string? Shortcut { get; init; }
    public Action Action { get; init; } = null!;
}
