using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Indentation.CSharp;
using AvaloniaEdit.Search;
using PdfEditorApp.Plugins.CSharpEditor.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;

namespace PdfEditorApp.Plugins.CSharpEditor.Views;

public partial class CSharpCodeStudioView : UserControl
{
    private TextEditor? _editor;
    private FoldingManager? _foldingManager;
    private SearchPanel? _searchPanel;
    private CSharpEditorCompletionController? _completionController;
    private readonly CSharpFoldingStrategy _foldingStrategy = new();
    private readonly DispatcherTimer _foldingTimer;
    private CSharpCodeStudioViewModel? _currentVm;
    private bool _isUpdatingText;

    private readonly BreakpointMargin _breakpointMargin = new();
    private readonly DebugLineRenderer _debugLineRenderer = new();
    private DebugHoverDataTipControl? _debugHoverTip;
    private DebugHoverDataTipController? _debugHoverController;

    public CSharpCodeStudioView()
    {
        InitializeComponent();

        _foldingTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _foldingTimer.Tick += (s, e) =>
        {
            _foldingTimer.Stop();
            UpdateCodeFolding();
        };

        _editor = this.FindControl<TextEditor>("Editor");
        if (_editor != null)
        {
            ApplyThemeVariant();
            ActualThemeVariantChanged += (s, e) => ApplyThemeVariant();

            _editor.Options.HighlightCurrentLine = true;
            _editor.Options.ConvertTabsToSpaces = true;
            _editor.Options.IndentationSize = 4;

            _editor.TextArea.IndentationStrategy = new CSharpIndentationStrategy(_editor.Options);
            _editor.TextArea.LeftMargins.Insert(0, _breakpointMargin);
            _editor.TextArea.TextView.BackgroundRenderers.Add(_debugLineRenderer);
            _breakpointMargin.BreakpointToggled += line => _currentVm?.ToggleBreakpoint(line);

            _searchPanel = SearchPanel.Install(_editor);
            _debugHoverTip = this.FindControl<DebugHoverDataTipControl>("DebugHoverTip");

            _editor.TextChanged += OnEditorTextChanged;
            _editor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
            _editor.KeyDown += OnEditorKeyDown;

            // Automatically manage FoldingManager lifetime to guarantee 1:1 match with active Document
            _editor.PropertyChanged += (s, e) =>
            {
                if (e.Property == TextEditor.DocumentProperty)
                {
                    OnEditorDocumentChanged(_editor.Document);
                }
            };
            OnEditorDocumentChanged(_editor.Document);
        }

        DataContextChanged += OnDataContextChanged;
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (_currentVm == null) return;

        if (e.Source is TextBox tb && tb.DataContext is ExplorerItemViewModel itemVm && itemVm.IsRenaming)
        {
            if (e.Key == Key.Enter)
            {
                itemVm.CommitRenameCommand.Execute(null);
                e.Handled = true;
                return;
            }
            else if (e.Key == Key.Escape)
            {
                itemVm.CancelRenameCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        var isModifier = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        if (isModifier && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.O)
        {
            _ = OpenProjectOrFileDialogAsync();
            e.Handled = true;
            return;
        }

        if (isModifier && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.S)
        {
            _ = _currentVm.SaveCommand.ExecuteAsync(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F5 && !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            if (_currentVm.IsPaused)
            {
                _currentVm.ContinueDebug();
                e.Handled = true;
                return;
            }
            else if (!_currentVm.IsExecuting && !_currentVm.IsDebugging)
            {
                _ = _currentVm.DebugCodeCommand.ExecuteAsync(null);
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.F5 && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && (_currentVm.IsDebugging || _currentVm.IsExecuting))
        {
            if (_currentVm.IsDebugging)
            {
                _currentVm.StopDebug();
            }
            else
            {
                _currentVm.StopCommand.Execute(null);
            }
            e.Handled = true;
            return;
        }

        if (isModifier && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.B)
        {
            _currentVm.ToggleSideBarCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (isModifier && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.J)
        {
            _currentVm.ToggleBottomDeckCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (isModifier && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.E)
        {
            _currentVm.SelectActivityBarItem(0); // Explorer
            e.Handled = true;
            return;
        }

        if (isModifier && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.F)
        {
            _currentVm.SelectActivityBarItem(1); // Search
            e.Handled = true;
            return;
        }

        if (isModifier && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.D)
        {
            _currentVm.SelectActivityBarItem(2); // Debug
            e.Handled = true;
            return;
        }

        if (isModifier && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.X)
        {
            _currentVm.SelectActivityBarItem(3); // NuGet / Dependencies
            e.Handled = true;
            return;
        }

        if (isModifier && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.M)
        {
            _currentVm.ShowProblemsTabCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // ── VS Code Command Palette (Ctrl+Shift+P / Cmd+Shift+P) ──
        if (isModifier && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.P)
        {
            _currentVm.ShowCommandPalette();
            e.Handled = true;
            return;
        }

        // ── VS Code Quick Open (Ctrl+P / Cmd+P) ──
        if (isModifier && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.P)
        {
            _currentVm.ShowQuickOpen("files");
            e.Handled = true;
            return;
        }

        // ── VS Code Go to Line (Ctrl+G / Cmd+G) ──
        if (isModifier && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.G)
        {
            _currentVm.ShowGoToLine();
            e.Handled = true;
            return;
        }

        // ── VS Code Close Active Tab (Ctrl+W / Cmd+W) ──
        if (isModifier && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.W)
        {
            CloseActiveTab();
            e.Handled = true;
            return;
        }

        // ── VS Code New Script (Ctrl+N / Cmd+N) ──
        if (isModifier && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.N)
        {
            _ = _currentVm.NewScriptCommand.ExecuteAsync(null);
            e.Handled = true;
            return;
        }

        // ── VS Code Toggle Line Comment (Ctrl+/ / Cmd+/) ──
        if (isModifier && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && (e.Key == Key.OemQuestion || e.Key == Key.Oem2 || e.Key == Key.Divide))
        {
            ToggleLineComment();
            e.Handled = true;
            return;
        }

        // ── VS Code Format Document (Shift+Alt+F or Ctrl+K, Ctrl+D) ──
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.KeyModifiers.HasFlag(KeyModifiers.Alt) && e.Key == Key.F)
        {
            _currentVm.FormatCode();
            e.Handled = true;
            return;
        }

        // ── VS Code Toggle Word Wrap (Alt+Z) ──
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.Z)
        {
            _currentVm.ToggleWordWrap();
            e.Handled = true;
            return;
        }

        // ── Global F10/F11 Debugging Stepping ──
        if (e.Key == Key.F10 && _currentVm.IsPaused)
        {
            _currentVm.StepOver();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F11 && _currentVm.IsPaused)
        {
            _currentVm.StepInto();
            e.Handled = true;
            return;
        }
    }

    public void ApplyThemeVariant()
    {
        if (_editor == null) return;

        bool isDark = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark ||
                      (ActualThemeVariant != Avalonia.Styling.ThemeVariant.Light && (Avalonia.Application.Current?.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark));

        if (isDark)
        {
            _editor.SyntaxHighlighting = CSharpSyntaxHighlightingTheme.GetDarkTheme();
            _editor.Background = new SolidColorBrush(Color.Parse("#14171F"));
            _editor.Foreground = new SolidColorBrush(Color.Parse("#D4D4D4"));
            _editor.LineNumbersForeground = new SolidColorBrush(Color.Parse("#6E7681"));
            _editor.TextArea.SelectionBrush = new SolidColorBrush(Color.Parse("#264F78"));
            _editor.TextArea.SelectionForeground = null;
            _editor.TextArea.Caret.CaretBrush = new SolidColorBrush(Color.Parse("#58A6FF"));
        }
        else
        {
            _editor.SyntaxHighlighting = CSharpSyntaxHighlightingTheme.GetLightTheme();
            _editor.Background = new SolidColorBrush(Color.Parse("#FFFFFF"));
            _editor.Foreground = new SolidColorBrush(Color.Parse("#1E293B"));
            _editor.LineNumbersForeground = new SolidColorBrush(Color.Parse("#94A3B8"));
            _editor.TextArea.SelectionBrush = new SolidColorBrush(Color.Parse("#BFDBFE"));
            _editor.TextArea.SelectionForeground = null;
            _editor.TextArea.Caret.CaretBrush = new SolidColorBrush(Color.Parse("#0F172A"));
        }

        PolishLeftMargins(isDark);
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ApplyThemeVariant();

        if (_debugHoverController == null && _editor != null && _debugHoverTip != null)
        {
            _debugHoverController = new DebugHoverDataTipController(
                _editor,
                _debugHoverTip,
                () => _currentVm?.IsPaused == true,
                () => _currentVm?.Locals != null ? (IReadOnlyList<DebugVariableItem>)_currentVm.Locals : Array.Empty<DebugVariableItem>(),
                expr => _currentVm != null ? _currentVm.EvaluateExpressionAsync(expr) : Task.FromResult((false, "", "")),
                expr => _ = _currentVm?.AddWatchExpressionAsync(expr));
        }
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _debugHoverController?.Dispose();
        _debugHoverController = null;
    }

    private void PolishLeftMargins(bool isDark = true)
    {
        if (_editor == null) return;

        for (int i = _editor.TextArea.LeftMargins.Count - 1; i >= 0; i--)
        {
            var margin = _editor.TextArea.LeftMargins[i];
            if (margin.GetType().Name.Contains("DottedLineMargin"))
            {
                _editor.TextArea.LeftMargins.RemoveAt(i);
            }
            else if (margin is FoldingMargin foldingMargin)
            {
                if (isDark)
                {
                    foldingMargin.FoldingMarkerBrush = new SolidColorBrush(Color.Parse("#8B949E"));
                    foldingMargin.FoldingMarkerBackgroundBrush = new SolidColorBrush(Color.Parse("#1E2633"));
                    foldingMargin.SelectedFoldingMarkerBrush = new SolidColorBrush(Color.Parse("#58A6FF"));
                    foldingMargin.SelectedFoldingMarkerBackgroundBrush = new SolidColorBrush(Color.Parse("#264F78"));
                }
                else
                {
                    foldingMargin.FoldingMarkerBrush = new SolidColorBrush(Color.Parse("#64748B"));
                    foldingMargin.FoldingMarkerBackgroundBrush = new SolidColorBrush(Color.Parse("#F1F5F9"));
                    foldingMargin.SelectedFoldingMarkerBrush = new SolidColorBrush(Color.Parse("#2563EB"));
                    foldingMargin.SelectedFoldingMarkerBackgroundBrush = new SolidColorBrush(Color.Parse("#DBEAFE"));
                }
            }
        }
    }

    private void UpdateCodeFolding()
    {
        if (_foldingManager != null && _editor?.Document != null)
        {
            try
            {
                _foldingStrategy.UpdateFoldings(_foldingManager, _editor.Document);
            }
            catch
            {
                // Ignore transient syntax errors while actively editing
            }
        }
    }

    private void OnEditorDocumentChanged(TextDocument? newDoc)
    {
        if (_editor == null) return;

        if (_foldingManager != null)
        {
            try
            {
                FoldingManager.Uninstall(_foldingManager);
            }
            catch
            {
            }
            _foldingManager = null;
        }

        if (newDoc != null)
        {
            try
            {
                _foldingManager = FoldingManager.Install(_editor.TextArea);
                var isDark = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark ||
                             (ActualThemeVariant != Avalonia.Styling.ThemeVariant.Light &&
                              (Avalonia.Application.Current?.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark));
                PolishLeftMargins(isDark);
                UpdateCodeFolding();
            }
            catch
            {
            }
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_currentVm != null)
        {
            _currentVm.RequestNavigateToCaret -= OnNavigateToCaret;
            _currentVm.RequestGoToLine -= ScrollToAndSelectLine;
            _currentVm.RequestFoldAll -= FoldAll;
            _currentVm.RequestUnfoldAll -= UnfoldAll;
            _currentVm.RequestToggleSearch -= ToggleSearch;
            _currentVm.RequestSetPausedLine -= OnSetPausedLine;
            _currentVm.RequestSyncBreakpoints -= OnSyncBreakpoints;
            _currentVm.RequestReloadEditorText -= OnReloadEditorText;
            _currentVm.RequestSwitchTabDocument -= OnSwitchTabDocument;
            _currentVm.PropertyChanged -= OnVmPropertyChanged;
            _completionController?.Dispose();
            _completionController = null;
        }

        _currentVm = DataContext as CSharpCodeStudioViewModel;

        if (_currentVm != null && _editor != null)
        {
            _currentVm.RequestNavigateToCaret += OnNavigateToCaret;
            _currentVm.RequestGoToLine += ScrollToAndSelectLine;
            _currentVm.RequestFoldAll += FoldAll;
            _currentVm.RequestUnfoldAll += UnfoldAll;
            _currentVm.RequestToggleSearch += ToggleSearch;
            _currentVm.RequestSetPausedLine += OnSetPausedLine;
            _currentVm.RequestSyncBreakpoints += OnSyncBreakpoints;
            _currentVm.RequestReloadEditorText += OnReloadEditorText;
            _currentVm.RequestSwitchTabDocument += OnSwitchTabDocument;
            _currentVm.PropertyChanged += OnVmPropertyChanged;

            _breakpointMargin.SetBreakpoints(_currentVm.Breakpoints.Where(b => b.IsEnabled).Select(b => b.LineNumber));

            _completionController = new CSharpEditorCompletionController(_editor, _currentVm.CompilerService)
            {
                LanguageMode = _currentVm.CurrentLanguageMode
            };

            _editor.WordWrap = _currentVm.IsWordWrap;
            if (_editor.Options != null)
            {
                _editor.Options.IndentationSize = _currentVm.IndentationSize;
            }

            _isUpdatingText = true;
            try
            {
                var activeTab = _currentVm.OpenTabs.FirstOrDefault(t => t.IsActive) ?? _currentVm.OpenTabs.FirstOrDefault();
                if (activeTab?.DocumentModel != null)
                {
                    _editor.Document = activeTab.DocumentModel;
                }
                else
                {
                    _editor.Text = _currentVm.Code ?? string.Empty;
                }
                UpdateCodeFolding();
            }
            finally
            {
                _isUpdatingText = false;
            }
        }
    }

    private void OnSwitchTabDocument(StudioTabItemViewModel tab)
    {
        if (_editor == null) return;

        _isUpdatingText = true;
        try
        {
            var doc = tab.DocumentModel;
            var targetCode = tab.Document.Code ?? string.Empty;
            if (doc.Text != targetCode)
            {
                doc.Text = targetCode;
            }
            _editor.Document = doc;

            UpdateCodeFolding();

            var maxLines = Math.Max(1, _editor.Document?.LineCount ?? 1);
            var targetLine = Math.Clamp(tab.CaretLine, 1, maxLines);
            _editor.TextArea.Caret.Line = targetLine;
            _editor.TextArea.Caret.Column = Math.Max(1, tab.CaretColumn);
            _editor.ScrollTo(targetLine, Math.Max(1, tab.CaretColumn));
        }
        finally
        {
            _isUpdatingText = false;
        }
    }

    private void OnReloadEditorText()
    {
        if (_editor == null || _currentVm == null) return;

        _isUpdatingText = true;
        try
        {
            var targetCode = _currentVm.Code ?? string.Empty;
            if (_editor.Document != null)
            {
                if (_editor.Document.Text != targetCode)
                {
                    _editor.Document.Text = targetCode;
                }
            }
            else
            {
                _editor.Text = targetCode;
            }
            UpdateCodeFolding();
        }
        finally
        {
            _isUpdatingText = false;
        }
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_editor == null || _currentVm == null) return;

        if (e.PropertyName == nameof(CSharpCodeStudioViewModel.IsWordWrap))
        {
            _editor.WordWrap = _currentVm.IsWordWrap;
        }
        else if (e.PropertyName == nameof(CSharpCodeStudioViewModel.SelectedLanguageModeIndex))
        {
            if (_completionController != null)
            {
                _completionController.LanguageMode = _currentVm.CurrentLanguageMode;
            }
        }
        else if (e.PropertyName == nameof(CSharpCodeStudioViewModel.IndentationSize))
        {
            if (_editor.Options != null)
            {
                _editor.Options.IndentationSize = _currentVm.IndentationSize;
            }
        }
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingText || _editor == null || _currentVm == null) return;

        _currentVm.Code = _editor.Text;

        _foldingTimer.Stop();
        _foldingTimer.Start();
    }

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        if (_editor == null || _currentVm == null) return;

        var caret = _editor.TextArea.Caret;
        _currentVm.SetCaretPosition(caret.Line, caret.Column);

        var activeTab = _currentVm.OpenTabs.FirstOrDefault(t => t.Id == _currentVm.Script.Id);
        if (activeTab != null)
        {
            activeTab.CaretLine = caret.Line;
            activeTab.CaretColumn = caret.Column;
        }
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (_editor == null) return;

        if (e.Key == Key.F9)
        {
            _currentVm?.ToggleBreakpoint(_editor.TextArea.Caret.Line);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F5 && !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            if (_currentVm?.IsPaused == true)
            {
                _currentVm.ContinueDebug();
                e.Handled = true;
                return;
            }
            else if (_currentVm?.IsExecuting == false && _currentVm?.IsDebugging == false)
            {
                _ = _currentVm.DebugCodeCommand.ExecuteAsync(null);
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.F10 && _currentVm?.IsPaused == true)
        {
            _currentVm.StepOver();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F11 && _currentVm?.IsPaused == true)
        {
            _currentVm.StepInto();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F5 && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && _currentVm?.IsDebugging == true)
        {
            _currentVm.StopDebug();
            e.Handled = true;
            return;
        }

        if (_foldingManager == null) return;

        var isModifier = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        if (isModifier && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && (e.Key == Key.OemOpenBrackets || e.Key == Key.Oem4))
        {
            ToggleFoldAtCaret(fold: true);
            e.Handled = true;
            return;
        }

        if (isModifier && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && (e.Key == Key.OemCloseBrackets || e.Key == Key.Oem6))
        {
            ToggleFoldAtCaret(fold: false);
            e.Handled = true;
            return;
        }
    }

    private void OnSetPausedLine(int line)
    {
        if (_editor == null) return;
        if (line == -1)
        {
            _debugHoverController?.HideTip();
        }
        _breakpointMargin.CurrentPausedLine = line;
        _debugLineRenderer.CurrentPausedLine = line;
        _editor.TextArea.TextView.InvalidateVisual();
    }

    private void OnSyncBreakpoints(IEnumerable<int> lines)
    {
        _breakpointMargin.SetBreakpoints(lines);
    }

    private void ToggleFoldAtCaret(bool? fold = null)
    {
        if (_editor == null || _foldingManager == null) return;
        int offset = _editor.CaretOffset;
        var foldings = _foldingManager.GetFoldingsContaining(offset);
        var target = foldings.OrderByDescending(f => f.StartOffset).FirstOrDefault();
        if (target != null)
        {
            target.IsFolded = fold ?? !target.IsFolded;
        }
    }

    public void FoldAll()
    {
        if (_foldingManager == null) return;
        foreach (var fold in _foldingManager.AllFoldings)
        {
            fold.IsFolded = true;
        }
    }

    public void UnfoldAll()
    {
        if (_foldingManager == null) return;
        foreach (var fold in _foldingManager.AllFoldings)
        {
            fold.IsFolded = false;
        }
    }

    public void ToggleSearch()
    {
        _searchPanel?.Open();
    }

    private void OnNavigateToCaret(int line, int col)
    {
        if (_editor == null) return;

        try
        {
            _editor.TextArea.Caret.Line = line;
            _editor.TextArea.Caret.Column = col;
            _editor.ScrollTo(line, col);
            _editor.Focus();
        }
        catch
        {
            // Ignore invalid line index if text modified
        }
    }

    public async void OnOpenProjectClick(object? sender, RoutedEventArgs e)
    {
        await OpenProjectOrFileDialogAsync();
    }

    private async Task OpenProjectOrFileDialogAsync()
    {
        if (_currentVm == null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } storageProvider) return;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Project or Script File",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("FryPDF Project / Document (*.frycsproj, *.frynbproj, *.frycs, *.frynb, *.csproj, *.cs, *.csx, *.zip)")
                {
                    Patterns = new[] { "*.frycsproj", "*.frynbproj", "*.frycs", "*.frynb", "*.csproj", "*.cs", "*.csx", "*.zip" }
                },
                new("C# Files (*.cs, *.csx, *.frycs)")
                {
                    Patterns = new[] { "*.cs", "*.csx", "*.frycs" }
                },
                new("C# Notebooks (*.frynb, *.frynbproj)")
                {
                    Patterns = new[] { "*.frynb", "*.frynbproj" }
                },
                new("Project Archives (*.zip)")
                {
                    Patterns = new[] { "*.zip" }
                },
                new("All Files (*.*)")
                {
                    Patterns = new[] { "*.*" }
                }
            }
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } filePath)
        {
            await _currentVm.OpenExternalProjectAsync(filePath);
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Contains(DataFormat.File))
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (_currentVm == null) return;

