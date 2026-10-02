using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio;

public partial class CSharpCodeStudioViewModel
{
    public ObservableCollection<BreakpointItem> Breakpoints { get; } = new();

    public event Action<IEnumerable<int>>? RequestSyncBreakpoints;

    [RelayCommand]
    public void ToggleBreakpoint(int line)
    {
        if (!SupportsBreakpoints) return;
        var existing = Breakpoints.FirstOrDefault(b => b.LineNumber == line);
        if (existing != null)
        {
            Breakpoints.Remove(existing);
            Script.Breakpoints.Remove(line);
        }
        else
        {
            var bp = new BreakpointItem { LineNumber = line, IsEnabled = true };
            Breakpoints.Add(bp);
            if (!Script.Breakpoints.Contains(line))
            {
                Script.Breakpoints.Add(line);
            }
        }

        var sorted = Breakpoints.OrderBy(b => b.LineNumber).ToList();
        Breakpoints.Clear();
        foreach (var b in sorted) Breakpoints.Add(b);

        RequestSyncBreakpoints?.Invoke(Breakpoints.Where(b => b.IsEnabled).Select(b => b.LineNumber));
        _ = _storageService.SaveScriptAsync(Script);

        var currentTab = OpenTabs.FirstOrDefault(t => t.Id == Script.Id);
        var sourcePath = DebugSourcePath(currentTab);
        if (_activeDebugSession != null)
        {
            _ = _activeDebugSession.SetBreakpointsAsync(sourcePath, Breakpoints.ToList()).ContinueWith(_ =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    RequestSyncBreakpoints?.Invoke(Breakpoints.Where(b => b.IsEnabled).Select(b => b.LineNumber));
                });
            });
        }
    }

    [RelayCommand]
    public void RemoveBreakpoint(BreakpointItem? item)
    {
        if (item == null) return;
        Breakpoints.Remove(item);
        Script.Breakpoints.Remove(item.LineNumber);
        RequestSyncBreakpoints?.Invoke(Breakpoints.Where(b => b.IsEnabled).Select(b => b.LineNumber));
        _ = _storageService.SaveScriptAsync(Script);
    }

    [RelayCommand]
    public void ClearAllBreakpoints()
    {
        Breakpoints.Clear();
        Script.Breakpoints.Clear();
        RequestSyncBreakpoints?.Invoke(Array.Empty<int>());
        _ = _storageService.SaveScriptAsync(Script);
    }

    [RelayCommand]
    public void ToggleBreakpointEnabled(BreakpointItem? item)
    {
        if (item == null) return;
        item.IsEnabled = !item.IsEnabled;
    }
}
