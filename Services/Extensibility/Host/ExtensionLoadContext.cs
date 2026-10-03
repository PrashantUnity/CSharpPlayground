using System;
using System.Reflection;
using System.Runtime.Loader;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Host;

/// <summary>
/// Collectible AssemblyLoadContext for isolated script and extension loading.
/// Enables unloading and hot reloading user-written C# code without restarting the application.
/// </summary>
public sealed class ExtensionLoadContext : AssemblyLoadContext
{
    private static readonly string[] SharedAssemblyNames =
    [
        "CSharpEditorPlugin",
        "FrySharp",
        "FrySharp.Sdk",
        "Avalonia",
        "Avalonia.Base",
        "Avalonia.Controls",
        "Avalonia.Markup",
        "Avalonia.Markup.Xaml",
        "Avalonia.Styling",
        "Avalonia.Media",
        "CommunityToolkit.Mvvm",
        "Material.Icons.Avalonia"
    ];

    public ExtensionLoadContext(string? name = null)
        : base(name: name ?? $"Ext_{Guid.NewGuid():N}", isCollectible: true)
    {
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name != null)
        {
            // Share the host and SDK assemblies so interface types match exactly
            foreach (var sharedName in SharedAssemblyNames)
            {
                if (string.Equals(assemblyName.Name, sharedName, StringComparison.OrdinalIgnoreCase))
                {
                    return typeof(StudioAppContext).Assembly.GetName().Name == assemblyName.Name
                        ? typeof(StudioAppContext).Assembly
                        : Default.LoadFromAssemblyName(assemblyName);
                }
            }
        }

        return null; // Fallback to standard resolution
    }
}
