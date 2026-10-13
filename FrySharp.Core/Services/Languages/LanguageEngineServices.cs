using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

/// <summary>
/// What language engines share: the machine they're found on, the process launcher, the saved toolchain choices and
/// the data folder their kernels and environments are written to. Creating one touches nothing on disk.
/// </summary>
public sealed class LanguageEngineServices
{
    public LanguageEngineServices(string baseDirectory, IHostEnvironment host, IProcessLauncher processes, ToolchainSettingsStore toolchainSettings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        BaseDirectory = baseDirectory;
        Host = host ?? throw new ArgumentNullException(nameof(host));
        Processes = processes ?? throw new ArgumentNullException(nameof(processes));
        ToolchainSettings = toolchainSettings ?? throw new ArgumentNullException(nameof(toolchainSettings));
    }

    /// <summary>Services over <paramref name="baseDirectory"/> with the real machine and process launcher.</summary>
    public static LanguageEngineServices Create(string baseDirectory) => new(
        baseDirectory,
        new HostEnvironment(),
        new ProcessLauncher(),
        new ToolchainSettingsStore(Path.Combine(baseDirectory, "toolchains.json")));

    /// <summary>Where toolchain settings, environments and kernel files live.</summary>
    public string BaseDirectory { get; }

    public IHostEnvironment Host { get; }

    public IProcessLauncher Processes { get; }

    public ToolchainSettingsStore ToolchainSettings { get; }
}
