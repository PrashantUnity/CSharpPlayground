using System.Text;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

/// <summary>An installed program that runs a language: a Python interpreter, a JDK, a C++ compiler.</summary>
public sealed record ToolchainInfo
{
    private static readonly IReadOnlyDictionary<string, string> NoProperties = new Dictionary<string, string>();

    public required string LanguageId { get; init; }
    public required string ExecutablePath { get; init; }
    public required Version Version { get; init; }

    /// <summary>E.g. "Python 3.14.6".</summary>
    public required string DisplayName { get; init; }

    /// <summary>Where it came from, e.g. "Homebrew", "Project .venv", "Studio environment".</summary>
    public required string Source { get; init; }

    /// <summary>Language-specific facts from probing it (e.g. whether pip is available).</summary>
    public IReadOnlyDictionary<string, string> Properties { get; init; } = NoProperties;

    /// <summary>"Python 3.14.6 (Homebrew)".</summary>
    public string Label => $"{DisplayName} ({Source})";

    public string? Get(string key) => Properties.TryGetValue(key, out var value) ? value : null;

    public bool Is(string key) => string.Equals(Get(key), "true", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Where a toolchain is needed: the document's folder (project environments) and the workspace root.</summary>
public sealed record ToolchainQuery(string? DocumentFolder = null, string? WorkspaceRoot = null);

/// <summary>What to tell the user when a toolchain isn't installed.</summary>
/// <param name="Title">E.g. "Python isn't installed".</param>
/// <param name="Summary">Why it's needed and what happens next.</param>
/// <param name="Steps">Ways to install it on this OS, e.g. <c>brew install python</c>.</param>
public sealed record MissingToolchainGuidance(string Title, string Summary, IReadOnlyList<string> Steps, string? DownloadUrl = null)
{
    public string ToText()
    {
        var text = new StringBuilder();
        text.Append(Title).Append('\n').Append(Summary).Append('\n');
        foreach (var step in Steps) text.Append("  • ").Append(step).Append('\n');
        if (!string.IsNullOrEmpty(DownloadUrl)) text.Append("  • Download: ").Append(DownloadUrl).Append('\n');
        return text.ToString();
    }
}

/// <summary>The toolchain to use, or what's missing.</summary>
public sealed record ToolchainResolution(ToolchainInfo? Toolchain, MissingToolchainGuidance? Missing = null)
{
    public bool IsFound => Toolchain != null;

    public static ToolchainResolution Found(ToolchainInfo toolchain) => new(toolchain);

    public static ToolchainResolution NotFound(MissingToolchainGuidance guidance) => new(null, guidance);
}

/// <summary>A command the toolchain picker offers besides choosing one, e.g. "Create studio environment".</summary>
public sealed record ToolchainAction(string Id, string Label, string Description);

public sealed record ToolchainActionResult(bool Success, string Message, ToolchainInfo? Toolchain = null);
