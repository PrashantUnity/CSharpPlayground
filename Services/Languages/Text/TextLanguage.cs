using AvaloniaEdit.Highlighting;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Text;

/// <summary>
/// Language definition for plain text, configuration files, and data files
/// (.txt, .json, .csv, .md, .xml, .yaml, .toml, .ini, .log, etc.) in the workspace.
/// </summary>
public sealed class TextLanguage : LanguageDefinition
{
    public override string Id => LanguageIds.Text;
    public override string DisplayName => "Plain Text";
    public override string ShortName => "TXT";
    public override IReadOnlyList<string> Aliases => ["txt", "text", "plaintext"];
    public override IReadOnlyList<string> FileExtensions =>
    [
        ".txt", ".json", ".csv", ".tsv", ".md", ".markdown",
        ".xml", ".yaml", ".yml", ".toml", ".ini", ".conf",
        ".cfg", ".env", ".log", ".sh", ".bat", ".cmd", ".ps1",
        ".html", ".htm", ".css", ".scss", ".less"
    ];

    public override LanguageStorageKind Storage => LanguageStorageKind.SourceFile;
    public override LanguageCapabilities Capabilities => LanguageCapabilities.None;
    public override string IconKind => "FileDocumentOutline";
    public override string AccentHex => "#8B949E";
    public override string LineCommentPrefix => "#";
    public override string RuntimeDescription => "Plain Text";

    public override IHighlightingDefinition? GetHighlighting(bool isDark) => null;
}
