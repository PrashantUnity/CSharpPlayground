using System.Collections.ObjectModel;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

public partial class CSharpCodeStudioViewModel
{
    private StudioTabItemViewModel CreateTab(ScriptDocumentItem document, bool isActive = false)
    {
        return new StudioTabItemViewModel(document, isActive)
        {
            OnSelect = t => { _ = SwitchToTabAsync(t); },
            OnClose = t => { _ = CloseTabAsync(t); },
            OnCloseOthers = t => { _ = CloseOtherTabsAsync(t); },
            OnCloseToTheRight = t => { _ = CloseTabsToTheRightAsync(t); },
            OnCloseAll = tab => { _ = CloseAllTabsAsync(); },
            OnCopyPath = t => CopyTabPath(t),
            OnRevealInExplorer = t => RevealTabInExplorer(t)
        };
    }

    public async Task SwitchToTabAsync(StudioTabItemViewModel tab)
    {
        if (tab.Id == Script.Id && tab.IsActive) return;

        // 1. Save state of current active tab
        var currentTab = OpenTabs.FirstOrDefault(t => t.Id == Script.Id);
        if (currentTab != null)
        {
            currentTab.Document.Code = Code;
            currentTab.Document.Notes = Notes;
            currentTab.ConsoleOutput = ConsoleOutput;
            currentTab.ExecutionTimeText = ExecutionTimeText;
            currentTab.CompilerStatusText = CompilerStatusText;
            currentTab.PausedLine = CurrentPausedLine;
            currentTab.IsExecuting = IsExecuting;
            currentTab.IsDebugging = IsDebugging;
            currentTab.IsPaused = IsPaused;
            currentTab.SelectedBottomTabIndex = SelectedBottomTabIndex;

            currentTab.Diagnostics.Clear();
            foreach (var d in Diagnostics) currentTab.Diagnostics.Add(d);

            currentTab.DumpResults.Clear();
            foreach (var r in DumpResults) currentTab.DumpResults.Add(r);

            currentTab.RichOutputs.Clear();
            foreach (var ro in RichOutputs) currentTab.RichOutputs.Add(ro);

            currentTab.Locals.Clear();
            foreach (var l in Locals) currentTab.Locals.Add(l);

            currentTab.CallStack.Clear();
            foreach (var cs in CallStack) currentTab.CallStack.Add(cs);
        }

        // 2. Mark active flags
        foreach (var t in OpenTabs)
        {
            t.IsActive = (t.Id == tab.Id);
        }

        // 3. Restore target tab state into active studio context
        Script = tab.Document;
        Code = tab.Document.Code ?? string.Empty;
        Notes = tab.Document.Notes;
        SelectedLanguageModeIndex = tab.Document.ExecutionMode switch
        {
            "Program" => 1,
            "Expression" => 2,
            _ => 0
        };

        ConsoleOutput = tab.ConsoleOutput;
        ExecutionTimeText = tab.ExecutionTimeText;
        CompilerStatusText = tab.CompilerStatusText;
        CurrentPausedLine = tab.PausedLine;
        IsExecuting = tab.IsExecuting;
        IsDebugging = tab.IsDebugging;
        IsPaused = tab.IsPaused;
        SelectedBottomTabIndex = tab.SelectedBottomTabIndex;

        Diagnostics.Clear();
        foreach (var d in tab.Diagnostics) Diagnostics.Add(d);

        DumpResults.Clear();
        foreach (var r in tab.DumpResults) DumpResults.Add(r);

        RichOutputs.Clear();
        foreach (var ro in tab.RichOutputs) RichOutputs.Add(ro);

        Locals.Clear();
        foreach (var l in tab.Locals) Locals.Add(l);

        CallStack.Clear();
        foreach (var cs in tab.CallStack) CallStack.Add(cs);

        TestCases.Clear();
        foreach (var tc in tab.Document.TestCases)
        {
            TestCases.Add(tc);
        }

        Breakpoints.Clear();
        foreach (var bpLine in tab.Document.Breakpoints)
        {
            Breakpoints.Add(new BreakpointItem { LineNumber = bpLine, IsEnabled = true });
        }

        RequestSwitchTabDocument?.Invoke(tab);
        RequestSyncBreakpoints?.Invoke(Breakpoints.Where(b => b.IsEnabled).Select(b => b.LineNumber));
        RequestSetPausedLine?.Invoke(CurrentPausedLine > 0 ? CurrentPausedLine : -1);
        RequestReloadEditorText?.Invoke();

        ErrorCount = Diagnostics.Count(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        WarningCount = Diagnostics.Count(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Warning);

        if (Diagnostics.Count == 0 && !string.IsNullOrWhiteSpace(Code))
        {
            TriggerDiagnosticsCheck();
        }

        await RefreshExplorerAsync();
    }

    public async Task CloseTabAsync(StudioTabItemViewModel tab)
    {
        if (OpenTabs.Count <= 1)
        {
            var freshScript = new ScriptDocumentItem
            {
                Title = "Untitled Script",
                Code = "// Welcome to C# Code Studio\nConsole.WriteLine(\"Hello, World!\");\n",
                Notes = string.Empty
            };
            var freshTab = CreateTab(freshScript, isActive: true);
            OpenTabs.Add(freshTab);
            await SwitchToTabAsync(freshTab);
        }

        int index = OpenTabs.IndexOf(tab);
        if (index >= 0)
        {
            OpenTabs.Remove(tab);
            if (tab.IsActive && OpenTabs.Count > 0)
            {
                var nextTab = (index < OpenTabs.Count) ? OpenTabs[index] : OpenTabs[^1];
                await SwitchToTabAsync(nextTab);
            }
        }
        RefreshQuickOpenDocuments();
    }

    public async Task CloseOtherTabsAsync(StudioTabItemViewModel tab)
    {
        if (OpenTabs.Count <= 1) return;
        var toRemove = OpenTabs.Where(t => t.Id != tab.Id).ToList();
        foreach (var t in toRemove)
        {
            OpenTabs.Remove(t);
        }
        if (!tab.IsActive)
        {
            await SwitchToTabAsync(tab);
        }
        RefreshQuickOpenDocuments();
    }

    public async Task CloseTabsToTheRightAsync(StudioTabItemViewModel tab)
    {
        int index = OpenTabs.IndexOf(tab);
        if (index < 0 || index >= OpenTabs.Count - 1) return;

        var toRemove = OpenTabs.Skip(index + 1).ToList();
        foreach (var t in toRemove)
        {
            OpenTabs.Remove(t);
        }
        if (!tab.IsActive && !OpenTabs.Any(t => t.IsActive))
        {
            await SwitchToTabAsync(tab);
        }
        RefreshQuickOpenDocuments();
    }

    public async Task CloseAllTabsAsync()
    {
        var freshScript = new ScriptDocumentItem
        {
            Title = "Untitled Script",
            Code = "// Welcome to C# Code Studio\nConsole.WriteLine(\"Hello, World!\");\n",
            Notes = string.Empty
        };
        var freshTab = CreateTab(freshScript, isActive: true);
        OpenTabs.Clear();
        OpenTabs.Add(freshTab);
        await SwitchToTabAsync(freshTab);
        RefreshQuickOpenDocuments();
    }

    public void CopyTabPath(StudioTabItemViewModel tab)
    {
        try
        {
            var text = !string.IsNullOrEmpty(tab.Document.Title) ? tab.Document.Title : "Untitled Script";
            _ = CopyTextToClipboardAsync(text);
        }
        catch
        {
        }
    }

    public void RevealTabInExplorer(StudioTabItemViewModel tab)
    {
        SelectedActivityBarIndex = 0; // Explorer
        IsSideBarVisible = true;
        HighlightExplorerItem(tab.Id);
    }

    [RelayCommand]
    public void ShowQuickOpen(string? mode)
    {
        RefreshQuickOpenDocuments();
        var qMode = mode?.ToLowerInvariant() switch
        {
            "commands" => QuickOpenMode.Commands,
            "line" => QuickOpenMode.GoToLine,
            _ => QuickOpenMode.Files
        };
        QuickOpen.Show(qMode);
    }

    [RelayCommand]
    public void ShowCommandPalette() => ShowQuickOpen("commands");

    [RelayCommand]
    public void ShowGoToLine() => ShowQuickOpen("line");

    [RelayCommand]
    public async Task ExportScriptToCsAsync()
    {
        if (Script == null) return;
        var content = DocumentExportService.ExportScriptToCs(Script);
        await CopyTextToClipboardAsync(content);
        ConsoleOutput += $"\n[Export] Script '{Script.Title}' exported to standalone C# source (.cs) and copied to clipboard!\n";
    }

    [RelayCommand]
    public async Task ExportScriptToCsxAsync()
    {
        if (Script == null) return;
        var content = DocumentExportService.ExportScriptToCsx(Script);
        await CopyTextToClipboardAsync(content);
        ConsoleOutput += $"\n[Export] Script '{Script.Title}' exported to C# Script (.csx) and copied to clipboard!\n";
    }

    private static async Task CopyTextToClipboardAsync(string text)
    {
        try
        {
            var clipboard = Avalonia.Application.Current?.ApplicationLifetime switch
            {
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop => desktop.MainWindow?.Clipboard,
                Avalonia.Controls.ApplicationLifetimes.ISingleViewApplicationLifetime singleView => Avalonia.Controls.TopLevel.GetTopLevel(singleView.MainView)?.Clipboard,
                _ => null
            };
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(text);
            }
        }
        catch
        {
        }
    }

