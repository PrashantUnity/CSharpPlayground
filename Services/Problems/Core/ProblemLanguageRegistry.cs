using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Core;

/// <summary>
/// Registry of all supported programming languages for Blind 75 curriculum problems.
/// </summary>
public static class ProblemLanguageRegistry
{
    private static readonly ConcurrentDictionary<string, IProblemLanguageAdapter> _adapters =
        new(StringComparer.OrdinalIgnoreCase);

    private static IProblemLanguageAdapter? _defaultAdapter;

    public static void Register(IProblemLanguageAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        _adapters[adapter.LanguageId] = adapter;
        if (adapter.LanguageId.Equals("csharp", StringComparison.OrdinalIgnoreCase))
        {
            _defaultAdapter = adapter;
        }
    }

    public static IProblemLanguageAdapter GetAdapter(string? languageId = null)
    {
        if (!string.IsNullOrWhiteSpace(languageId) && _adapters.TryGetValue(languageId, out var adapter))
        {
            return adapter;
        }

        return _defaultAdapter ?? _adapters.Values.FirstOrDefault()
            ?? throw new InvalidOperationException("No problem language adapters registered in ProblemLanguageRegistry.");
    }

    public static bool TryGetAdapter(string languageId, out IProblemLanguageAdapter? adapter) =>
        _adapters.TryGetValue(languageId, out adapter);

    public static IReadOnlyList<IProblemLanguageAdapter> GetAllAdapters() =>
        _adapters.Values.OrderBy(a => a.LanguageId == "csharp" ? 0 : 1).ThenBy(a => a.DisplayName).ToList();

    public static IReadOnlyList<string> SupportedLanguageIds =>
        _adapters.Keys.ToList();
}
