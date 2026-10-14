namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

/// <summary>Languages by name, as notebook routing and <c>#!</c> directives read them, whatever host owns them.</summary>
public interface ILanguageCatalog
{
    /// <summary>The language with this id or alias ("python", "py", "c#"), or null.</summary>
    ILanguageEngine? FindLanguage(string? idOrAlias);

    /// <summary>Languages a notebook cell can be written in.</summary>
    IReadOnlyList<ILanguageEngine> NotebookCellLanguages { get; }
}

/// <summary>
/// The languages a host knows, looked up by id, alias or file extension. The desktop keeps its full language
/// definitions (editor included) in one; a headless host keeps engines.
/// </summary>
public class LanguageRegistry<TLanguage> : ILanguageCatalog where TLanguage : class, ILanguageEngine
{
    private readonly Lock _gate = new();
    private readonly List<TLanguage> _languages = new();

    public IReadOnlyList<TLanguage> All
    {
        get
        {
            lock (_gate) return _languages.ToArray();
        }
    }

    /// <summary>Languages kept as plain source files, in registration order (these get "New … File" entries).</summary>
    public IReadOnlyList<TLanguage> SourceFileLanguages =>
        All.Where(l => l.Storage == LanguageStorageKind.SourceFile && l.Id != LanguageIds.Text).ToArray();

    /// <summary>Languages a notebook cell can be written in.</summary>
    public IReadOnlyList<TLanguage> NotebookLanguages =>
        All.Where(l => l.NotebookKernels != null && l.Has(LanguageCapabilities.NotebookCells)).ToArray();

    /// <summary>Fired when a new language is registered.</summary>
    public event Action<TLanguage>? LanguageRegistered;

    /// <summary>Fired when an existing language is unregistered.</summary>
    public event Action<TLanguage>? LanguageUnregistered;

    /// <summary>Fired whenever languages are registered or unregistered.</summary>
    public event Action? Changed;

    public IDisposable Register(TLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        lock (_gate)
        {
            foreach (var existing in _languages)
            {
                if (existing.IsNamed(language.Id) || language.Aliases.Any(existing.IsNamed))
                {
                    throw new InvalidOperationException($"A language named '{language.Id}' is already registered ({existing.DisplayName}).");
                }

                var clash = language.FileExtensions.FirstOrDefault(e =>
                    existing.FileExtensions.Contains(e, StringComparer.OrdinalIgnoreCase));
                if (clash != null)
                {
                    throw new InvalidOperationException($"'{clash}' files already belong to {existing.DisplayName}.");
                }
            }

            _languages.Add(language);
        }

        LanguageRegistered?.Invoke(language);
        Changed?.Invoke();
        return new RegistrationToken(this, language.Id);
    }

    /// <summary>Unregisters a language by its ID or alias. Returns true if it was found and removed.</summary>
    public bool Unregister(string? idOrAlias)
    {
        if (string.IsNullOrWhiteSpace(idOrAlias)) return false;

        TLanguage? removed = null;
        lock (_gate)
        {
            var existing = _languages.FirstOrDefault(l => l.IsNamed(idOrAlias));
            if (existing != null)
            {
                _languages.Remove(existing);
                removed = existing;
            }
        }

        if (removed != null)
        {
            LanguageUnregistered?.Invoke(removed);
            Changed?.Invoke();
            return true;
        }

        return false;
    }

    private sealed class RegistrationToken(LanguageRegistry<TLanguage> registry, string id) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                registry.Unregister(id);
            }
        }
    }

    /// <summary>The language with this id or alias ("python", "py", "c#"), or null.</summary>
    public TLanguage? Get(string? idOrAlias)
    {
        if (string.IsNullOrWhiteSpace(idOrAlias)) return null;
        lock (_gate) return _languages.FirstOrDefault(l => l.IsNamed(idOrAlias));
    }

    /// <summary>The language a file belongs to, from a path or an extension (".py"), or null.</summary>
    public TLanguage? FindByExtension(string? pathOrExtension)
    {
        if (string.IsNullOrWhiteSpace(pathOrExtension)) return null;
        var extension = pathOrExtension.StartsWith('.') && pathOrExtension.IndexOfAny(['/', '\\']) < 0
            ? pathOrExtension
            : Path.GetExtension(pathOrExtension);
        if (string.IsNullOrEmpty(extension)) return null;

        lock (_gate)
        {
            return _languages.FirstOrDefault(l => l.FileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase));
        }
    }

    /// <summary>The source-file language for a path, or null when the file isn't a source file the studio opens as text.</summary>
    public TLanguage? FindSourceFileLanguage(string? path)
    {
        var language = FindByExtension(path);
        return language?.Storage == LanguageStorageKind.SourceFile ? language : null;
    }

    ILanguageEngine? ILanguageCatalog.FindLanguage(string? idOrAlias) => Get(idOrAlias);

    IReadOnlyList<ILanguageEngine> ILanguageCatalog.NotebookCellLanguages => NotebookLanguages;
}
