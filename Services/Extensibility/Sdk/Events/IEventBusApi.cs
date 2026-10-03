using System;

namespace FrySharp.Sdk;

/// <summary>
/// Thread-safe publish-subscribe event bus enabling decoupled communication across scripts,
/// extensions, notebooks, and studio components.
/// </summary>
public interface IEventBusApi
{
    /// <summary>Publishes an event payload to all subscribers registered on the specified topic.</summary>
    void Publish<T>(string topic, T payload);

    /// <summary>Publishes a notification without payload to all subscribers on the specified topic.</summary>
    void Publish(string topic);

    /// <summary>Subscribes to events on a topic. Returns an IDisposable to unsubscribe.</summary>
    IDisposable Subscribe<T>(string topic, Action<T> handler);

    /// <summary>Subscribes to parameterless notifications on a topic. Returns an IDisposable to unsubscribe.</summary>
    IDisposable Subscribe(string topic, Action handler);

    /// <summary>Clears all subscriptions on a topic.</summary>
    void Clear(string topic);
}
