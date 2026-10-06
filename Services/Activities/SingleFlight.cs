using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Activities;

/// <summary>
/// Joins concurrent loads of the same key: a second request for a document that is already loading waits for that load
/// instead of reading and parsing the file again. Finished loads are not cached; the next request loads afresh.
/// </summary>
public sealed class SingleFlight<TKey, TValue> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, Lazy<Task<TValue>>> _inFlight;

    public SingleFlight(IEqualityComparer<TKey>? comparer = null)
    {
        _inFlight = new ConcurrentDictionary<TKey, Lazy<Task<TValue>>>(comparer ?? EqualityComparer<TKey>.Default);
    }

    /// <summary>How many loads are running (for tests).</summary>
    public int InFlightCount => _inFlight.Count;

    public Task<TValue> RunAsync(TKey key, Func<Task<TValue>> load)
    {
        ArgumentNullException.ThrowIfNull(load);
        Lazy<Task<TValue>>? mine = null;
        mine = new Lazy<Task<TValue>>(() => LoadAndForget(key, load, mine!));
        return _inFlight.GetOrAdd(key, mine).Value;
    }

    private async Task<TValue> LoadAndForget(TKey key, Func<Task<TValue>> load, Lazy<Task<TValue>> entry)
    {
        try
        {
            return await load().ConfigureAwait(false);
        }
        finally
        {
            // Only this load's own entry: a newer load of the same key may already have replaced it.
            _inFlight.TryRemove(new KeyValuePair<TKey, Lazy<Task<TValue>>>(key, entry));
        }
    }
}
