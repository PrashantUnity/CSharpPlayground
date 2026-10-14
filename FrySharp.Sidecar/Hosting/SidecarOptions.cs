namespace FrySharp.Sidecar.Hosting;

/// <summary>
/// Configuration for the headless sidecar.
/// </summary>
public sealed record SidecarOptions
{
    /// <summary>The root workspace directory the sidecar operates in.</summary>
    public required string WorkspaceRoot { get; init; }

    /// <summary>Directory where language environments and cache are stored.</summary>
    public string DataDirectory { get; init; } = DefaultDataDirectory();

    /// <summary>The largest file opened in memory (default 20MB).</summary>
    public long MaxOpenFileBytes { get; init; } = 20L * 1024 * 1024;

    public static string DefaultDataDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".frysharp", "sidecar");
}
