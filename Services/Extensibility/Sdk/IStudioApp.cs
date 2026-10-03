namespace FrySharp.Sdk;

/// <summary>
/// Root ambient facade for the C# Code Studio and FrySharp extensibility system.
/// Available as global 'App' or 'Studio' in customization scripts and extensions.
/// </summary>
public interface IStudioApp
{
    /// <summary>Dynamic theming, color tokens, typography and visual styling.</summary>
    IThemeApi Theme { get; }

    /// <summary>Alias for Theme.</summary>
    IThemeApi Themes => Theme;

    /// <summary>Command palette, custom shortcuts, and execution pipeline interception.</summary>
    ICommandApi Commands { get; }

    /// <summary>Active editor document, selection, formatting, and file opening.</summary>
    IEditorApi Editor { get; }

    /// <summary>UI contribution points: Activity Bar, Status Bar, Bottom Deck tabs, and notifications.</summary>
    IUiApi UI { get; }

    /// <summary>Lifecycle hooks and event listeners (before/after run, document saved, theme changed).</summary>
    IHookApi Hooks { get; }

    /// <summary>Thread-safe state bag that persists values across hot-reloads and script executions.</summary>
    IStateBag State { get; }

    /// <summary>Active workspace, project file management, and directory globbing.</summary>
    IWorkspaceApi Workspace { get; }

    /// <summary>Studio Terminal output and CLI child process execution.</summary>
    ITerminalApi Terminal { get; }

    /// <summary>Rich visuals, interactive controls, data tables, and .Dump display.</summary>
    IResultsApi Results { get; }

    /// <summary>Cross-script, cross-extension decoupled publish-subscribe event bus.</summary>
    IEventBusApi Events { get; }

    /// <summary>Language registration, queries, and file-type associations.</summary>
    ILanguagesApi Languages { get; }

    /// <summary>Convenience alias for UI.Dialogs (modals, prompts, and file pickers).</summary>
    IDialogApi Dialogs => UI.Dialogs;
}
