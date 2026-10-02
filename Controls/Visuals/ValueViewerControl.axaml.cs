using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Visuals;

public partial class ValueViewerControl : UserControl
{
    private string _fullValue = string.Empty;

    public event Action? CloseRequested;

    public ValueViewerControl()
    {
        InitializeComponent();

        CloseButton.Click += (s, e) => CloseRequested?.Invoke();
        CopyButton.Click += OnCopyClicked;
    }

    public void SetValue(DebugVariableItem item)
    {
        TitleText.Text = $"Value View: {item.Name}";
        _fullValue = item.RawValue is string s ? s : item.ValueDisplay.Trim('"');
        ValueTextBox.Text = _fullValue;
        LengthInfoText.Text = $"{_fullValue.Length} characters";
    }

    public void SetRawText(string title, string text)
    {
        TitleText.Text = $"Value View: {title}";
        _fullValue = text;
        ValueTextBox.Text = _fullValue;
        LengthInfoText.Text = $"{_fullValue.Length} characters";
    }

    private async void OnCopyClicked(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard != null)
        {
            await topLevel.Clipboard.SetTextAsync(_fullValue);
        }
    }
}
