using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Core;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Catalogs.Blind75.MultiLanguage;

/// <summary>
/// Central registry and enricher of multi-language solution implementations (Python, JavaScript, Java)
/// for Blind 75 curriculum problems.
/// </summary>
public static partial class Blind75MultiLanguageSolutions
{
    private static readonly ConcurrentDictionary<int, Action<BlindProblemItem>> _enrichers = new();

    static Blind75MultiLanguageSolutions()
    {
        RegisterArraysAndHashing();
        RegisterTwoPointersAndSlidingWindow();
        RegisterLinkedListsAndTrees();
        RegisterDynamicProgrammingAndIntervals();
    }

    private static void Register(int problemNumber, Action<BlindProblemItem> enrichAction)
    {
        _enrichers[problemNumber] = enrichAction;
    }

    /// <summary>
    /// Attaches curated multi-language solutions and starter templates to the problem item.
    /// </summary>
    public static void Enrich(BlindProblemItem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        if (_enrichers.TryGetValue(problem.Number, out var enricher))
        {
            enricher(problem);
        }
    }

    static partial void RegisterArraysAndHashing();
    static partial void RegisterTwoPointersAndSlidingWindow();
    static partial void RegisterLinkedListsAndTrees();
    static partial void RegisterDynamicProgrammingAndIntervals();
}
