namespace PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

/// <summary>Finds, lists and remembers the installed programs a language runs with.</summary>
public interface IToolchainProvider
{
    string LanguageId { get; }

    /// <summary>What the toolchain is called in messages and the picker, e.g. "Python".</summary>
    string ToolName { get; }

    /// <summary>The toolchain a document should run with (a saved choice, a project environment, then what's installed), or what's missing.</summary>
    Task<ToolchainResolution> ResolveAsync(ToolchainQuery query, CancellationToken ct = default);

    /// <summary>Every usable toolchain found, for the picker.</summary>
    Task<IReadOnlyList<ToolchainInfo>> ListAsync(ToolchainQuery query, CancellationToken ct = default);

    /// <summary>The path the user picked, or null for automatic.</summary>
    string? SelectedPath { get; }

    /// <summary>Remembers the user's choice; null goes back to automatic.</summary>
    void Select(string? executablePath);

    /// <summary>Commands the picker offers besides choosing a toolchain.</summary>
    IReadOnlyList<ToolchainAction> Actions { get; }

    Task<ToolchainActionResult> RunActionAsync(string actionId, ToolchainQuery query, Action<string> output, CancellationToken ct = default);

    /// <summary>Forgets what was found, so a toolchain installed since is picked up.</summary>
    void Refresh();
}
