using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;

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

    [ObservableProperty]
    private bool _isInputCollapsed;

    [ObservableProperty]
    private bool _isOutputCollapsed;

    [ObservableProperty]
    private bool _isOutputScrolled;

    [ObservableProperty]
    private Avalonia.Media.Imaging.Bitmap? _imageOutputBitmap;

    [ObservableProperty]
    private bool _hasImageOutput;

    [ObservableProperty]
    private string _imageDimensionsText = string.Empty;

    [ObservableProperty]
    private Avalonia.Controls.Control? _interactiveControl;

    [ObservableProperty]
    private bool _hasInteractiveControl;

    [ObservableProperty]
    private bool _interactiveControlPlaceholderVisible;

    [ObservableProperty]
    private string _htmlContent = string.Empty;

    [ObservableProperty]
    private bool _hasHtmlContent;

    [ObservableProperty]
    private DumpTableResult? _tableResult;

    [ObservableProperty]
    private bool _hasTableOutput;

    [ObservableProperty]
    private ObjectInspectorNode? _inspectorNode;

    [ObservableProperty]
    private bool _hasInspectorOutput;

    [ObservableProperty]
    private bool _isSelected;

    public bool IsCodeCell => Type == CellType.Code;
    public bool IsMarkdownCell => Type == CellType.Markdown;
    public string LanguageTag => IsCodeCell ? "C#" : "MD";

    public string ExecutionBadgeText => IsExecuting ? "[*]" : ExecutionCount.HasValue ? $"[{ExecutionCount}]" : "[ ]";

    private readonly Func<NotebookCellViewModel, Task>? _runAction;
    private readonly Action<NotebookCellViewModel>? _deleteAction;
    private readonly Action<NotebookCellViewModel, int>? _moveAction;
    private readonly Action<NotebookCellViewModel, CellType>? _addBelowAction;
    private readonly Func<NotebookCellViewModel, Task>? _runAndSelectNextAction;

    public NotebookCellViewModel(
        NotebookCellItem model,
        Func<NotebookCellViewModel, Task>? runAction = null,
        Action<NotebookCellViewModel>? deleteAction = null,
        Action<NotebookCellViewModel, int>? moveAction = null,
        Action<NotebookCellViewModel, CellType>? addBelowAction = null,
        Func<NotebookCellViewModel, Task>? runAndSelectNextAction = null)
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
        _isInputCollapsed = model.IsInputCollapsed;
        _isOutputCollapsed = model.IsOutputCollapsed;
        _isOutputScrolled = model.IsOutputScrolled;

        _runAction = runAction;
        _deleteAction = deleteAction;
        _moveAction = moveAction;
        _addBelowAction = addBelowAction;
        _runAndSelectNextAction = runAndSelectNextAction;

        if (model.ImageBytes != null && model.ImageBytes.Length > 0)
        {
            try
            {
                using var ms = new System.IO.MemoryStream(model.ImageBytes);
                _imageOutputBitmap = new Avalonia.Media.Imaging.Bitmap(ms);
                _hasImageOutput = true;
                _imageDimensionsText = (model.ImageWidth.HasValue && model.ImageHeight.HasValue)
                    ? $"{model.ImageWidth.Value} × {model.ImageHeight.Value} px • {model.ImageFormat ?? "PNG"}"
                    : $"{model.ImageFormat ?? "PNG"} Image";
            }
            catch { }
        }

        if (!string.IsNullOrEmpty(model.HtmlContent))
        {
            _htmlContent = model.HtmlContent;
            _hasHtmlContent = true;
        }

        if (model.TableSnapshot != null)
        {
            _tableResult = model.TableSnapshot.ToLive();
            _hasTableOutput = true;
        }

        if (model.InspectorSnapshot != null)
        {
            _inspectorNode = model.InspectorSnapshot.ToLive();
            _hasInspectorOutput = true;
        }

        if (model.HadInteractiveControl)
        {
            _interactiveControlPlaceholderVisible = true;
            _hasOutput = true;
        }
    }

    partial void OnSourceChanged(string value)
    {
        Model.Source = value;
        OnPropertyChanged(nameof(MarkdownTitle));
        OnPropertyChanged(nameof(MarkdownBody));
        OnPropertyChanged(nameof(InputCollapsedSummaryText));
    }

    partial void OnTypeChanged(CellType value)
    {
        Model.Type = value;
        OnPropertyChanged(nameof(IsCodeCell));
        OnPropertyChanged(nameof(IsMarkdownCell));
        OnPropertyChanged(nameof(LanguageTag));
        OnPropertyChanged(nameof(IsEditingMarkdown));
        OnPropertyChanged(nameof(IsViewingMarkdown));
        OnPropertyChanged(nameof(InputCollapsedSummaryText));
    }

    partial void OnOutputTextChanged(string value)
    {
        Model.OutputText = value;
        HasOutput = !string.IsNullOrEmpty(value) || HasImageOutput || HasHtmlContent || HasTableOutput || HasInspectorOutput || HasInteractiveControl || InteractiveControlPlaceholderVisible;
    }

    partial void OnIsInputCollapsedChanged(bool value)
    {
        Model.IsInputCollapsed = value;
        OnPropertyChanged(nameof(IsInputVisible));
        OnPropertyChanged(nameof(InputCollapsedSummaryText));
        OnPropertyChanged(nameof(InputCollapseIcon));
        OnPropertyChanged(nameof(ToggleInputCollapseTooltip));
        OnPropertyChanged(nameof(ToggleInputCollapseText));
        OnPropertyChanged(nameof(IsEntireCellCollapsed));
    }

    partial void OnIsOutputCollapsedChanged(bool value)
    {
        Model.IsOutputCollapsed = value;
        OnPropertyChanged(nameof(IsOutputVisible));
        OnPropertyChanged(nameof(IsOutputCollapsedBarVisible));
        OnPropertyChanged(nameof(OutputCollapsedSummaryText));
        OnPropertyChanged(nameof(OutputCollapseIcon));
        OnPropertyChanged(nameof(ToggleOutputCollapseTooltip));
        OnPropertyChanged(nameof(ToggleOutputCollapseText));
        OnPropertyChanged(nameof(IsEntireCellCollapsed));
    }

    partial void OnIsOutputScrolledChanged(bool value)
    {
        Model.IsOutputScrolled = value;
        OnPropertyChanged(nameof(OutputScrolledIcon));
        OnPropertyChanged(nameof(OutputScrolledTooltip));
        OnPropertyChanged(nameof(ToggleOutputScrolledText));
    }

    partial void OnHasOutputChanged(bool value)
    {
        OnPropertyChanged(nameof(IsOutputVisible));
        OnPropertyChanged(nameof(IsOutputCollapsedBarVisible));
        OnPropertyChanged(nameof(OutputCollapsedSummaryText));
        OnPropertyChanged(nameof(IsEntireCellCollapsed));
    }

    public bool IsInputVisible => !IsInputCollapsed;
    public bool IsOutputVisible => HasOutput && !IsOutputCollapsed;
    public bool IsOutputCollapsedBarVisible => HasOutput && IsOutputCollapsed;
    public bool IsEntireCellCollapsed => IsInputCollapsed && (IsOutputCollapsed || !HasOutput);

    public string InputCollapseIcon => IsInputCollapsed ? "ChevronRight" : "ChevronDown";
    public string OutputCollapseIcon => IsOutputCollapsed ? "ChevronRight" : "ChevronDown";
    public string OutputScrolledIcon => IsOutputScrolled ? "FormatLineSpacing" : "UnfoldMoreHorizontal";
    public string OutputScrolledTooltip => IsOutputScrolled ? "Disable Scrolled Output (Full Height)" : "Enable Scrolled Output (Fixed Height)";
    public string ToggleInputCollapseTooltip => IsInputCollapsed ? "Expand Cell Input" : "Collapse Cell Input";
    public string ToggleOutputCollapseTooltip => IsOutputCollapsed ? "Expand Cell Output" : "Collapse Cell Output";
    public string ToggleInputCollapseText => IsInputCollapsed ? "Expand Input" : "Collapse Input";
    public string ToggleOutputCollapseText => IsOutputCollapsed ? "Expand Output" : "Collapse Output";
    public string ToggleOutputScrolledText => IsOutputScrolled ? "Disable Scrolled Output" : "Enable Scrolled Output";
    public string ToggleCellCollapseText => IsEntireCellCollapsed ? "Expand Entire Cell" : "Collapse Entire Cell";

    public string InputCollapsedSummaryText
    {
        get
        {
            var raw = Source ?? string.Empty;
            var lines = raw.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
            var nonEmpty = lines.Select(l => l.Trim()).FirstOrDefault(l => !string.IsNullOrEmpty(l)) ?? string.Empty;
            var count = lines.Length;

            if (IsMarkdownCell)
            {
                var title = MarkdownTitle;
                return count <= 1
                    ? $"Markdown: {title}"
                    : $"Markdown ({count} lines): {title}";
            }
            else
            {
                if (string.IsNullOrEmpty(nonEmpty)) nonEmpty = "// Empty code block";
                if (nonEmpty.Length > 60) nonEmpty = nonEmpty.Substring(0, 57) + "...";
                return count <= 1
                    ? $"Code: {nonEmpty}"
                    : $"Code ({count} lines): {nonEmpty}";
            }
        }
    }

    public string OutputCollapsedSummaryText
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(OutputText))
            {
                var lineCount = OutputText.Split('\n').Length;
                parts.Add(lineCount == 1 ? "1 line output" : $"{lineCount} lines output");
            }
            if (HasImageOutput) parts.Add("Image");
            if (HasTableOutput) parts.Add("Table");
            if (HasInspectorOutput) parts.Add("Object Inspector");
            if (HasInteractiveControl || InteractiveControlPlaceholderVisible) parts.Add("UI Widget");
            if (HasHtmlContent) parts.Add("HTML");

            if (parts.Count == 0) return "Output collapsed — Click to expand";
            return $"Output collapsed ({string.Join(", ", parts)}) — Click to expand";
        }
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
        OnPropertyChanged(nameof(ExecutionDurationShortText));
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
        OnPropertyChanged(nameof(MarkdownPreviewButtonText));
    }

    public bool IsEditingMarkdown => IsMarkdownCell && !IsMarkdownPreviewMode;
    public bool IsViewingMarkdown => IsMarkdownCell && IsMarkdownPreviewMode;
    public string MarkdownPreviewButtonText => IsViewingMarkdown ? "Edit" : "Preview";

    public string StatusBadgeForeground => HasError ? "#FFB4AB" : IsExecuting ? "#A8C7FA" : ExecutionCount.HasValue ? "#BDC7DC" : "#9BA1AD";
    public string StatusBadgeBackground => HasError ? "#93000A" : IsExecuting ? "#0F387D" : ExecutionCount.HasValue ? "#343E4E" : "#252C36";
    public string StatusBadgeBorder => HasError ? "#FFB4AB" : IsExecuting ? "#A8C7FA" : ExecutionCount.HasValue ? "#BDC7DC" : "#3D4450";

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
    private async Task RunCellAndSelectNextAsync()
    {
        if (_runAndSelectNextAction != null)
        {
            await _runAndSelectNextAction.Invoke(this);
        }
        else if (_runAction != null)
        {
            await _runAction.Invoke(this);
        }
    }

    public void SetImageOutput(byte[] bytes, string format = "PNG", int? width = null, int? height = null)
    {
        try
        {
            Model.ImageBytes = bytes;
            Model.ImageFormat = format;
            Model.ImageWidth = width;
            Model.ImageHeight = height;

            var previousBitmap = ImageOutputBitmap;

            using var ms = new System.IO.MemoryStream(bytes);
            ImageOutputBitmap = new Avalonia.Media.Imaging.Bitmap(ms);
            HasImageOutput = true;

            previousBitmap?.Dispose();

            ImageDimensionsText = (width.HasValue && height.HasValue)
                ? $"{width.Value} × {height.Value} px • {format}"
                : $"{format} Image";

            HasOutput = true;
        }
        catch (Exception ex)
        {
            OutputText += $"\n⚠️ Image display error: {ex.Message}";
        }
    }

    public void SetInteractiveControl(Avalonia.Controls.Control control)
    {
        if (!ReferenceEquals(InteractiveControl, control))
        {
            InteractiveControlLifecycle.DisposeIfNeeded(InteractiveControl);
        }

        InteractiveControl = control;
        HasInteractiveControl = true;
        HasOutput = true;
        Model.HadInteractiveControl = true;
        InteractiveControlPlaceholderVisible = false;
    }

    public void DisposeLiveResources()
    {
        InteractiveControlLifecycle.DisposeIfNeeded(InteractiveControl);
        InteractiveControl = null;
        HasInteractiveControl = false;
    }

    public void SetHtmlContent(string html)
    {
        HtmlContent = html;
        Model.HtmlContent = html;
        HasHtmlContent = !string.IsNullOrEmpty(html);
        HasOutput = true;
    }

    public void SetTableOutput(DumpTableResult table)
    {
        TableResult = table;
        Model.TableSnapshot = table.ToSnapshot();
        HasTableOutput = true;
        HasOutput = true;
    }

    public void SetInspectorOutput(ObjectInspectorNode inspector)
    {
        InspectorNode = inspector;
        Model.InspectorSnapshot = inspector.ToSnapshot();
        HasInspectorOutput = true;
        HasOutput = true;
    }

    public string ExecutionDurationShortText
    {
        get
        {
            if (string.IsNullOrEmpty(ExecutionTimeText)) return string.Empty;
            if (ExecutionTimeText.EndsWith(" ms", StringComparison.OrdinalIgnoreCase))
            {
                var numStr = ExecutionTimeText.Substring(0, ExecutionTimeText.Length - 3).Trim();
                if (double.TryParse(numStr, out var ms))
                {
                    return $"{ms / 1000.0:F1}s";
                }
            }
            return ExecutionTimeText;
        }
    }

    [RelayCommand]
    public async Task CopyImageAsync()
    {
        if (Model.ImageBytes == null || Model.ImageBytes.Length == 0) return;

        var topLevel = Avalonia.Application.Current?.ApplicationLifetime switch
        {
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop => desktop.MainWindow,
            _ => null
        };

        if (topLevel?.Clipboard != null && ImageOutputBitmap != null)
        {
            try
            {
                var base64 = Convert.ToBase64String(Model.ImageBytes);
                await topLevel.Clipboard.SetTextAsync($"data:image/png;base64,{base64}");
            }
            catch { }
        }
    }

    [RelayCommand]
    public async Task SaveImageAsync()
    {
        if (Model.ImageBytes == null || Model.ImageBytes.Length == 0) return;

        try
        {
            var picturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (string.IsNullOrEmpty(picturesPath) || !System.IO.Directory.Exists(picturesPath))
            {
                picturesPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            var fileName = $"notebook_render_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            var fullPath = System.IO.Path.Combine(picturesPath, fileName);
            await System.IO.File.WriteAllBytesAsync(fullPath, Model.ImageBytes);

            OutputText += $"\n💾 Image successfully saved to: {fullPath}";
        }
        catch (Exception ex)
        {
            OutputText += $"\n❌ Failed to save image: {ex.Message}";
        }
    }

    [RelayCommand]
    public void ClearOutput()
    {
        OutputText = string.Empty;
        ExecutionTimeText = string.Empty;
        HasError = false;

        ImageOutputBitmap?.Dispose();
        ImageOutputBitmap = null;
        HasImageOutput = false;
        ImageDimensionsText = string.Empty;
        Model.ImageBytes = null;

        InteractiveControlLifecycle.DisposeIfNeeded(InteractiveControl);
        InteractiveControl = null;
        HasInteractiveControl = false;
        InteractiveControlPlaceholderVisible = false;
        Model.HadInteractiveControl = false;

        HtmlContent = string.Empty;
        HasHtmlContent = false;
        Model.HtmlContent = null;

        TableResult = null;
        HasTableOutput = false;
        Model.TableSnapshot = null;

        InspectorNode = null;
        HasInspectorOutput = false;
        Model.InspectorSnapshot = null;

        HasOutput = false;
        IsOutputCollapsed = false;
        OnPropertyChanged(nameof(ExecutionDurationShortText));
    }

    [RelayCommand]
    public void ToggleInputCollapse()
    {
        IsInputCollapsed = !IsInputCollapsed;
    }

    [RelayCommand]
    public void ToggleOutputCollapse()
    {
        if (!HasOutput) return;
        IsOutputCollapsed = !IsOutputCollapsed;
    }

    [RelayCommand]
    public void ToggleOutputScrolled()
    {
        IsOutputScrolled = !IsOutputScrolled;
    }

    [RelayCommand]
    public void ToggleCellCollapse()
    {
        if (IsEntireCellCollapsed)
        {
            IsInputCollapsed = false;
            IsOutputCollapsed = false;
        }
        else
        {
            IsInputCollapsed = true;
            if (HasOutput)
            {
                IsOutputCollapsed = true;
            }
        }
    }

    [RelayCommand]
    public void CollapseInput() => IsInputCollapsed = true;

    [RelayCommand]
    public void ExpandInput() => IsInputCollapsed = false;

    [RelayCommand]
    public void CollapseOutput()
    {
        if (HasOutput) IsOutputCollapsed = true;
    }

    [RelayCommand]
    public void ExpandOutput() => IsOutputCollapsed = false;

    [RelayCommand]
    public void CollapseCell()
    {
        IsInputCollapsed = true;
        if (HasOutput) IsOutputCollapsed = true;
    }

    [RelayCommand]
    public void ExpandCell()
    {
        IsInputCollapsed = false;
        IsOutputCollapsed = false;
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

    public event Action? RequestFoldAllCode;
    public event Action? RequestUnfoldAllCode;
    public event Action? RequestFormatCode;

    [RelayCommand]
    public void FoldAllCode()
    {
        RequestFoldAllCode?.Invoke();
    }

    [RelayCommand]
    public void UnfoldAllCode()
    {
        RequestUnfoldAllCode?.Invoke();
    }

    [RelayCommand]
    public void FormatCode()
    {
        if (Type != CellType.Code || string.IsNullOrWhiteSpace(Source)) return;

        try
        {
            var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(Source);
            var root = tree.GetRoot();
            var formatted = Microsoft.CodeAnalysis.SyntaxNodeExtensions.NormalizeWhitespace(root).ToFullString();
            if (formatted != Source)
            {
                Source = formatted;
            }
        }
        catch { }

        RequestFormatCode?.Invoke();
    }
}
