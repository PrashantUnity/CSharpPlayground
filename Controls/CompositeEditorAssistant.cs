using System;
using System.Collections.Generic;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls;

/// <summary>
/// Combines multiple editor assistants (e.g. Quick Info hover + Code Completion) into a single disposable unit.
/// </summary>
public sealed class CompositeEditorAssistant(IReadOnlyList<IDisposable> assistants) : IDisposable
{
    public CompositeEditorAssistant(params IDisposable[] assistants) : this((IReadOnlyList<IDisposable>)assistants)
    {
    }

    public void Dispose()
    {
        foreach (var assistant in assistants)
        {
            try
            {
                assistant.Dispose();
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }
}
