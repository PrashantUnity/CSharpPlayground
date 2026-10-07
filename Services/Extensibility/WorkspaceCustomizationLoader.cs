using System.IO;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Services.Activities;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;

/// <summary>
/// Loads a workspace's own customization script and extensions (<c>.frysharp/</c>) after the workspace is opened. It
/// runs in the background as a status-bar activity; a broken extension is reported to the user instead of vanishing.
/// </summary>
public static class WorkspaceCustomizationLoader
{
    public static void LoadInBackground(string? workspaceFolder, IActivityService activities)
    {
        if (string.IsNullOrWhiteSpace(workspaceFolder)) return;
        activities.RunAsync(new ActivityOptions("Loading workspace extensions") { Detail = Path.GetFileName(workspaceFolder) }, _ => Task.Run(async () =>
        {
            var app = StudioAppContext.Instance;
            await app.CustomizationManager.LoadWorkspaceCustomizationsAsync(workspaceFolder);
            var workspaceExtDir = Path.Combine(workspaceFolder, ".frysharp", "extensions");
            if (Directory.Exists(workspaceExtDir))
            {
                await app.ExtensionManager.DiscoverAndLoadAllAsync(workspaceExtDir, enableHotReload: true);
            }
        })).FireAndForget(activities, "Loading workspace extensions");
    }
}