    private void InitializeQuickOpenCommands()
    {
        var cmds = new List<QuickOpenItem>
        {
            new() { Title = "Run: Execute Script", Subtitle = "Compile and execute active script without debugging", Category = "Run", IconKind = "Play", IconColorHex = "#75D59A", ShortcutHint = "Ctrl+F5", ExecuteAction = () => _ = RunCodeCommand.ExecuteAsync(null) },
            new() { Title = "Debug: Start Debugging", Subtitle = "Compile with instrumentation and debug", Category = "Debug", IconKind = "BugPlayOutline", IconColorHex = "#58A6FF", ShortcutHint = "F5", ExecuteAction = () => _ = DebugCodeCommand.ExecuteAsync(null) },
            new() { Title = "Debug: Step Over", Subtitle = "Step to next statement", Category = "Debug", IconKind = "DebugStepOver", IconColorHex = "#D97706", ShortcutHint = "F10", ExecuteAction = StepOver },
            new() { Title = "Debug: Step Into", Subtitle = "Step into expression or function", Category = "Debug", IconKind = "DebugStepInto", IconColorHex = "#D97706", ShortcutHint = "F11", ExecuteAction = StepInto },
            new() { Title = "Debug: Stop Debugging", Subtitle = "Terminate active debug session", Category = "Debug", IconKind = "Stop", IconColorHex = "#E5534B", ShortcutHint = "Shift+F5", ExecuteAction = StopDebug },
            new() { Title = "File: Save Active Script", Subtitle = "Persist current script changes to storage", Category = "File", IconKind = "ContentSaveOutline", IconColorHex = "#58A6FF", ShortcutHint = "Ctrl+S", ExecuteAction = () => _ = SaveCommand.ExecuteAsync(null) },
            new() { Title = "File: New Script", Subtitle = "Create a new C# script tab", Category = "File", IconKind = "FilePlusOutline", IconColorHex = "#58A6FF", ShortcutHint = "Ctrl+N", ExecuteAction = () => _ = NewScriptCommand.ExecuteAsync(null) },
            new() { Title = "File: Close Active Tab", Subtitle = "Close the currently focused script tab", Category = "Tabs", IconKind = "Close", IconColorHex = "#E5534B", ShortcutHint = "Ctrl+W", ExecuteAction = () => { var a = OpenTabs.FirstOrDefault(t => t.IsActive); if (a != null) _ = CloseTabAsync(a); } },
            new() { Title = "File: Close Other Tabs", Subtitle = "Close all tabs except the active one", Category = "Tabs", IconKind = "CloseBoxMultipleOutline", IconColorHex = "#E5534B", ExecuteAction = () => { var a = OpenTabs.FirstOrDefault(t => t.IsActive); if (a != null) _ = CloseOtherTabsAsync(a); } },
            new() { Title = "File: Close All Tabs", Subtitle = "Close all open script tabs", Category = "Tabs", IconKind = "CloseCircleMultipleOutline", IconColorHex = "#E5534B", ExecuteAction = () => _ = CloseAllTabsAsync() },
            new() { Title = "Format: Format Document", Subtitle = "Format C# code using Roslyn syntax normalizer", Category = "Editor", IconKind = "FormatPaint", IconColorHex = "#75D59A", ShortcutHint = "Shift+Alt+F", ExecuteAction = FormatCode },
            new() { Title = "Editor: Toggle Word Wrap", Subtitle = "Toggle soft line wrapping in editor canvas", Category = "View", IconKind = "Wrap", IconColorHex = "#58A6FF", ShortcutHint = "Alt+Z", ExecuteAction = ToggleWordWrap },
            new() { Title = "View: Toggle Primary Side Bar", Subtitle = "Expand or collapse the primary activity sidebar", Category = "View", IconKind = "DockLeft", IconColorHex = "#58A6FF", ShortcutHint = "Ctrl+B", ExecuteAction = ToggleSideBar },
            new() { Title = "View: Toggle Bottom Panel", Subtitle = "Expand or collapse problems & output deck", Category = "View", IconKind = "DockBottom", IconColorHex = "#58A6FF", ShortcutHint = "Ctrl+J", ExecuteAction = ToggleBottomDeck },
            new() { Title = "View: Go to Line...", Subtitle = "Jump to specific line number in the active editor", Category = "Navigation", IconKind = "RayStartArrow", IconColorHex = "#75D59A", ShortcutHint = "Ctrl+G", ExecuteAction = ShowGoToLine },
            new() { Title = "View: Show Explorer", Subtitle = "Focus project explorer in side bar", Category = "Navigation", IconKind = "FolderMultipleOutline", IconColorHex = "#D97706", ShortcutHint = "Ctrl+Shift+E", ExecuteAction = () => SelectActivityBarItem(0) },
            new() { Title = "View: Show Search in Script", Subtitle = "Focus text search and replace panel", Category = "Navigation", IconKind = "Magnify", IconColorHex = "#58A6FF", ShortcutHint = "Ctrl+Shift+F", ExecuteAction = () => SelectActivityBarItem(1) },
            new() { Title = "View: Show Debug Panel", Subtitle = "Focus breakpoints, call stack, and locals", Category = "Navigation", IconKind = "BugPlayOutline", IconColorHex = "#58A6FF", ShortcutHint = "Ctrl+Shift+D", ExecuteAction = () => SelectActivityBarItem(2) },
            new() { Title = "View: Show NuGet Packages", Subtitle = "Browse and install NuGet package references", Category = "Navigation", IconKind = "PackageVariantClosed", IconColorHex = "#58A6FF", ShortcutHint = "Ctrl+Shift+X", ExecuteAction = () => SelectActivityBarItem(3) },
            new() { Title = "View: Show Problems", Subtitle = "Focus Roslyn compiler diagnostics list", Category = "Navigation", IconKind = "AlertCircleOutline", IconColorHex = "#E5534B", ShortcutHint = "Ctrl+Shift+M", ExecuteAction = () => ShowProblemsTabCommand.Execute(null) },
            new() { Title = "Export: Export as Standalone C# File (.cs)", Subtitle = "Copy clean C# code to clipboard with headers", Category = "Export", IconKind = "ExportVariant", IconColorHex = "#75D59A", ExecuteAction = () => _ = ExportScriptToCsAsync() },
            new() { Title = "Export: Export as C# Script (.csx)", Subtitle = "Copy Roslyn script with #r NuGet directives to clipboard", Category = "Export", IconKind = "ExportVariant", IconColorHex = "#75D59A", ExecuteAction = () => _ = ExportScriptToCsxAsync() },
            new() { Title = "Output: Clear Console", Subtitle = "Clear terminal execution stdout/stderr buffer", Category = "Terminal", IconKind = "Broom", IconColorHex = "#8B949E", ExecuteAction = ClearConsole },
            new() { Title = "Output: Clear Problems", Subtitle = "Clear active compiler error & warning list", Category = "Diagnostics", IconKind = "Broom", IconColorHex = "#8B949E", ExecuteAction = () => Diagnostics.Clear() },
            new() { Title = "Breakpoints: Clear All", Subtitle = "Remove all active breakpoints from current script", Category = "Debug", IconKind = "CloseCircleOutline", IconColorHex = "#E5534B", ExecuteAction = ClearAllBreakpoints },
            new() { Title = "Hub: Return to Workspace Manager", Subtitle = "Navigate back to Hub dashboard", Category = "Navigation", IconKind = "HomeOutline", IconColorHex = "#58A6FF", ExecuteAction = BackToHub }
        };
        QuickOpen.RegisterCommands(cmds);
    }

