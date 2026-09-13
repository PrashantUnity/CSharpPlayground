using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

public partial class NotebookCellViewModel : ObservableObject
{
    public NotebookCellItem Model { get; }

    public string Id => Model.Id;

    [ObservableProperty]
    private CellType _type;

    [ObservableProperty]
    private string _source;

    [ObservableProperty]
    private string _outputText = string.Empty;

    [ObservableProperty]
    private int? _executionCount;

    [ObservableProperty]
    private bool _isExecuting;

    [ObservableProperty]
    private string _executionTimeText = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private bool _hasOutput;

    [ObservableProperty]
    private bool _isMarkdownPreviewMode;

    public bool IsCodeCell => Type == CellType.Code;
    public bool IsMarkdownCell => Type == CellType.Markdown;

    public string ExecutionBadgeText => IsExecuting ? "[*]" : ExecutionCount.HasValue ? $"[{ExecutionCount}]" : "[ ]";

    private readonly Func<NotebookCellViewModel, Task>? _runAction;
    private readonly Action<NotebookCellViewModel>? _deleteAction;
    private readonly Action<NotebookCellViewModel, int>? _moveAction;
    private readonly Action<NotebookCellViewModel, CellType>? _addBelowAction;

    public NotebookCellViewModel(
        NotebookCellItem model,
        Func<NotebookCellViewModel, Task>? runAction = null,
        Action<NotebookCellViewModel>? deleteAction = null,
        Action<NotebookCellViewModel, int>? moveAction = null,
        Action<NotebookCellViewModel, CellType>? addBelowAction = null)
    {
        Model = model;
        _type = model.Type;
        _source = model.Source;
        _outputText = model.OutputText;
        _executionCount = model.ExecutionCount;
        _executionTimeText = model.ExecutionTimeText;
        _hasError = model.HasError;
        _hasOutput = model.HasOutput;
        _isMarkdownPreviewMode = model.IsMarkdownPreviewMode;

        _runAction = runAction;
        _deleteAction = deleteAction;
        _moveAction = moveAction;
        _addBelowAction = addBelowAction;
    }

    partial void OnSourceChanged(string value)
    {
        Model.Source = value;
    }

    partial void OnTypeChanged(CellType value)
    {
        Model.Type = value;
        OnPropertyChanged(nameof(IsCodeCell));
        OnPropertyChanged(nameof(IsMarkdownCell));
    }

    partial void OnOutputTextChanged(string value)
    {
        Model.OutputText = value;
        HasOutput = !string.IsNullOrEmpty(value);
    }

    partial void OnExecutionCountChanged(int? value)
    {
        Model.ExecutionCount = value;
        OnPropertyChanged(nameof(ExecutionBadgeText));
    }

    partial void OnIsExecutingChanged(bool value)
    {
        Model.IsExecuting = value;
        OnPropertyChanged(nameof(ExecutionBadgeText));
    }

    partial void OnExecutionTimeTextChanged(string value)
    {
        Model.ExecutionTimeText = value;
    }

    partial void OnHasErrorChanged(bool value)
    {
        Model.HasError = value;
    }

    partial void OnIsMarkdownPreviewModeChanged(bool value)
    {
        Model.IsMarkdownPreviewMode = value;
    }

    [RelayCommand]
    private async Task RunCellAsync()
    {
        if (_runAction != null)
        {
            await _runAction.Invoke(this);
        }
    }

    [RelayCommand]
    public void ClearOutput()
    {
        OutputText = string.Empty;
        ExecutionTimeText = string.Empty;
        HasError = false;
    }

    [RelayCommand]
    private void Delete()
    {
        _deleteAction?.Invoke(this);
    }

    [RelayCommand]
    private void MoveUp()
    {
        _moveAction?.Invoke(this, -1);
    }

    [RelayCommand]
    private void MoveDown()
    {
        _moveAction?.Invoke(this, 1);
    }

    [RelayCommand]
    private void AddCodeBelow()
    {
        _addBelowAction?.Invoke(this, CellType.Code);
    }

    [RelayCommand]
    private void AddMarkdownBelow()
    {
        _addBelowAction?.Invoke(this, CellType.Markdown);
    }

    [RelayCommand]
    private void ToggleMarkdownPreview()
    {
        IsMarkdownPreviewMode = !IsMarkdownPreviewMode;
    }

    [RelayCommand]
    private void ToggleType()
    {
        Type = Type == CellType.Code ? CellType.Markdown : CellType.Code;
    }
}
