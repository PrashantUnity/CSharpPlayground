using System.Collections.Generic;
using System.Linq;
using Avalonia.Platform.Storage;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

namespace PdfEditorApp.Plugins.CSharpEditor.Views;

/// <summary>File-picker filters for the registered source-file languages ("Python Files (*.py)").</summary>
internal static class LanguageFileTypes
{
    /// <summary>"*.py" and the like, to add to an "all supported files" filter.</summary>
    public static string[] Patterns(LanguageRegistry registry) =>
        registry.SourceFileLanguages.SelectMany(l => l.FileExtensions).Select(e => "*" + e).ToArray();

    /// <summary>One filter per source-file language.</summary>
    public static IEnumerable<FilePickerFileType> PerLanguage(LanguageRegistry registry) =>
        registry.SourceFileLanguages.Select(language =>
        {
            var patterns = language.FileExtensions.Select(e => "*" + e).ToArray();
            return new FilePickerFileType($"{language.DisplayName} Files ({string.Join(", ", patterns)})") { Patterns = patterns };
        });
}
