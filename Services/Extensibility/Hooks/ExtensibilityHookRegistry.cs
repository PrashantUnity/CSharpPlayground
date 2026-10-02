using System;
using System.Collections.Generic;
using System.Linq;
using FrySharp.Sdk;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Hooks;

/// <summary>
/// Central registry for lifecycle event hooks and behavioral triggers.
/// </summary>
public class ExtensibilityHookRegistry : IHookApi
{
    private readonly List<Action<ExecutionHookContext>> _beforeRunHooks = new();
    private readonly List<Action<ExecutionFinishedHookContext>> _afterRunHooks = new();
    private readonly List<Action<IDocumentContext>> _documentOpenedHooks = new();
    private readonly List<Action<IDocumentContext>> _documentSavedHooks = new();
    private readonly List<Action<IDocumentContext>> _editorTextChangedHooks = new();
    private readonly List<Action<(IDocumentContext Document, int Line, int Column)>> _caretMovedHooks = new();
    private readonly List<Action<string>> _workspaceOpenedHooks = new();
    private readonly List<Action> _workspaceClosedHooks = new();
    private readonly List<Action<string>> _themeChangedHooks = new();
    private readonly object _lock = new();

    public IDisposable OnBeforeScriptRun(Action<ExecutionHookContext> hook) => AddHook(_beforeRunHooks, hook);
    public IDisposable OnAfterScriptRun(Action<ExecutionFinishedHookContext> hook) => AddHook(_afterRunHooks, hook);
    public IDisposable OnDocumentOpened(Action<IDocumentContext> hook) => AddHook(_documentOpenedHooks, hook);
    public IDisposable OnDocumentSaved(Action<IDocumentContext> hook) => AddHook(_documentSavedHooks, hook);
    public IDisposable OnEditorTextChanged(Action<IDocumentContext> hook) => AddHook(_editorTextChangedHooks, hook);
    public IDisposable OnCaretMoved(Action<(IDocumentContext Document, int Line, int Column)> hook) => AddHook(_caretMovedHooks, hook);
    public IDisposable OnWorkspaceOpened(Action<string> hook) => AddHook(_workspaceOpenedHooks, hook);
    public IDisposable OnWorkspaceClosed(Action hook) => AddHook(_workspaceClosedHooks, hook);
    public IDisposable OnThemeChanged(Action<string> hook) => AddHook(_themeChangedHooks, hook);

    public void InvokeBeforeScriptRun(ExecutionHookContext ctx) => InvokeAll(_beforeRunHooks, ctx);
    public void InvokeAfterScriptRun(ExecutionFinishedHookContext ctx) => InvokeAll(_afterRunHooks, ctx);
    public void InvokeDocumentOpened(IDocumentContext ctx) => InvokeAll(_documentOpenedHooks, ctx);
    public void InvokeDocumentSaved(IDocumentContext ctx) => InvokeAll(_documentSavedHooks, ctx);
    public void InvokeEditorTextChanged(IDocumentContext ctx) => InvokeAll(_editorTextChangedHooks, ctx);
    public void InvokeCaretMoved(IDocumentContext doc, int line, int col) => InvokeAll(_caretMovedHooks, (doc, line, col));
    public void InvokeWorkspaceOpened(string rootPath) => InvokeAll(_workspaceOpenedHooks, rootPath);
    public void InvokeWorkspaceClosed()
    {
        List<Action> copy;
        lock (_lock) { copy = _workspaceClosedHooks.ToList(); }
        foreach (var hook in copy)
        {
            try { hook(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ExtensibilityHookRegistry] Error: {ex.Message}"); }
        }
    }
    public void InvokeThemeChanged(string themeId) => InvokeAll(_themeChangedHooks, themeId);

    public event Action? HooksChanged;

    public int ActiveHookCount
    {
        get
        {
            lock (_lock)
            {
                return _beforeRunHooks.Count + _afterRunHooks.Count +
                       _documentOpenedHooks.Count + _documentSavedHooks.Count +
                       _editorTextChangedHooks.Count + _caretMovedHooks.Count +
                       _workspaceOpenedHooks.Count + _workspaceClosedHooks.Count +
                       _themeChangedHooks.Count;
            }
        }
    }

    private IDisposable AddHook<T>(List<T> list, T item) where T : class
    {
        ArgumentNullException.ThrowIfNull(item);
        lock (_lock)
        {
            list.Add(item);
        }
        HooksChanged?.Invoke();

        return new ActionDisposable(() =>
        {
            lock (_lock)
            {
                list.Remove(item);
            }
            HooksChanged?.Invoke();
        });
    }

    private void InvokeAll<T>(List<Action<T>> list, T arg)
    {
        List<Action<T>> copy;
        lock (_lock)
        {
            copy = list.ToList();
        }

        foreach (var hook in copy)
        {
            try
            {
                hook(arg);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExtensibilityHookRegistry] Error in hook: {ex.Message}");
            }
        }
    }

    private sealed class ActionDisposable : IDisposable
    {
        private Action? _action;
        public ActionDisposable(Action action) => _action = action;
        public void Dispose()
        {
            var act = System.Threading.Interlocked.Exchange(ref _action, null);
            act?.Invoke();
        }
    }
}
