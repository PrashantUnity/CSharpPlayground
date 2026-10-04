using System;

namespace FrySharp.Sdk;

/// <summary>
/// Public API for registering event hooks and lifecycle notifications.
/// </summary>
public interface IHookApi
{
    /// <summary>Fires before a script or notebook cell begins execution. Allows canceling or modifying code.</summary>
    IDisposable OnBeforeScriptRun(Action<ExecutionHookContext> hook);

    /// <summary>Fires after a script or notebook cell finishes execution.</summary>
    IDisposable OnAfterScriptRun(Action<ExecutionFinishedHookContext> hook);

    /// <summary>Fires when a document is opened in the editor.</summary>
    IDisposable OnDocumentOpened(Action<IDocumentContext> hook);

    /// <summary>Fires when a document is saved to storage.</summary>
    IDisposable OnDocumentSaved(Action<IDocumentContext> hook);

    /// <summary>Fires when text content inside the active editor tab changes.</summary>
    IDisposable OnEditorTextChanged(Action<IDocumentContext> hook);

    /// <summary>Fires when the cursor / caret position in the active editor moves.</summary>
    IDisposable OnCaretMoved(Action<(IDocumentContext Document, int Line, int Column)> hook);

    /// <summary>Fires when a project workspace folder is opened.</summary>
    IDisposable OnWorkspaceOpened(Action<string> hook);

    /// <summary>Fires when the active workspace is closed.</summary>
    IDisposable OnWorkspaceClosed(Action hook);

    /// <summary>Fires when the active theme or a color token changes.</summary>
    IDisposable OnThemeChanged(Action<string> hook);

    /// <summary>Alias for OnBeforeScriptRun.</summary>
    IDisposable BeforeScriptRun(Action<ExecutionHookContext> hook) => OnBeforeScriptRun(hook);

    /// <summary>Alias for OnAfterScriptRun.</summary>
    IDisposable AfterScriptRun(Action<ExecutionFinishedHookContext> hook) => OnAfterScriptRun(hook);

    /// <summary>Alias for OnDocumentOpened.</summary>
    IDisposable DocumentOpened(Action<IDocumentContext> hook) => OnDocumentOpened(hook);

    /// <summary>Alias for OnDocumentSaved.</summary>
    IDisposable DocumentSaved(Action<IDocumentContext> hook) => OnDocumentSaved(hook);

    /// <summary>Alias for OnThemeChanged.</summary>
    IDisposable ThemeChanged(Action<string> hook) => OnThemeChanged(hook);
}
