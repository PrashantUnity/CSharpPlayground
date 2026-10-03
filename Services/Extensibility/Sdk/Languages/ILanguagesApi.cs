using System;
using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

namespace FrySharp.Sdk;

/// <summary>
/// Language registry and file-type association management.
/// Allows extensions to query, register, and unregister programming languages.
/// </summary>
public interface ILanguagesApi
{
    /// <summary>All currently registered languages in the studio.</summary>
    IReadOnlyList<ILanguageDefinition> All { get; }

    /// <summary>Languages kept as plain source files (these appear in "New File" options).</summary>
    IReadOnlyList<ILanguageDefinition> SourceFileLanguages { get; }

    /// <summary>Languages a notebook cell can execute in.</summary>
    IReadOnlyList<ILanguageDefinition> NotebookLanguages { get; }

    /// <summary>Finds a language by its ID or alias (e.g. "python", "py", "zig").</summary>
    ILanguageDefinition? Get(string idOrAlias);

    /// <summary>The language a file belongs to, from a path or an extension (".py"), or null.</summary>
    ILanguageDefinition? FindByExtension(string pathOrExtension);

    /// <summary>
    /// Registers a new language definition with the studio. Returns an <see cref="IDisposable"/> token
    /// that automatically unregisters the language when disposed.
    /// </summary>
    IDisposable Register(ILanguageDefinition language);

    /// <summary>Unregisters a language by its ID or alias. Returns true if removed.</summary>
    bool Unregister(string languageId);
}
