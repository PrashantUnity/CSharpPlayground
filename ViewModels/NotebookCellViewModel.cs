using System;
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
    private Avalonia.Media.Imaging.Bitmap? _imageOutputBitmap;

    [ObservableProperty]
    private bool _hasImageOutput;

    [ObservableProperty]
    private string _imageDimensionsText = string.Empty;

    [ObservableProperty]
    private Avalonia.Controls.Control? _interactiveControl;

    [ObservableProperty]
    private bool _hasInteractiveControl;

    /// <summary>True when this cell previously displayed a live Control (e.g. via Display.Animate)
    /// that couldn't be persisted (a live control reference isn't serializable) and hasn't been
    /// restored by re-running the cell yet. Seeded from Model.HadInteractiveControl on load; cleared
    /// the moment SetInteractiveControl runs again.</summary>
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

        // A live Control (e.g. from Display.Animate) can't be serialized, so it's never in `model` on
        // load — show a placeholder explaining that instead of silently rendering nothing.
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
    }

    partial void OnTypeChanged(CellType value)
    {
        Model.Type = value;
        OnPropertyChanged(nameof(IsCodeCell));
        OnPropertyChanged(nameof(IsMarkdownCell));
        OnPropertyChanged(nameof(LanguageTag));
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

    // Matches this app's actual dark M3 tokens (Error/Primary/Secondary/OnSurfaceVariant families)
    // instead of the Tailwind light-mode swatches these used to be hardcoded to.
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

            // Dispose the outgoing bitmap only *after* the new one is assigned (never before — disposing
            // first risks a render race against the compositor still reading the old handle). A cell
            // that calls Display.Image(...) repeatedly (e.g. a frame-loop animation) would otherwise
            // leak one native bitmap handle per call.
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

    /// <summary>Disposes only this cell's live, non-serializable output (currently just
    /// InteractiveControl) — safe to call from a "this cell/tab is going away" path without touching
    /// text/table/etc. data that might still need to be saved.</summary>
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
            // If ExecutionTimeText is like "700 ms", convert to "0.7s"
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
            // Avalonia Clipboard supports SetBitmapAsync or base64 fallback
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
        OnPropertyChanged(nameof(ExecutionDurationShortText));
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
