using PdfEditorApp.Plugins.CSharpEditor.Models.Server;
using PdfEditorApp.Plugins.CSharpEditor.Services.Server;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Server;
using Xunit;
using CSharpManagerViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Hub.CSharpManagerViewModel;
using CSharpStudioHostViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Common.CSharpStudioHostViewModel;

namespace CSharpEditorPlugin.Tests.Server;

public class FryServerViewModelTests
{
    [Fact]
    public void StudioViewModel_InitializesWithDefaultCellsAndConfiguration()
    {
        var studio = new FryServerStudioViewModel();

        Assert.NotEmpty(studio.Cells);
        Assert.NotNull(studio.ActiveCell);
        Assert.Equal("API Server", studio.DocumentTitle);
        Assert.Equal(5000, studio.PortInput);
        Assert.Equal("/api", studio.ApiPrefixInput);
        Assert.True(studio.CorsEnabled);
    }

    [Fact]
    public void StudioViewModel_AddCells_InsertsPolymorphicTypes()
    {
        var studio = new FryServerStudioViewModel();
        var initialCount = studio.Cells.Count;

        studio.AddEndpointCell();
        Assert.Equal(initialCount + 1, studio.Cells.Count);
        Assert.True(studio.Cells.Last().IsEndpoint);

        studio.AddMiddlewareCell();
        Assert.Equal(initialCount + 2, studio.Cells.Count);
        Assert.True(studio.Cells.Last().IsMiddleware);

        studio.AddStartupCell();
        Assert.Equal(initialCount + 3, studio.Cells.Count);
        Assert.True(studio.Cells.Last().IsStartup);

        studio.AddScenarioCell();
        Assert.Equal(initialCount + 4, studio.Cells.Count);
        Assert.True(studio.Cells.Last().IsScenario);

        studio.AddMarkdownCell();
        Assert.Equal(initialCount + 5, studio.Cells.Count);
        Assert.True(studio.Cells.Last().IsMarkdown);
    }

    [Fact]
    public void StudioViewModel_DuplicateAndRemoveCell_UpdatesCollections()
    {
        var studio = new FryServerStudioViewModel();
        var original = studio.Cells.First();

        studio.DuplicateCell(original);
        Assert.Equal(2, studio.Cells.Count);
        Assert.Contains("(Copy)", studio.Cells[1].Title);

        studio.RemoveCell(studio.Cells[1]);
        Assert.Single(studio.Cells);
    }

    [Fact]
    public void StudioViewModel_ApplySuggestedPort_UpdatesPortInput()
    {
        var studio = new FryServerStudioViewModel();
        studio.SuggestedPort = 5005;

        studio.ApplySuggestedPort();

        Assert.Equal(5005, studio.PortInput);
    }

    [Fact]
    public async Task CellViewModel_SendTestRequest_ExecutesLoopbackAndSetsResponse()
    {
        var engine = new FryHttpListenerServerEngine();
        var cellItem = new FryServerCellItem
        {
            Type = FryServerCellType.Endpoint,
            Method = "GET",
            Route = "/calc/{x}",
            Source = @"
                var x = Context.ParamValue<int>(""x"");
                return Ok(new { doubled = x * 2 });
            ",
            TestHarness = new FryServerTestHarnessItem
            {
                PathParams = new() { ["x"] = "21" }
            }
        };

        var cellVm = new FryServerCellViewModel(cellItem, engine);

        await cellVm.SendTestRequestAsync();

        Assert.Equal(200, cellVm.LastStatusCode);
        Assert.True(cellVm.HasOutput);
        Assert.False(cellVm.HasError);
        Assert.Contains("42", cellVm.LastResponseText);
    }

