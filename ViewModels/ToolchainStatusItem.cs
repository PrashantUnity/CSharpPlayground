using CommunityToolkit.Mvvm.ComponentModel;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

/// <summary>
/// A Hub STUDIO ENVIRONMENT row for a language that runs with an installed toolchain (Python): what the studio found,
/// or what's missing and how to install it.
/// </summary>
public sealed partial class ToolchainStatusItem(ILanguageDefinition language, IToolchainProvider provider) : ObservableObject
{
    public ILanguageDefinition Language { get; } = language;

    public IToolchainProvider Provider { get; } = provider;

    public string Title => Language.DisplayName;

    public string IconKind => Language.IconKind;

    public string AccentHex => Language.AccentHex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMissing))]
    private bool _isFound;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMissing))]
    private bool _isChecking = true;

    /// <summary>E.g. "Python 3.14.6 · Homebrew", "Looking for Python…" or "Not installed: hover for how to install".</summary>
    [ObservableProperty]
    private string _status = $"Looking for {language.DisplayName}…";

    /// <summary>The tooltip: where the toolchain is, or the steps to install one.</summary>
    [ObservableProperty]
    private string _detail = string.Empty;

    public bool IsMissing => !IsChecking && !IsFound;

    public void Show(ToolchainResolution resolution)
    {
        IsChecking = false;
        IsFound = resolution.Toolchain != null;
        if (resolution.Toolchain is { } toolchain)
        {
            Status = $"{toolchain.DisplayName} · {toolchain.Source}";
            Detail = toolchain.ExecutablePath;
        }
        else
        {
            Status = $"{Provider.ToolName} not found: hover for how to install";
            Detail = (resolution.Missing?.ToText() ?? $"{Provider.ToolName} wasn't found.").TrimEnd();
        }
    }

    public void ShowChecking()
    {
        IsChecking = true;
        Status = $"Looking for {Provider.ToolName}…";
    }
}
