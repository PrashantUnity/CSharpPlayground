using System;
using System.Collections.Generic;

namespace FrySharp.Sdk;

/// <summary>
/// Serializable theme definition for dark/light themes, color tokens, and typography.
/// </summary>
public class ThemeDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsDark { get; init; } = true;
    public Dictionary<string, string> Colors { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Fonts { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, double> Numbers { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
