using System;

namespace FrySharp.Sdk;

/// <summary>
/// Thread-safe state bag that persists values across hot-reloads and script executions.
/// </summary>
public interface IStateBag
{
    /// <summary>Gets an existing value, or generates and stores it if not present.</summary>
    T GetOrSet<T>(string key, Func<T> factory);

    /// <summary>Gets a value by key, or default(T) if not present.</summary>
    T? Get<T>(string key);

    /// <summary>Sets a value by key.</summary>
    void Set<T>(string key, T value);

    /// <summary>Removes a key from the state bag.</summary>
    bool Remove(string key);

    /// <summary>Clears all state.</summary>
    void Clear();

    /// <summary>Checks whether a key exists in the state bag.</summary>
    bool Contains(string key);

    /// <summary>Indexer access for dynamic objects.</summary>
    object? this[string key] { get; set; }
}