        if (e.DataTransfer.Contains(DataFormat.File))
        {
            var files = e.DataTransfer.TryGetFiles();
            if (files != null)
            {
                var first = files.FirstOrDefault();
                if (first != null && first.TryGetLocalPath() is { } localPath)
                {
                    await _currentVm.OpenExternalProjectAsync(localPath);
                    e.Handled = true;
                }
            }
        }
    }

    private void OnRenameTextBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is ExplorerItemViewModel itemVm && itemVm.IsRenaming)
        {
            itemVm.CommitRenameCommand.Execute(null);
        }
    }

    private void CloseActiveTab()
    {
        if (_currentVm == null) return;
        var activeTab = _currentVm.OpenTabs.FirstOrDefault(t => t.IsActive);
        if (activeTab != null)
        {
            _ = _currentVm.CloseTabAsync(activeTab);
        }
    }

    public void ScrollToAndSelectLine(int lineNumber)
    {
        if (_editor?.Document == null) return;
        if (lineNumber < 1) lineNumber = 1;
        if (lineNumber > _editor.Document.LineCount) lineNumber = _editor.Document.LineCount;

        var line = _editor.Document.GetLineByNumber(lineNumber);
        _editor.CaretOffset = line.Offset;
        _editor.ScrollTo(lineNumber, 1);
        _editor.Focus();
    }

    public void ToggleLineComment()
    {
        if (_editor?.Document == null) return;
        var document = _editor.Document;
        var selection = _editor.TextArea.Selection;
        int startLine;
        int endLine;

        if (!selection.IsEmpty)
        {
            startLine = document.GetLineByOffset(selection.SurroundingSegment.Offset).LineNumber;
            endLine = document.GetLineByOffset(selection.SurroundingSegment.EndOffset).LineNumber;
        }
        else
        {
            startLine = document.GetLineByOffset(_editor.CaretOffset).LineNumber;
            endLine = startLine;
        }

        using (document.RunUpdate())
        {
            bool allCommented = true;
            for (int i = startLine; i <= endLine; i++)
            {
                var line = document.GetLineByNumber(i);
                var lineText = document.GetText(line.Offset, line.Length).TrimStart();
                if (!string.IsNullOrEmpty(lineText) && !lineText.StartsWith("//"))
                {
                    allCommented = false;
                    break;
                }
            }

            for (int i = startLine; i <= endLine; i++)
            {
                var line = document.GetLineByNumber(i);
                var lineText = document.GetText(line.Offset, line.Length);
                if (allCommented)
                {
                    int slashIdx = lineText.IndexOf("//");
                    if (slashIdx >= 0)
                    {
                        int removeLen = (slashIdx + 2 < lineText.Length && lineText[slashIdx + 2] == ' ') ? 3 : 2;
                        document.Remove(line.Offset + slashIdx, removeLen);
                    }
                }
                else
                {
                    int indent = 0;
                    while (indent < lineText.Length && char.IsWhiteSpace(lineText[indent])) indent++;
                    document.Insert(line.Offset + indent, "// ");
                }
            }
        }
    }

    public void OnTabPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed)
        {
            if (sender is Visual v && v.DataContext is StudioTabItemViewModel tabVm)
            {
                tabVm.Close();
                e.Handled = true;
            }
        }
    }
}
