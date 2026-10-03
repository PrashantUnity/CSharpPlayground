using System;
using System.Collections.Generic;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Languages;

/// <summary>
/// Bridge implementation connecting the public FrySharp.Sdk ILanguagesApi to the internal LanguageRegistry.
/// </summary>
public class ExtensibilityLanguagesService : ILanguagesApi
{
    private readonly Func<LanguageRegistry> _registryProvider;

    public ExtensibilityLanguagesService(Func<LanguageRegistry> registryProvider)
    {
        _registryProvider = registryProvider ?? throw new ArgumentNullException(nameof(registryProvider));
    }

    public ExtensibilityLanguagesService(LanguageRegistry registry)
        : this(() => registry ?? throw new ArgumentNullException(nameof(registry)))
    {
    }

    private LanguageRegistry Registry => _registryProvider();

    public IReadOnlyList<ILanguageDefinition> All => Registry.All;

    public IReadOnlyList<ILanguageDefinition> SourceFileLanguages => Registry.SourceFileLanguages;

    public IReadOnlyList<ILanguageDefinition> NotebookLanguages => Registry.NotebookLanguages;

    public ILanguageDefinition? Get(string idOrAlias) => Registry.Get(idOrAlias);

    public ILanguageDefinition? FindByExtension(string pathOrExtension) => Registry.FindByExtension(pathOrExtension);

    public IDisposable Register(ILanguageDefinition language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return Registry.Register(language);
    }

    public bool Unregister(string languageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageId);
        return Registry.Unregister(languageId);
    }
}
