namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

/// <summary>A language as Jupyter knows it: the kernelspec and language_info of an exported .ipynb.</summary>
public sealed record JupyterLanguageInfo(
    string KernelName,
    string KernelDisplayName,
    string KernelLanguage,
    string LanguageName,
    string FileExtension,
    string MimeType,
    string? Version = null);
