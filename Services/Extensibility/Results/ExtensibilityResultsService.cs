using System;
using PdfEditorApp.Plugins.CSharpEditor.Services.Common;
using FrySharp.Sdk;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Results;

/// <summary>
/// Bridge implementation of IResultsApi allowing scripts and extensions to render rich visuals into Zone 4.
/// </summary>
public class ExtensibilityResultsService : IResultsApi
{
    public Action<object?, string?>? ShowHandler { get; set; }
    public Action<object, string?>? ShowControlHandler { get; set; }
    public Action<object, string?>? ShowTableHandler { get; set; }
    public Action? ClearHandler { get; set; }
    public Action? FocusHandler { get; set; }

    public void Show(object? value, string? title = null)
    {
        RunOnUI(() => ShowHandler?.Invoke(value, title));
    }

    public void ShowControl(object control, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(control);
        RunOnUI(() => ShowControlHandler?.Invoke(control, title));
    }

    public void ShowTable(object data, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        RunOnUI(() => ShowTableHandler?.Invoke(data, title));
    }

    public void Clear()
    {
        RunOnUI(() => ClearHandler?.Invoke());
    }

    public void Focus()
    {
        RunOnUI(() => FocusHandler?.Invoke());
    }

    private static void RunOnUI(Action action) => UiDispatchHelper.RunOnUi(action);
}
