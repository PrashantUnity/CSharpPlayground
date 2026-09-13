using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
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
        OnPropertyChanged(nameof(MarkdownTitle));
        OnPropertyChanged(nameof(MarkdownBody));
    }

    partial void OnTypeChanged(CellType value)
    {
        Model.Type = value;
        OnPropertyChanged(nameof(IsCodeCell));
        OnPropertyChanged(nameof(IsMarkdownCell));
        OnPropertyChanged(nameof(IsEditingMarkdown));
        OnPropertyChanged(nameof(IsViewingMarkdown));
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
        OnPropertyChanged(nameof(StatusBadgeForeground));
        OnPropertyChanged(nameof(StatusBadgeBackground));
        OnPropertyChanged(nameof(StatusBadgeBorder));
    }

    partial void OnIsExecutingChanged(bool value)
    {
        Model.IsExecuting = value;
        OnPropertyChanged(nameof(ExecutionBadgeText));
        OnPropertyChanged(nameof(StatusBadgeForeground));
        OnPropertyChanged(nameof(StatusBadgeBackground));
        OnPropertyChanged(nameof(StatusBadgeBorder));
    }

    partial void OnExecutionTimeTextChanged(string value)
    {
        Model.ExecutionTimeText = value;
    }

    partial void OnHasErrorChanged(bool value)
    {
        Model.HasError = value;
        OnPropertyChanged(nameof(StatusBadgeForeground));
        OnPropertyChanged(nameof(StatusBadgeBackground));
        OnPropertyChanged(nameof(StatusBadgeBorder));
    }

    partial void OnIsMarkdownPreviewModeChanged(bool value)
    {
        Model.IsMarkdownPreviewMode = value;
        OnPropertyChanged(nameof(IsEditingMarkdown));
        OnPropertyChanged(nameof(IsViewingMarkdown));
    }

    public bool IsEditingMarkdown => IsMarkdownCell && !IsMarkdownPreviewMode;
    public bool IsViewingMarkdown => IsMarkdownCell && IsMarkdownPreviewMode;

    public string StatusBadgeForeground => HasError ? "#F87171" : IsExecuting ? "#38BDF8" : ExecutionCount.HasValue ? "#34D399" : "#64748B";
    public string StatusBadgeBackground => HasError ? "#350E0E" : IsExecuting ? "#082F49" : ExecutionCount.HasValue ? "#062E22" : "#161E2E";
    public string StatusBadgeBorder => HasError ? "#991B1B" : IsExecuting ? "#0284C7" : ExecutionCount.HasValue ? "#059669" : "#243048";

    public string MarkdownTitle
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Source)) return "Documentation Note";
            var lines = Source.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("#"))
                {
                    return trimmed.TrimStart('#').Trim();
                }
            }
            return lines.Length > 0 ? lines[0].Trim() : "Documentation Note";
        }
    }

    public string MarkdownBody
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Source)) return string.Empty;
            var lines = Source.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
            var bodyLines = lines.Where(l => !l.Trim().StartsWith("#")).ToArray();
            return string.Join(Environment.NewLine, bodyLines).Trim();
        }
    }

    [RelayCommand]
    public async Task CopyOutputAsync()
    {
        if (string.IsNullOrEmpty(OutputText)) return;
        var topLevel = Avalonia.Application.Current?.ApplicationLifetime switch
        {
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop => desktop.MainWindow,
            _ => null
        };
        if (topLevel?.Clipboard != null)
        {
            await topLevel.Clipboard.SetTextAsync(OutputText);
        }
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
