using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Models.Server;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Server;

public class FryServerRegistry : IFryServerRegistry
{
    private static FryServerRegistry? _shared;
    public static FryServerRegistry Shared => _shared ??= new FryServerRegistry();

    private readonly ConcurrentDictionary<string, FryRunningServerItem> _servers = new();

    public IReadOnlyList<FryRunningServerItem> RunningServers => _servers.Values.ToList();
    public int RunningCount => _servers.Count;

    public event Action? RunningServersChanged;

    public void RegisterServer(FryRunningServerItem item)
    {
        if (string.IsNullOrEmpty(item.ServerId)) return;

        // Unsubscribe from previous if any
        if (_servers.TryGetValue(item.ServerId, out var existing))
        {
            existing.Engine.RequestProcessed -= OnRequestProcessed;
            existing.Engine.StateChanged -= OnStateChanged;
        }

        _servers[item.ServerId] = item;

        void OnRequestProcessed(FryServerTrafficLogItem _)
        {
            item.NotifyMetricsChanged();
        }

        void OnStateChanged(ServerLifecycleState state)
        {
            item.NotifyMetricsChanged();
            if (state is ServerLifecycleState.Stopped or ServerLifecycleState.Faulted)
            {
                UnregisterServer(item.ServerId);
            }
        }

        item.Engine.RequestProcessed += OnRequestProcessed;
        item.Engine.StateChanged += OnStateChanged;

        NotifyChanged();
    }

    public void UnregisterServer(string serverId)
    {
        if (_servers.TryRemove(serverId, out _))
        {
            NotifyChanged();
        }
    }

    public FryRunningServerItem? GetServer(string serverId)
    {
        return _servers.TryGetValue(serverId, out var item) ? item : null;
    }

    public FryRunningServerItem? GetServerByPort(int port)
    {
        return _servers.Values.FirstOrDefault(s => s.BoundPort == port);
    }

    public async Task StopServerAsync(string serverId)
    {
        if (_servers.TryGetValue(serverId, out var item))
        {
            try
            {
                await item.Engine.StopAsync().ConfigureAwait(false);
            }
            finally
            {
                UnregisterServer(serverId);
            }
        }
    }

    public async Task StopAllAsync()
    {
        var list = _servers.Values.ToList();
        foreach (var item in list)
        {
            try
            {
                await item.Engine.StopAsync().ConfigureAwait(false);
            }
            catch { }
            UnregisterServer(item.ServerId);
        }
    }

    private void NotifyChanged()
    {
        RunningServersChanged?.Invoke();
    }
}
