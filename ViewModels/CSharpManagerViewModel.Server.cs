using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;

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
