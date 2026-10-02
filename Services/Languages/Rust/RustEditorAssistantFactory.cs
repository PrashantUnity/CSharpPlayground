using PdfEditorApp.Plugins.CSharpEditor.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Controls.Editor;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;

/// <summary>
/// Attaches code completion (after <c>.</c> and <c>::</c>, or as you type) and hover Quick Info to Rust editors and
/// notebook cells. A language server such as rust-analyzer can replace this factory later without touching the rest.
/// </summary>
public sealed class RustEditorAssistantFactory : IEditorAssistantFactory
{
    public static readonly RustEditorAssistantFactory Instance = new();

    private readonly RustCompletionService _completionService = new();

    public IDisposable Attach(AvaloniaEdit.TextEditor editor, EditorAssistantContext context)
    {
        var quickInfo = new LanguageQuickInfoController(editor, context, Resolve);
        var completion = new LanguageCompletionController(
            editor,
            context,
            _completionService,
            isImmediateTrigger: (ch, text, offset) => ch == '.' || (ch == ':' && offset >= 2 && text[offset - 2] == ':'));

        return new CompositeEditorAssistant(quickInfo, completion);
    }

    /// <summary>What hovering at <paramref name="offset"/> in <paramref name="text"/> should show, or null.</summary>
    public static LanguageQuickInfoHit? Resolve(string text, int offset)
    {
        if (RustQuickInfoProvider.ExtractSymbol(text, offset) is not { } symbol) return null;
        if (RustQuickInfoProvider.Lookup(symbol.Text) is not { } info) return null;

        return new LanguageQuickInfoHit(symbol.Start, symbol.Length, info.Signature, info.Summary, info.Owner, KeywordFor(info.Kind));
    }

    private static string KeywordFor(RustSymbolKind kind) => kind == RustSymbolKind.Module ? "module" : "in";
}
