namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

/// <summary>
/// What a language supports in the studio. Views and view models switch features on these flags, never on a language's
/// id, so a newly registered language gets the right toolbar, panels and shortcuts without changes elsewhere.
/// </summary>
[Flags]
public enum LanguageCapabilities
{
    None = 0,
    Completion = 1 << 0,
    QuickInfo = 1 << 1,
    Formatting = 1 << 2,
    Debugging = 1 << 3,
    Breakpoints = 1 << 4,

    /// <summary>Problems are refreshed while editing (C#: Roslyn). Without it, Problems come from runs.</summary>
    LiveDiagnostics = 1 << 5,

    /// <summary>The Test Cases panel, which writes <c>Check(...)</c> lines into the code.</summary>
    TestCases = 1 << 6,

    /// <summary>Code templates can be inserted from the template gallery.</summary>
    Templates = 1 << 7,

    /// <summary>C#'s Statements / Program / Expression modes.</summary>
    ExecutionModes = 1 << 8,

    /// <summary>A running script can type into its standard input from the Terminal.</summary>
    StandardInput = 1 << 9,

    /// <summary>Notebook code cells can be written in this language.</summary>
    NotebookCells = 1 << 10,

    /// <summary>Values can be copied in and out of its notebook kernel with <c>#!share</c>.</summary>
    ValueSharing = 1 << 11,

    /// <summary>Packages can be installed (e.g. <c>%pip install</c>).</summary>
    Packages = 1 << 12,
}

/// <summary>How documents of a language are kept on disk.</summary>
public enum LanguageStorageKind
{
    /// <summary>Inside a <c>.frycs</c> JSON document with its notes, test cases and breakpoints (C#).</summary>
    FryDocument,

    /// <summary>A plain source file (<c>script.py</c>), read and written as text so other tools can use it too.</summary>
    SourceFile
}

/// <summary>Ids of the built-in languages. A language's id is a plain string, so adding one needs no enum.</summary>
public static class LanguageIds
{
    public const string CSharp = "csharp";
    public const string Python = "python";
}
