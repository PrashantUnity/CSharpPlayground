using System;
using AvaloniaEdit;
using PdfEditorApp.Plugins.CSharpEditor.Controls;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;

/// <summary>
/// Attaches code completion and language assistance to Java editors and notebook cells.
/// </summary>
public sealed class JavaEditorAssistantFactory : IEditorAssistantFactory
{
    public static readonly JavaEditorAssistantFactory Instance = new();

    private readonly JavaCompletionService _completionService = new();

    public IDisposable Attach(TextEditor editor, EditorAssistantContext context)
    {
        return new LanguageCompletionController(
            editor,
            context,
            _completionService,
            isImmediateTrigger: (ch, _, _) => ch == '.');
    }
}
