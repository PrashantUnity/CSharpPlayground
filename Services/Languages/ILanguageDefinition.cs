using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Indentation;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

/// <summary>
/// Everything the studio needs to know about one programming language. A language is added by implementing this
/// (usually by deriving from <see cref="LanguageDefinition"/>) and registering it in <see cref="LanguageRegistry"/>:
/// storage, the Explorer, Code Studio, notebooks, the Hub and the toolchain picker all work from it. See
/// docs/adding-a-language.md.
/// </summary>
public interface ILanguageDefinition
{
    /// <summary>Lower-case id, e.g. <c>python</c>. Stored in notebook cells, so it must never change.</summary>
    string Id { get; }

    string DisplayName { get; }

    /// <summary>Short chip text, e.g. "C#" or "PY".</summary>
    string ShortName { get; }

    /// <summary>Other names accepted by <c>#!name</c> and <c>#!share --from name</c>, lower case. The id always works.</summary>
    IReadOnlyList<string> Aliases { get; }

    /// <summary>File extensions, lower case with the dot. The first one names new files.</summary>
    IReadOnlyList<string> FileExtensions { get; }

    LanguageStorageKind Storage { get; }

    LanguageCapabilities Capabilities { get; }

    /// <summary>A Material icon name (<c>MaterialIconKind</c>), e.g. <c>LanguagePython</c>.</summary>
    string IconKind { get; }

    string AccentHex { get; }

    /// <summary>What Ctrl+/ puts in front of a line.</summary>
    string LineCommentPrefix { get; }

    /// <summary>The text a new file of this language starts with.</summary>
    string NewFileTemplate { get; }

    /// <summary>How the language runs, for the breadcrumbs when it has no toolchain to name (C#: "C# (.NET 10 Roslyn)").</summary>
    string RuntimeDescription { get; }

    /// <summary>Syntax colors for the editor, or null for plain text.</summary>
    IHighlightingDefinition? GetHighlighting(bool isDark);

    IIndentationStrategy CreateIndentationStrategy(TextEditorOptions options);

    /// <summary>Collapsible regions, or null when the language has none.</summary>
    ILanguageFolding? Folding { get; }

    /// <summary>Completion, hover or live diagnostics attached to an editor (e.g. through a language server), or null.</summary>
    IEditorAssistantFactory? EditorAssistants { get; }

    /// <summary>Finds the program that runs this language, or null when it runs inside the studio (C#).</summary>
    IToolchainProvider? Toolchain { get; }

    /// <summary>Turns a source file into the commands that build and run it, or null for the built-in C# runner.</summary>
    IScriptRunner? ScriptRunner { get; }

    /// <summary>Reads errors out of a run's output for the Problems panel.</summary>
    IDiagnosticParser? RunDiagnostics { get; }

    /// <summary>Creates the kernel that runs this language's notebook cells, or null when it has none.</summary>
    INotebookKernelFactory? NotebookKernels { get; }

    IPackageManager? Packages { get; }

    /// <summary>How Jupyter knows the language, for .ipynb export; null when it has no Jupyter kernel.</summary>
    JupyterLanguageInfo? Jupyter { get; }
}

/// <summary>Collapsible regions of a document.</summary>
public interface ILanguageFolding
{
    IEnumerable<NewFolding> CreateFoldings(TextDocument document, out int firstErrorOffset);
}

/// <summary>
/// Editor help for a language that the studio doesn't implement itself: completion, hover, live diagnostics. Attached
/// when a document of the language is shown and disposed when another one replaces it.
/// </summary>
public interface IEditorAssistantFactory
{
    IDisposable Attach(TextEditor editor, EditorAssistantContext context);
}

/// <param name="PrecedingCode">Code that runs before this editor's text (earlier notebook cells), if any.</param>
/// <param name="IsSuppressed">True while the assistant should stay quiet (e.g. paused in the debugger).</param>
public sealed record EditorAssistantContext(Func<string>? PrecedingCode = null, Func<bool>? IsSuppressed = null);

/// <summary>Defaults for the members most languages don't need, so a language only overrides what it has.</summary>
public abstract class LanguageDefinition : ILanguageDefinition
{
    public abstract string Id { get; }
    public abstract string DisplayName { get; }
    public virtual string ShortName => DisplayName;
    public virtual IReadOnlyList<string> Aliases => Array.Empty<string>();
    public abstract IReadOnlyList<string> FileExtensions { get; }
    public virtual LanguageStorageKind Storage => LanguageStorageKind.SourceFile;
    public virtual LanguageCapabilities Capabilities => LanguageCapabilities.None;
    public virtual string IconKind => "FileCodeOutline";
    public virtual string AccentHex => "#8B949E";
    public virtual string LineCommentPrefix => "#";
    public virtual string NewFileTemplate => string.Empty;
    public virtual string RuntimeDescription => DisplayName;

    public virtual IHighlightingDefinition? GetHighlighting(bool isDark) => null;
    public virtual IIndentationStrategy CreateIndentationStrategy(TextEditorOptions options) => new DefaultIndentationStrategy();
    public virtual ILanguageFolding? Folding => null;
    public virtual IEditorAssistantFactory? EditorAssistants => null;
    public virtual IToolchainProvider? Toolchain => null;
    public virtual IScriptRunner? ScriptRunner => null;
    public virtual IDiagnosticParser? RunDiagnostics => null;
    public virtual INotebookKernelFactory? NotebookKernels => null;
    public virtual IPackageManager? Packages => null;
    public virtual JupyterLanguageInfo? Jupyter => null;

    public override string ToString() => DisplayName;
}

public static class LanguageDefinitionExtensions
{
    /// <summary>True when the language has every one of <paramref name="capabilities"/>.</summary>
    public static bool Has(this ILanguageDefinition language, LanguageCapabilities capabilities) =>
        (language.Capabilities & capabilities) == capabilities;

    /// <summary>True for <paramref name="name"/> matching the id or an alias, ignoring case.</summary>
    public static bool IsNamed(this ILanguageDefinition language, string? name) =>
        !string.IsNullOrWhiteSpace(name) &&
        (string.Equals(language.Id, name.Trim(), StringComparison.OrdinalIgnoreCase) ||
         language.Aliases.Any(a => string.Equals(a, name.Trim(), StringComparison.OrdinalIgnoreCase)));

    public static string DefaultExtension(this ILanguageDefinition language) =>
        language.FileExtensions.Count > 0 ? language.FileExtensions[0] : string.Empty;
}

/// <summary>A language as Jupyter knows it: the kernelspec and language_info of an exported .ipynb.</summary>
public sealed record JupyterLanguageInfo(
    string KernelName,
    string KernelDisplayName,
    string KernelLanguage,
    string LanguageName,
    string FileExtension,
    string MimeType,
    string? Version = null);