    public void RefreshQuickOpenDocuments()
    {
        var docs = new List<QuickOpenItem>();

        foreach (var tab in OpenTabs)
        {
            docs.Add(new QuickOpenItem
            {
                Title = tab.Title,
                Subtitle = tab.IsActive ? "Currently Active Script" : "Open Tab",
                Category = "Open Tabs",
                IconKind = "FileCodeOutline",
                IconColorHex = "#58A6FF",
                Kind = QuickOpenItemKind.Document,
                ExecuteAction = () => _ = SwitchToTabAsync(tab)
            });
        }

        foreach (var tmpl in _allTemplates)
        {
            docs.Add(new QuickOpenItem
            {
                Title = tmpl.Title,
                Subtitle = $"Starter Template • {tmpl.Category}",
                Category = "Templates",
                IconKind = "CodeTags",
                IconColorHex = "#75D59A",
                Kind = QuickOpenItemKind.Template,
                ExecuteAction = () => InsertTemplate(tmpl)
            });
        }

        QuickOpen.RegisterDocuments(docs);
    }

    private void AddOrActivateTab(ScriptDocumentItem document)
    {
        var existing = OpenTabs.FirstOrDefault(t => t.Id == document.Id);
        if (existing != null)
        {
            foreach (var t in OpenTabs)
            {
                t.IsActive = (t.Id == document.Id);
            }
        }
        else
        {
            foreach (var t in OpenTabs)
            {
                t.IsActive = false;
            }
            var newTab = CreateTab(document, isActive: true);
            OpenTabs.Add(newTab);
        }
    }

    public async Task UpdateActiveScriptAsync(ScriptDocumentItem script)
    {
        var existing = OpenTabs.FirstOrDefault(t => t.Id == script.Id);
        if (existing != null)
        {
            await SwitchToTabAsync(existing);
            return;
        }

        var newTab = CreateTab(script, isActive: false);
        OpenTabs.Add(newTab);
        await SwitchToTabAsync(newTab);
    }
}
