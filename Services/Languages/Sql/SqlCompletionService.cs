using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;

/// <summary>
/// The editor's completion for SQL scripts and notebook cells: <see cref="SqlCompletions"/>, off the UI thread's way.
/// </summary>
public sealed class SqlCompletionService : ILanguageCompletionService
{
    public Task<IReadOnlyList<CSharpCompletionItem>> GetCompletionsAsync(
        string code,
        int caretOffset,
        EditorAssistantContext context,
        CancellationToken ct = default) =>
        Task.FromResult(SqlCompletions.Get(code, caretOffset));
}
