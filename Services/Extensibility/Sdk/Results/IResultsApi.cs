using System;

namespace FrySharp.Sdk;

/// <summary>
/// Public API for pushing rich visuals, controls, tables, and objects to the Results (.Dump) panel.
/// </summary>
public interface IResultsApi
{
    /// <summary>Displays an object in the Results (.Dump) tab using rich visual formatting.</summary>
    void Show(object? value, string? title = null);

    /// <summary>Displays an Avalonia Control directly in the Results panel.</summary>
    void ShowControl(object control, string? title = null);

    /// <summary>Displays tabular data (e.g. DataTable or collection) with interactive column sorting.</summary>
    void ShowTable(object data, string? title = null);

    /// <summary>Clears all Results (.Dump) entries.</summary>
    void Clear();

    /// <summary>Switches active Bottom Deck tab to Results (.Dump) and expands the deck.</summary>
    void Focus();
}
