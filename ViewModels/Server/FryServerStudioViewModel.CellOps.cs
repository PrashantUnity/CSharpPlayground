using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.Server;

public partial class FryServerStudioViewModel
{
    [RelayCommand]
    public void AddEndpointCell()
    {
        var item = new FryServerCellItem
        {
            Type = FryServerCellType.Endpoint,
            Title = "New Endpoint",
            Method = "GET",
            Route = $"/endpoint_{Cells.Count + 1}",
            Source = "return Ok(new { message = \"Hello from new endpoint!\" });",
            TestHarness = new FryServerTestHarnessItem()
        };

        AddCellInternal(item);
    }

    [RelayCommand]
    public void AddMiddlewareCell()
    {
        var item = new FryServerCellItem
        {
            Type = FryServerCellType.Middleware,
            Title = "Auth / Header Interceptor",
            Source = "// Inspect headers or validate auth\nreturn Context.Next();"
        };

        AddCellInternal(item);
    }

    [RelayCommand]
    public void AddStartupCell()
    {
        var item = new FryServerCellItem
        {
            Type = FryServerCellType.Startup,
            Title = "Shared State Seed",
            Source = "// Seed in-memory tables or state\nState[\"initializedAt\"] = DateTime.UtcNow;"
        };

        AddCellInternal(item);
    }

    [RelayCommand]
    public void InsertEndpointCellAfter(FryServerCellViewModel? cell)
    {
        var item = new FryServerCellItem
        {
            Type = FryServerCellType.Endpoint,
            Title = "New Endpoint",
            Method = "GET",
            Route = $"/endpoint_{Cells.Count + 1}",
            Source = "return Ok(new { message = \"Hello from new endpoint!\" });",
            TestHarness = new FryServerTestHarnessItem()
        };

        var index = cell != null ? Cells.IndexOf(cell) : Cells.Count - 1;
        if (index >= 0 && index < Cells.Count)
        {
            var vm = new FryServerCellViewModel(item, _engine);
            Cells.Insert(index + 1, vm);
            Document.Cells.Insert(index + 1, item);
            SelectCell(vm);
        }
        else
        {
            AddCellInternal(item);
        }
    }

    [RelayCommand]
    public void InsertMiddlewareCellAfter(FryServerCellViewModel? cell)
    {
        var item = new FryServerCellItem
        {
            Type = FryServerCellType.Middleware,
            Title = "Middleware",
            Source = "// Inspect headers or validate auth\nreturn Context.Next();"
        };

        var index = cell != null ? Cells.IndexOf(cell) : Cells.Count - 1;
        if (index >= 0 && index < Cells.Count)
        {
            var vm = new FryServerCellViewModel(item, _engine);
            Cells.Insert(index + 1, vm);
            Document.Cells.Insert(index + 1, item);
            SelectCell(vm);
        }
        else
        {
            AddCellInternal(item);
        }
    }

    [RelayCommand]
    public void AddScenarioCell()
    {
        var item = new FryServerCellItem
        {
            Type = FryServerCellType.Scenario,
            Title = "Integration Test Scenario",
            Source = "// Multi-step assertion\nreturn Ok(new { test = \"Passed\" });"
        };

        AddCellInternal(item);
    }

    [RelayCommand]
    public void AddMarkdownCell()
    {
        var item = new FryServerCellItem
        {
            Type = FryServerCellType.Markdown,
            Title = "Documentation",
            Source = "### Endpoint Notes\nDocument your API contract here."
        };

        AddCellInternal(item);
    }

    [RelayCommand]
    public void RemoveCell(FryServerCellViewModel? cell)
    {
        if (cell == null) return;

        var index = Cells.IndexOf(cell);
        Cells.Remove(cell);
        Document.Cells.Remove(cell.Model);

        if (ActiveCell == cell)
        {
            ActiveCell = Cells.ElementAtOrDefault(index) ?? Cells.LastOrDefault();
        }
        NotifyCellCounts();
        _engine.RefreshRoutes();
    }

    [RelayCommand]
    public void DuplicateCell(FryServerCellViewModel? cell)
    {
        if (cell == null) return;

        var duplicateItem = new FryServerCellItem
        {
            Type = cell.Type,
            Title = $"{cell.Title} (Copy)",
            Method = cell.Method,
            Route = $"{cell.Route}_copy",
            Description = cell.Description,
            Source = cell.Source,
            TestHarness = cell.Model.TestHarness.Clone(),
            Enabled = cell.Enabled
        };

        var index = Cells.IndexOf(cell);
        var vm = new FryServerCellViewModel(duplicateItem, _engine);
        Cells.Insert(index + 1, vm);
        Document.Cells.Insert(index + 1, duplicateItem);
        SelectCell(vm);
        NotifyCellCounts();
        _engine.RefreshRoutes();
    }

    [RelayCommand]
    public void MoveCellUp(FryServerCellViewModel? cell)
    {
        if (cell == null) return;
        var idx = Cells.IndexOf(cell);
        if (idx > 0)
        {
            Cells.Move(idx, idx - 1);
            Document.Cells.RemoveAt(idx);
            Document.Cells.Insert(idx - 1, cell.Model);
            _engine.RefreshRoutes();
        }
    }

    [RelayCommand]
    public void MoveCellDown(FryServerCellViewModel? cell)
    {
        if (cell == null) return;
        var idx = Cells.IndexOf(cell);
        if (idx < Cells.Count - 1)
        {
            Cells.Move(idx, idx + 1);
            Document.Cells.RemoveAt(idx);
            Document.Cells.Insert(idx + 1, cell.Model);
            _engine.RefreshRoutes();
        }
    }

    [RelayCommand]
    public async Task InvalidateCellAsync(FryServerCellViewModel? cell)
    {
        if (cell != null && IsServerRunning)
        {
            await _engine.InvalidateCellCompilationAsync(cell.Model).ConfigureAwait(false);
        }
    }

    private void AddCellInternal(FryServerCellItem item)
    {
        Document.Cells.Add(item);
        var vm = new FryServerCellViewModel(item, _engine);
        Cells.Add(vm);
        SelectCell(vm);
        NotifyCellCounts();
        _engine.RefreshRoutes();
    }
}
