using CommunityToolkit.Mvvm.Input;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

/// <summary>A "New … File" entry for a language whose documents are plain source files, e.g. "New Python File".</summary>
/// <param name="Command">Creates the file in the folder of the Explorer item given, or of the selected one when null.</param>
public sealed record NewFileOption(string LanguageId, string Label, string IconKind, string AccentHex, IAsyncRelayCommand<ExplorerItemViewModel?> Command);

/// <summary>A studio whose Explorer can create source files; its menus list <see cref="NewFileOptions"/>.</summary>
public interface IExplorerNewFileHost
{
    IReadOnlyList<NewFileOption> NewFileOptions { get; }
}
