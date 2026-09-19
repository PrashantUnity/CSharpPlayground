using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

public partial class CSharpCodeStudioViewModel : ObservableObject
{
    private readonly IScriptStorageService _storageService;
    private readonly RoslynCompilerService _compilerService;
    public RoslynCompilerService CompilerService => _compilerService;
    private readonly ScriptExecutionEngine _executionEngine;
    private readonly ScriptDebuggerService _debuggerService;
    private readonly NotebookExecutionKernel _kernel;
    private readonly Action _backToHubAction;
    private readonly Action? _backToHomeAction;
    private readonly Action<NotebookDocumentItem>? _openNotebookAction;

    public QuickOpenViewModel QuickOpen { get; } = new();
    public event Action<int>? RequestGoToLine;

    [ObservableProperty]
    private int _indentationSize = 4;

    public string IndentationStatusText => $"Spaces: {IndentationSize}";

    public string LanguageModeStatusText => SelectedLanguageModeIndex switch
    {
        1 => "C# Program",
        2 => "C# Expression",
        _ => "C# Statements"
    };

    [RelayCommand]
    public void ToggleIndentation()
    {
        IndentationSize = IndentationSize == 4 ? 2 : 4;
        OnPropertyChanged(nameof(IndentationStatusText));
    }

    [RelayCommand]
    public void SetLanguageMode(string? modeIndexStr)
    {
        if (int.TryParse(modeIndexStr, out var idx) && idx >= 0 && idx <= 2)
        {
            SelectedLanguageModeIndex = idx;
        }
    }

    private CancellationTokenSource? _diagnosticsCts;
    private CancellationTokenSource? _executionCts;
    private readonly Func<int> _getTimeoutSeconds;

    [ObservableProperty]
    private ScriptDocumentItem _script;

    [ObservableProperty]
    private string _code = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    // ── VS Code Multi-Tab Document Strip ──
    public ObservableCollection<StudioTabItemViewModel> OpenTabs { get; } = new();

    // ── VS Code Layout: Activity Bar & Primary Side Bar ──
    // 0=Explorer, 1=Search, 2=Debug, 3=NuGet, 4=Scratchpad, 5=Problems
    [ObservableProperty]
    private int _selectedActivityBarIndex = 0;

    [ObservableProperty]
    private bool _isSideBarVisible = true;

    [ObservableProperty]
    private Avalonia.Controls.GridLength _sideBarGridLength = new(280, Avalonia.Controls.GridUnitType.Pixel);

    private double _savedSideBarWidth = 280;

    partial void OnIsSideBarVisibleChanged(bool value)
    {
        if (value)
        {
            SideBarGridLength = new Avalonia.Controls.GridLength(_savedSideBarWidth > 120 ? _savedSideBarWidth : 280, Avalonia.Controls.GridUnitType.Pixel);
        }
        else
        {
            if (SideBarGridLength.IsAbsolute && SideBarGridLength.Value > 120)
            {
                _savedSideBarWidth = SideBarGridLength.Value;
            }
            SideBarGridLength = new Avalonia.Controls.GridLength(0, Avalonia.Controls.GridUnitType.Pixel);
        }
    }

    [ObservableProperty]
    private string _sideBarTitle = "EXPLORER";

    [ObservableProperty]
    private int _selectedLeftTabIndex = -1;

    public bool IsExplorerActive => SelectedActivityBarIndex == 0;
    public bool IsSearchActive => SelectedActivityBarIndex == 1;
    public bool IsDebugActive => SelectedActivityBarIndex == 2;
    public bool IsDependenciesActive => SelectedActivityBarIndex == 3;
    public bool IsScratchpadActive => SelectedActivityBarIndex == 4;
    public bool IsProblemsActive => SelectedActivityBarIndex == 5;

    partial void OnSelectedActivityBarIndexChanged(int value)
    {
        SideBarTitle = value switch
        {
            1 => "SEARCH",
            2 => "RUN AND DEBUG",
            3 => "DEPENDENCIES & NUGET",
            4 => "SCRATCHPAD & NOTES",
            5 => "PROBLEMS",
            _ => "EXPLORER"
        };

        OnPropertyChanged(nameof(IsExplorerActive));
        OnPropertyChanged(nameof(IsSearchActive));
        OnPropertyChanged(nameof(IsDebugActive));
        OnPropertyChanged(nameof(IsDependenciesActive));
        OnPropertyChanged(nameof(IsScratchpadActive));
        OnPropertyChanged(nameof(IsProblemsActive));
    }

    partial void OnSelectedLeftTabIndexChanged(int value)
    {
        switch (value)
        {
            case 0:
                SelectedActivityBarIndex = 4; // Scratchpad & Notes
                IsSideBarVisible = true;
                break;
            case 1:
                SelectedActivityBarIndex = 3; // Dependencies & NuGet
                IsSideBarVisible = true;
                break;
            case 2:
                SelectedBottomTabIndex = 3; // Test Cases
                IsBottomDeckExpanded = true;
                break;
        }
    }

    [ObservableProperty]
    private int _selectedLanguageModeIndex = 0;

    [ObservableProperty]
    private int _caretLine = 1;

    [ObservableProperty]
    private int _caretColumn = 1;

