using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PdfEditorApp.Plugins.CSharpEditor.Models;

public partial class TestCaseItem : ObservableObject
{
    [ObservableProperty]
    private string _name = "Case 1";

    [ObservableProperty]
    private string _input = string.Empty;

    [ObservableProperty]
    private string _expectedOutput = string.Empty;

    [ObservableProperty]
    private string _actualOutput = string.Empty;

    [ObservableProperty]
    private bool? _passed;

    [ObservableProperty]
    private bool _isRunning;
}