    [Fact]
    public void StudioViewModel_LoadDocument_RefreshesConfigurationAndCells()
    {
        var studio = new FryServerStudioViewModel();
        var newDoc = new FryServerDocumentItem
        {
            Title = "Updated API Service",
            ServerConfig = new FryServerConfiguration
            {
                Host = "127.0.0.1",
                Port = 8080,
                ApiPrefix = "/v1",
                EnableCors = false
            },
            Cells = new()
            {
                new FryServerCellItem
                {
                    Type = FryServerCellType.Endpoint,
                    Title = "Health Check",
                    Method = "GET",
                    Route = "/health",
                    Source = "return Ok(new { status = \"live\" });"
                }
            }
        };

        studio.LoadDocument(newDoc, "/tmp/updated.fryserver");

        Assert.Equal("Updated API Service", studio.DocumentTitle);
        Assert.Equal(8080, studio.PortInput);
        Assert.Equal("127.0.0.1", studio.HostInput);
        Assert.Equal("/v1", studio.ApiPrefixInput);
        Assert.False(studio.CorsEnabled);
        Assert.Single(studio.Cells);
        Assert.Equal("Health Check", studio.Cells[0].Title);
    }

    [Fact]
    public async Task StudioViewModel_SaveDocument_InvokesStorageService()
    {
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FryServerSaveTest_" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(tempDir);
        try
        {
            var storage = new LocalScriptStorageService(tempDir);
            var doc = new FryServerDocumentItem
            {
                Title = "Saveable API",
                ServerConfig = new FryServerConfiguration { Port = 9000 }
            };

            var studio = new FryServerStudioViewModel(document: doc, storageService: storage);
            await studio.SaveDocumentAsync();

            var loaded = await storage.LoadServerDocumentAsync(doc.Id);
            Assert.NotNull(loaded);
            Assert.Equal("Saveable API", loaded.Title);
            Assert.Equal(9000, loaded.ServerConfig.Port);
        }
        finally
        {
            if (System.IO.Directory.Exists(tempDir))
            {
                System.IO.Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ManagerViewModel_CreateAndOpenServer_NavigatesSuccessfully()
    {
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FryServerManagerTest_" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(tempDir);
        try
        {
            var storage = new LocalScriptStorageService(tempDir);
            FryServerDocumentItem? openedServer = null;

            var manager = new CSharpManagerViewModel(
                storage,
                openScriptAction: _ => { },
                openNotebookAction: _ => { },
                openServerAction: s => openedServer = s);
            await manager.LoadWorkspaceItemsAsync();

            await manager.CreateNewServerAsync();
            Assert.True(manager.IsCreatingServer);
            Assert.Equal("New API Server", manager.CreatePromptTitle);

            manager.NewItemName = "Orders Microservice";
            await manager.ConfirmCreateAsync();

            Assert.NotNull(openedServer);
            Assert.Equal("Orders Microservice", openedServer.Title);
            Assert.Equal(1, manager.TotalServers);

            // Open existing server item
            var serverSummary = manager.AllItems.First(i => i.IsServer);
            openedServer = null;
            await manager.OpenItemAsync(serverSummary);

            Assert.NotNull(openedServer);
            Assert.Equal("Orders Microservice", openedServer.Title);
        }
        finally
        {
            if (System.IO.Directory.Exists(tempDir))
            {
                System.IO.Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void HostViewModel_NavigateToServerStudio_SwitchesCurrentPage()
    {
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FryServerHostTest_" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(tempDir);
        try
        {
            var storage = new LocalScriptStorageService(tempDir);
            var host = new CSharpStudioHostViewModel(storageService: storage);

            var serverDoc = new FryServerDocumentItem
            {
                Title = "Payment Gateway Simulator"
            };

            host.NavigateToServerStudio(serverDoc);

            Assert.False((bool)host.IsOnManagerPage);
            Assert.Same(host.ServerStudioViewModel, host.CurrentPage);
            Assert.Equal((string?)"Payment Gateway Simulator", (string?)host.ActiveDocumentTitle);
        }
        finally
        {
            if (System.IO.Directory.Exists(tempDir))
            {
                System.IO.Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ServerRegistry_RegisterAndStopServer_UpdatesStateAndNotifies()
    {
        var registry = new FryServerRegistry();
        var engine = new FryHttpListenerServerEngine();
        var doc = new FryServerDocumentItem
        {
            Id = "server-test-1",
            Title = "Inventory Service",
            ServerConfig = new() { Port = 5991 }
        };

        var item = new FryRunningServerItem
        {
            ServerId = doc.Id,
            DocumentTitle = doc.Title,
            BoundPort = 5991,
            BaseUrl = "http://localhost:5991",
            Engine = engine,
            Document = doc
        };

        var eventFired = false;
        registry.RunningServersChanged += () => eventFired = true;

        registry.RegisterServer(item);

        Assert.True(eventFired);
        Assert.Equal(1, registry.RunningCount);
        Assert.Same(item, registry.GetServer(doc.Id));

        // Stop server via registry
        await registry.StopServerAsync(doc.Id);

        Assert.Equal(0, registry.RunningCount);
        Assert.Null(registry.GetServer(doc.Id));
    }

    [Fact]
    public async Task ServerRegistry_StopAll_StopsAllRegisteredServers()
    {
        var registry = new FryServerRegistry();
        var engine1 = new FryHttpListenerServerEngine();
        var engine2 = new FryHttpListenerServerEngine();

        var item1 = new FryRunningServerItem
        {
            ServerId = "s1",
            DocumentTitle = "Server 1",
            BoundPort = 5992,
            BaseUrl = "http://localhost:5992",
            Engine = engine1,
            Document = new() { Id = "s1", Title = "Server 1" }
        };
        var item2 = new FryRunningServerItem
        {
            ServerId = "s2",
            DocumentTitle = "Server 2",
            BoundPort = 5993,
            BaseUrl = "http://localhost:5993",
            Engine = engine2,
            Document = new() { Id = "s2", Title = "Server 2" }
        };

        registry.RegisterServer(item1);
        registry.RegisterServer(item2);
        Assert.Equal(2, registry.RunningCount);

        await registry.StopAllAsync();
        Assert.Equal(0, registry.RunningCount);
    }

    [Fact]
    public void StudioViewModel_RunningServers_TracksActiveServersAndSwitches()
    {
        var registry = new FryServerRegistry();
        var engine = new FryHttpListenerServerEngine();
        var docA = new FryServerDocumentItem { Id = "srv-a", Title = "Server A" };
        var docB = new FryServerDocumentItem { Id = "srv-b", Title = "Server B" };

        var studio = new FryServerStudioViewModel(document: docA, registry: registry);

        var runningB = new FryRunningServerItem
        {
            ServerId = docB.Id,
            DocumentTitle = docB.Title,
            BoundPort = 5880,
            BaseUrl = "http://localhost:5880",
            Engine = engine,
            Document = docB
        };
        registry.RegisterServer(runningB);
        studio.SyncRunningServers();

        Assert.True(studio.HasRunningServers);
        Assert.Equal(1, studio.RunningServerCount);

        // Switch to running server B
        studio.SwitchToRunningServer(runningB);

        Assert.Equal("Server B", studio.DocumentTitle);
        Assert.Equal(docB.Id, studio.Document.Id);
    }

    [Fact]
    public void StudioViewModel_ToggleSideBar_CollapsesToZeroAndRestoresWidth()
    {
        var studio = new FryServerStudioViewModel();

        Assert.True(studio.IsSideBarVisible);
        Assert.True(studio.SideBarGridLength.Value > 0);

        // Toggle collapsed
        studio.ToggleSideBar();
        Assert.False(studio.IsSideBarVisible);
        Assert.Equal(0, studio.SideBarGridLength.Value);

        // Toggle restored
        studio.ToggleSideBar();
        Assert.True(studio.IsSideBarVisible);
        Assert.Equal(270, studio.SideBarGridLength.Value);
    }

    [Fact]
    public void StudioViewModel_ToggleBottomPanel_CollapsesToZeroAndRestoresHeight()
    {
        var studio = new FryServerStudioViewModel();

        Assert.True(studio.IsBottomPanelVisible);
        Assert.True(studio.BottomDeckGridLength.Value > 0);

        // Toggle collapsed
        studio.ToggleBottomPanel();
        Assert.False(studio.IsBottomPanelVisible);
        Assert.Equal(0, studio.BottomDeckGridLength.Value);

        // Toggle restored
        studio.ToggleBottomPanel();
        Assert.True(studio.IsBottomPanelVisible);
        Assert.Equal(180, studio.BottomDeckGridLength.Value);
    }
}
