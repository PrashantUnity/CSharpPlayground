using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FrySharp.Sdk;

/// <summary>
/// Public API for showing native modals, user input prompts, file pickers, and custom dialogs.
/// </summary>
public interface IDialogApi
{
    /// <summary>Prompts the user for a single line of text with an optional default value.</summary>
    Task<string?> PromptAsync(string message, string title = "Prompt", string defaultValue = "", string? placeholder = null);

    /// <summary>Shows a confirmation modal dialog with Yes / No actions.</summary>
    Task<bool> ConfirmAsync(string message, string title = "Confirm");

    /// <summary>Shows an informational alert modal dialog with an OK button.</summary>
    Task AlertAsync(string message, string title = "Alert");

    /// <summary>Prompts the user to pick a file from their local file system.</summary>
    Task<string?> PickFileAsync(string title = "Select File", IReadOnlyList<string>? extensions = null);

    /// <summary>Prompts the user to pick a folder from their local file system.</summary>
    Task<string?> PickFolderAsync(string title = "Select Folder");

    /// <summary>Displays an arbitrary Avalonia control hosted inside a modern floating dialog window.</summary>
    Task ShowCustomDialogAsync(string title, object content, double width = 500, double height = 400);
}
