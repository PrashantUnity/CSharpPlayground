using System.Reflection;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;

/// <summary>
/// What user C# compiles against in one host: the host's own assemblies, and the namespaces and types that a script, a
/// program with a Main, a notebook cell and a debugger watch all see without a using directive. The desktop studio and
/// the headless hosts (web server, CLI) give scripts different APIs, so each host states its profile once, before the
/// first compile, with <see cref="ScriptHost.Configure"/>.
/// </summary>
public sealed class ScriptHostProfile
{
    public ScriptHostProfile(
        string name,
        IEnumerable<Assembly> assemblies,
        IEnumerable<string> namespaces,
        IEnumerable<string>? staticTypes = null,
        IScriptHostDisplay? display = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(assemblies);
        ArgumentNullException.ThrowIfNull(namespaces);

        Name = name;
        Assemblies = assemblies.Distinct().ToArray();
        Namespaces = namespaces.Distinct(StringComparer.Ordinal).ToArray();
        StaticTypes = (staticTypes ?? []).Distinct(StringComparer.Ordinal).ToArray();
        Display = display;

        if (Assemblies.FirstOrDefault(a => a.IsDynamic) is { } dynamicAssembly)
            throw new ArgumentException($"A dynamic assembly ({dynamicAssembly.GetName().Name}) has no file to compile against.", nameof(assemblies));
    }

    /// <summary>Names the profile in errors, e.g. "desktop".</summary>
    public string Name { get; }

    /// <summary>The host's assemblies that scripts reference, besides .NET and what the host process can load.</summary>
    public IReadOnlyList<Assembly> Assemblies { get; }

    /// <summary>The imported namespaces.</summary>
    public IReadOnlyList<string> Namespaces { get; }

    /// <summary>Types imported with <c>using static</c>, so their members need no prefix.</summary>
    public IReadOnlyList<string> StaticTypes { get; }

    /// <summary>What only this host's UI can show (desktop controls and bitmaps), or null.</summary>
    public IScriptHostDisplay? Display { get; }

    /// <summary>The .NET namespaces every host imports.</summary>
    public static IReadOnlyList<string> SystemNamespaces { get; } =
    [
        "System",
        "System.Collections",
        "System.Collections.Generic",
        "System.Data",
        "System.Diagnostics",
        "System.IO",
        "System.Linq",
        "System.Net.Http",
        "System.Text",
        "System.Text.Json",
        "System.Text.RegularExpressions",
        "System.Threading.Tasks",
    ];

    /// <summary>The profile of a host without the desktop studio: the CLI, the web server and tests of Core.</summary>
    public static ScriptHostProfile Headless { get; } = new(
        "headless",
        [typeof(ScriptHostProfile).Assembly],
        [
            .. SystemNamespaces,
            "PdfEditorApp.Plugins.CSharpEditor.Scripting", // Display
            "PdfEditorApp.Plugins.CSharpEditor.Services.Display", // Dump, EChart, Charts
            "PdfEditorApp.Plugins.CSharpEditor.Services.Execution",
            "PdfEditorApp.Plugins.CSharpEditor.Models",
            "PdfEditorApp.Plugins.CSharpEditor.Visuals.Spec",
            "PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction",
            "PdfEditorApp.Plugins.CSharpEditor.Charting.Models",
            "PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models",
            "PdfEditorApp.Plugins.CSharpEditor.Charting3D.Spatial",
            "PdfEditorApp.Plugins.CSharpEditor.Charting3D.Layouts",
            "PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models",
            "PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services",
        ]);
}

/// <summary>
/// The process's <see cref="ScriptHostProfile"/>. A host configures it once at start-up. The first compile fixes it, because
/// the compiler, completion and hover cache the imports and references it gives. Without a configured profile, scripts
/// compile against <see cref="ScriptHostProfile.Headless"/>.
/// </summary>
public static class ScriptHost
{
    private static readonly Lock Gate = new();
    private static ScriptHostProfile? _configured;
    private static volatile ScriptHostProfile? _active;

    /// <summary>The profile scripts compile against. Reading it fixes it.</summary>
    public static ScriptHostProfile Profile
    {
        get
        {
            if (_active is { } active) return active;
            lock (Gate)
            {
                return _active ??= _configured ?? ScriptHostProfile.Headless;
            }
        }
    }

    /// <summary>
    /// Sets the profile. Calling it again with the same profile does nothing. A different profile after the first one,
    /// or after the first compile, throws: two hosts in one process would give scripts two APIs.
    /// </summary>
    public static void Configure(ScriptHostProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        lock (Gate)
        {
            var current = _active ?? _configured;
            if (current == null)
            {
                _configured = profile;
                return;
            }

            if (!ReferenceEquals(current, profile))
            {
                throw new InvalidOperationException(
                    $"Scripts already use the '{current.Name}' profile, so the '{profile.Name}' profile can't be configured. " +
                    "Configure the host's profile once, before the first compile.");
            }
        }
    }
}