    public event Action? RequestReloadEditorText;
    public event Action<StudioTabItemViewModel>? RequestSwitchTabDocument;
    public event Action<int, int>? RequestNavigateToCaret;

    public ObservableCollection<string> LanguageModes { get; } = new()
    {
        "C# Statements",
        "C# Program (Main)",
        "C# Expression"
    };

    public ExecutionLanguageMode CurrentLanguageMode => SelectedLanguageModeIndex switch
    {
        1 => ExecutionLanguageMode.Program,
        2 => ExecutionLanguageMode.Expression,
        _ => ExecutionLanguageMode.Statements
    };

    public CSharpCodeStudioViewModel(
        ScriptDocumentItem script,
        IScriptStorageService storageService,
        RoslynCompilerService compilerService,
        ScriptExecutionEngine executionEngine,
        Action backToHubAction,
        Action? backToHomeAction = null,
        Func<int>? getTimeoutSeconds = null,
        Action<NotebookDocumentItem>? openNotebookAction = null)
    {
        _script = script;
        _storageService = storageService;
        _compilerService = compilerService;
        _executionEngine = executionEngine;
        _debuggerService = new ScriptDebuggerService(_compilerService, _executionEngine);
        _backToHubAction = backToHubAction;
        _backToHomeAction = backToHomeAction;
        _openNotebookAction = openNotebookAction;
        _getTimeoutSeconds = getTimeoutSeconds ?? (() => 10);
        _kernel = new NotebookExecutionKernel();

        _code = script.Code;
        _notes = script.Notes;
        _selectedLanguageModeIndex = script.ExecutionMode switch
        {
            "Program" => 1,
            "Expression" => 2,
            _ => 0
        };

        foreach (var r in _compilerService.AvailableReferences)
        {
            References.Add(new AssemblyReferenceViewModel(r));
        }

        foreach (var tc in script.TestCases)
        {
            TestCases.Add(tc);
        }

        if (TestCases.Count == 0)
        {
            TestCases.Add(new TestCaseItem { Name = "Case 1", Input = "// Sample input parameters" });
        }

        Breakpoints.Clear();
        foreach (var bpLine in script.Breakpoints)
        {
            Breakpoints.Add(new BreakpointItem { LineNumber = bpLine, IsEnabled = true });
        }

        _allTemplates.AddRange(CodeTemplateLibrary.GetTemplates());
        RefreshFilteredTemplates();

        OpenTabs.Add(CreateTab(script, isActive: true));

        QuickOpen.RequestGoToLine += line => RequestGoToLine?.Invoke(line);
        InitializeQuickOpenCommands();
        RefreshQuickOpenDocuments();

        TriggerDiagnosticsCheck();
        PopulateExplorerTree();
    }

    partial void OnCodeChanged(string value)
    {
        Script.Code = value;
        Script.LastModified = DateTime.UtcNow;
        var activeTab = OpenTabs.FirstOrDefault(t => t.Id == Script.Id);
        if (activeTab != null)
        {
            activeTab.IsDirty = true;
            activeTab.Document.Code = value;
        }
        TriggerDiagnosticsCheck();
    }

    partial void OnNotesChanged(string value)
    {
        Script.Notes = value;
        Script.LastModified = DateTime.UtcNow;
    }

    partial void OnSelectedLanguageModeIndexChanged(int value)
    {
        Script.ExecutionMode = value switch
        {
            1 => "Program",
            2 => "Expression",
            _ => "Statements"
        };
        OnPropertyChanged(nameof(LanguageModeStatusText));
        TriggerDiagnosticsCheck();
    }

    public void SetCaretPosition(int line, int col)
    {
        CaretLine = line;
        CaretColumn = col;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        Script.Code = Code;
        Script.Notes = Notes;
        Script.TestCases = TestCases.ToList();
        Script.LastModified = DateTime.UtcNow;
        var saved = await _storageService.SaveScriptAsync(Script);
        CompilerStatusText = saved ? "Saved" : "⚠️ Save failed — check disk space/permissions";

        var activeTab = OpenTabs.FirstOrDefault(t => t.Id == Script.Id);
        if (activeTab != null)
        {
            activeTab.IsDirty = false;
            activeTab.NotifyTitleChanged();
        }
    }

    [RelayCommand]
    private void BackToHub()
    {
        _ = SaveAsync();
        _backToHubAction.Invoke();
    }

    [RelayCommand]
    private void BackToHome()
    {
        _ = SaveAsync();
        _backToHomeAction?.Invoke();
    }

    [RelayCommand]
    private void SetLeftTab(string index)
    {
        if (int.TryParse(index, out var idx))
        {
            SelectedLeftTabIndex = idx;
        }
    }

    [RelayCommand]
    public void ToggleSideBar()
    {
        IsSideBarVisible = !IsSideBarVisible;
    }

    [RelayCommand]
    public void SelectActivityBarItem(string? indexStr)
    {
        if (int.TryParse(indexStr, out var index))
        {
            SelectActivityBarItem(index);
        }
    }

    public void SelectActivityBarItem(int index)
    {
        if (SelectedActivityBarIndex == index)
        {
            IsSideBarVisible = !IsSideBarVisible;
        }
        else
        {
            SelectedActivityBarIndex = index;
            IsSideBarVisible = true;
        }
    }
}
