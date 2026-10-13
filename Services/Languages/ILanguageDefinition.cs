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
public interface ILanguageDefinition : ILanguageEngine
{
    /// <summary>Syntax colors for the editor, or null for plain text.</summary>
    IHighlightingDefinition? GetHighlighting(bool isDark);

    IIndentationStrategy CreateIndentationStrategy(TextEditorOptions options);

    /// <summary>Collapsible regions, or null when the language has none.</summary>
    ILanguageFolding? Folding { get; }

    /// <summary>Completion, hover or live diagnostics attached to an editor (e.g. through a language server), or null.</summary>
    IEditorAssistantFactory? EditorAssistants { get; }

    /// <summary>Creates and configures debug sessions for this language, or null when debugging is not supported.</summary>
    PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.IDebuggerProvider? Debugger { get; }
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
    public virtual bool IsCompiled => false;
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
    public virtual PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.IDebuggerProvider? Debugger => null;
    public virtual JupyterLanguageInfo? Jupyter => null;

    public override string ToString() => DisplayName;
}

public static class LanguageDefinitionExtensions
{
    /// <summary>
    /// True when the editor's built-in C# helper for <paramref name="capability"/> (Roslyn completion or hover) should run on
    /// this language's text: it has the capability and doesn't bring its own <see cref="ILanguageDefinition.EditorAssistants"/>.
    /// A null language is the default, C#.
    /// </summary>
    public static bool UsesRoslynHelper(this ILanguageDefinition? language, LanguageCapabilities capability) =>
        language == null || (language.Has(capability) && language.EditorAssistants == null);
}

/// <summary>
/// A language whose engine (toolchain, runner, packages, notebook kernels) lives in FrySharp.Core, shared with the
/// headless hosts; the definition adds the editor (highlighting, folding, indentation, assistants) and the debugger.
/// </summary>
public abstract class EngineLanguageDefinition<TEngine>(TEngine engine) : LanguageDefinition where TEngine : ILanguageEngine
{
    public TEngine Engine { get; } = engine ?? throw new ArgumentNullException(nameof(engine));

    public sealed override string Id => Engine.Id;
    public sealed override string DisplayName => Engine.DisplayName;
    public sealed override string ShortName => Engine.ShortName;
    public sealed override IReadOnlyList<string> Aliases => Engine.Aliases;
    public sealed override IReadOnlyList<string> FileExtensions => Engine.FileExtensions;
    public sealed override LanguageStorageKind Storage => Engine.Storage;
    public sealed override bool IsCompiled => Engine.IsCompiled;
    public sealed override LanguageCapabilities Capabilities => Engine.Capabilities;
    public sealed override string IconKind => Engine.IconKind;
    public sealed override string AccentHex => Engine.AccentHex;
    public sealed override string LineCommentPrefix => Engine.LineCommentPrefix;
    public sealed override string NewFileTemplate => Engine.NewFileTemplate;
    public sealed override string RuntimeDescription => Engine.RuntimeDescription;
    public sealed override IToolchainProvider? Toolchain => Engine.Toolchain;
    public sealed override IScriptRunner? ScriptRunner => Engine.ScriptRunner;
    public sealed override IDiagnosticParser? RunDiagnostics => Engine.RunDiagnostics;
    public sealed override IPackageManager? Packages => Engine.Packages;
    public sealed override INotebookKernelFactory? NotebookKernels => Engine.NotebookKernels;
    public sealed override JupyterLanguageInfo? Jupyter => Engine.Jupyter;
}
