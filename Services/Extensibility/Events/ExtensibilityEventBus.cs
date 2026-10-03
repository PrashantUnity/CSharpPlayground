using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using FrySharp.Sdk;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Events;

/// <summary>
/// Thread-safe publish/subscribe event bus for decoupled communication across extensions and scripts.
/// </summary>
public class ExtensibilityEventBus : IEventBusApi
{
    private readonly ConcurrentDictionary<string, List<Delegate>> _subscriptions = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public void Publish<T>(string topic, T payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        List<Delegate> targets;
        lock (_lock)
        {
            if (!_subscriptions.TryGetValue(topic, out var list) || list.Count == 0) return;
            targets = new List<Delegate>(list);
        }

        foreach (var handler in targets)
        {
            try
            {
                if (handler is Action<T> typed)
                {
                    typed(payload);
                }
                else if (handler is Action parameterless)
                {
                    parameterless();
                }
                else
                {
                    handler.DynamicInvoke(payload);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExtensibilityEventBus] Error in handler for '{topic}': {ex.Message}");
            }
        }
    }

    public void Publish(string topic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        List<Delegate> targets;
        lock (_lock)
        {
            if (!_subscriptions.TryGetValue(topic, out var list) || list.Count == 0) return;
            targets = new List<Delegate>(list);
        }

        foreach (var handler in targets)
        {
            try
            {
                if (handler is Action parameterless)
                {
                    parameterless();
                }
                else
                {
                    handler.DynamicInvoke(null);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExtensibilityEventBus] Error in parameterless handler for '{topic}': {ex.Message}");
            }
        }
    }

    public IDisposable Subscribe<T>(string topic, Action<T> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(handler);

        lock (_lock)
        {
            var list = _subscriptions.GetOrAdd(topic, _ => new List<Delegate>());
            list.Add(handler);
        }

        return new ActionDisposable(() =>
        {
            lock (_lock)
            {
                if (_subscriptions.TryGetValue(topic, out var list))
                {
                    list.Remove(handler);
                    if (list.Count == 0)
                    {
                        _subscriptions.TryRemove(topic, out _);
                    }
                }
            }
        });
    }

    public IDisposable Subscribe(string topic, Action handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(handler);

        lock (_lock)
        {
            var list = _subscriptions.GetOrAdd(topic, _ => new List<Delegate>());
            list.Add(handler);
        }

        return new ActionDisposable(() =>
        {
            lock (_lock)
            {
                if (_subscriptions.TryGetValue(topic, out var list))
                {
                    list.Remove(handler);
                    if (list.Count == 0)
                    {
                        _subscriptions.TryRemove(topic, out _);
                    }
                }
            }
        });
    }

    public void Clear(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic)) return;
        lock (_lock)
        {
            _subscriptions.TryRemove(topic, out _);
        }
    }

    private sealed class ActionDisposable : IDisposable
    {
        private Action? _action;
        public ActionDisposable(Action action) => _action = action;
        public void Dispose()
        {
            var act = System.Threading.Interlocked.Exchange(ref _action, null);
            act?.Invoke();
        }
    }
}
