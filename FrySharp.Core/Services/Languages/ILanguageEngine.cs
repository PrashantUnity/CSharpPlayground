using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

/// <summary>
/// Headless core definition for a language: execution plans, toolchain resolution,
/// package management, diagnostics, and storage. Free of any GUI or editor dependencies.
/// </summary>
public interface ILanguageEngine
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

    /// <summary>True when the language relies on an ahead-of-time compiler (Java, C++, C) rather than a pure script interpreter.</summary>
    bool IsCompiled { get; }

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

    /// <summary>Finds the program that runs this language, or null when it runs inside the studio (C#).</summary>
    IToolchainProvider? Toolchain { get; }

    /// <summary>Turns a source file into the commands that build and run it, or null for the built-in C# runner.</summary>
    IScriptRunner? ScriptRunner { get; }

    /// <summary>Reads errors out of a run's output for the Problems panel.</summary>
    IDiagnosticParser? RunDiagnostics { get; }

    IPackageManager? Packages { get; }

    /// <summary>Creates the kernel that runs this language's notebook cells, or null when it has none.</summary>
    INotebookKernelFactory? NotebookKernels { get; }

    /// <summary>How Jupyter knows the language, for .ipynb export; null when it has no Jupyter kernel.</summary>
    JupyterLanguageInfo? Jupyter { get; }
}

public static class LanguageEngineExtensions
{
    /// <summary>True when the language has every one of <paramref name="capabilities"/>.</summary>
    public static bool Has(this ILanguageEngine language, LanguageCapabilities capabilities) =>
        (language.Capabilities & capabilities) == capabilities;

    /// <summary>True for <paramref name="name"/> matching the id or an alias, ignoring case.</summary>
    public static bool IsNamed(this ILanguageEngine language, string? name) =>
        !string.IsNullOrWhiteSpace(name) &&
        (string.Equals(language.Id, name.Trim(), StringComparison.OrdinalIgnoreCase) ||
         language.Aliases.Any(a => string.Equals(a, name.Trim(), StringComparison.OrdinalIgnoreCase)));

    public static string DefaultExtension(this ILanguageEngine language) =>
        language.FileExtensions.Count > 0 ? language.FileExtensions[0] : string.Empty;
}

/// <summary>
/// The engine half of a language, with the defaults of a language that only has a name and file extensions: what a
/// language runs with, without the editor. The desktop adds the editor (highlighting, folding, debugger) around it.
/// </summary>
public abstract class LanguageEngine : ILanguageEngine
{
    public abstract string Id { get; }
    public abstract string DisplayName { get; }
    public virtual string ShortName => DisplayName;
    public virtual IReadOnlyList<string> Aliases => Array.Empty<string>();
    public abstract IReadOnlyList<string> FileExtensions { get; }
    public virtual LanguageStorageKind Storage => LanguageStorageKind.SourceFile;
    public virtual bool IsCompiled => false;
    public virtual LanguageCapabilities Capabilities => LanguageCapabilities.None;
    public virtual string IconKind => "FileCodeOutline";
    public virtual string AccentHex => "#8B949E";
    public virtual string LineCommentPrefix => "#";
    public virtual string NewFileTemplate => string.Empty;
    public virtual string RuntimeDescription => DisplayName;
    public virtual IToolchainProvider? Toolchain => null;
    public virtual IScriptRunner? ScriptRunner => null;
    public virtual IDiagnosticParser? RunDiagnostics => null;
    public virtual IPackageManager? Packages => null;
    public virtual INotebookKernelFactory? NotebookKernels => null;
    public virtual JupyterLanguageInfo? Jupyter => null;

    public override string ToString() => DisplayName;
}
