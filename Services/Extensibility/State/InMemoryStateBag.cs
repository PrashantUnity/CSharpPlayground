using System;
using System.Collections.Concurrent;
using FrySharp.Sdk;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.State;

/// <summary>
/// Thread-safe in-memory implementation of IStateBag.
/// Persists across script evaluations and hot reloads within the same application process.
/// </summary>
public class InMemoryStateBag : IStateBag
{
    private readonly ConcurrentDictionary<string, object?> _storage = new(StringComparer.OrdinalIgnoreCase);

    public T GetOrSet<T>(string key, Func<T> factory)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(factory);

        var val = _storage.GetOrAdd(key, _ => factory());
        if (val is T typed) return typed;
        return (T)Convert.ChangeType(val!, typeof(T));
    }

    public T? Get<T>(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (_storage.TryGetValue(key, out var val))
        {
            if (val is T typed) return typed;
            if (val == null) return default;
            return (T)Convert.ChangeType(val, typeof(T));
        }

        return default;
    }

    public void Set<T>(string key, T value)
    {
        ArgumentNullException.ThrowIfNull(key);
        _storage[key] = value;
    }

    public bool Remove(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _storage.TryRemove(key, out _);
    }

    public void Clear()
    {
        _storage.Clear();
    }

    public bool Contains(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _storage.ContainsKey(key);
    }

    public object? this[string key]
    {
        get => _storage.TryGetValue(key, out var val) ? val : null;
        set => _storage[key] = value;
    }
}
