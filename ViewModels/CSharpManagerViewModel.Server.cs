using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Server;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

public partial class CSharpManagerViewModel
{
    public int TotalServers => AllItems.Count(i => i.IsServer);
    public bool IsServersFilterActive => SelectedTypeFilter == "Servers";
    public bool IsServersNavActive => IsWorkspaceSectionActive && IsServersFilterActive;
    public bool IsCreatingServer => PendingCreateKind == WorkspaceItemKind.Server;

    public WorkspaceItemSummary? RecentServer => PinnedItem1?.IsServer == true ? PinnedItem1
        : (PinnedItem2?.IsServer == true ? PinnedItem2 : AllItems.FirstOrDefault(i => i.IsServer));

    public bool HasRecentServer => RecentServer != null;
    public string RecentServerTitle => RecentServer?.Title ?? "API Server";
    public string RecentServerPath => RecentServer?.DisplayLocation ?? "~/library/";
    public string RecentServerTime => RecentServer?.FormattedLastModified ?? "Just now";

    // Active running servers across the studio
    public ObservableCollection<FryRunningServerItem> RunningServers { get; } = new();
    public int RunningServerCount => RunningServers.Count;
    public bool HasRunningServers => RunningServers.Count > 0;

    private void OnRunningServersChanged()
    {
        Dispatcher.UIThread.Post(SyncRunningServers);
    }

    public void SyncRunningServers()
    {
        RunningServers.Clear();
        foreach (var server in _serverRegistry.RunningServers)
        {
            RunningServers.Add(server);
        }
        OnPropertyChanged(nameof(RunningServerCount));
        OnPropertyChanged(nameof(HasRunningServers));
    }

    [RelayCommand]
    public async Task StopRunningServerAsync(FryRunningServerItem? server)
    {
        if (server == null) return;
        await _serverRegistry.StopServerAsync(server.ServerId).ConfigureAwait(false);
    }

    [RelayCommand]
    public async Task StopAllRunningServersAsync()
    {
        await _serverRegistry.StopAllAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    public void OpenRunningServer(FryRunningServerItem? server)
    {
        if (server?.Document != null)
        {
            _openServerAction?.Invoke(server.Document);
        }
    }

    [RelayCommand]
    public async Task CopyServerUrlAsync(FryRunningServerItem? server)
    {
        if (server == null) return;
        try
        {
            var clipboard = Avalonia.Application.Current?.ApplicationLifetime switch
            {
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop => desktop.MainWindow?.Clipboard,
                Avalonia.Controls.ApplicationLifetimes.ISingleViewApplicationLifetime singleView => Avalonia.Controls.TopLevel.GetTopLevel(singleView.MainView)?.Clipboard,
                _ => null
            };
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(server.BaseUrl).ConfigureAwait(false);
            }
        }
        catch
        {
        }
    }

    [RelayCommand]
    public async Task CreateNewServerAsync(string? templateId = null)
    {
        if (IsLaunching || IsLoading) return;
        await OpenCreatePromptAsync(WorkspaceItemKind.Server, templateId, "New API Server");
    }

    private async Task CreateNewServerCoreAsync(string? templateId, string? folderPath = null, string? explicitTitle = null)
    {
        var title = explicitTitle ?? "New API Server";

        if (AllItems.Any(i => i.IsServer && string.Equals(i.Title, title, StringComparison.OrdinalIgnoreCase)))
        {
            title = $"{title} (Copy)";
        }

        var newServer = await _storageService.CreateNewServerDocumentAsync(title, folderPath);
        await LoadWorkspaceItemsAsync();
        _openServerAction?.Invoke(newServer);
    }
}
