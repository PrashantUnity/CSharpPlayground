using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

/// <summary>
/// Provides code completion suggestions for a language editor off the UI thread.
/// </summary>
public interface ILanguageCompletionService
{
    /// <summary>
    /// Computes code completion items at <paramref name="caretOffset"/> within <paramref name="code"/>.
    /// </summary>
    Task<IReadOnlyList<CSharpCompletionItem>> GetCompletionsAsync(
        string code,
        int caretOffset,
        EditorAssistantContext context,
        CancellationToken ct = default);
}
