using System.Reflection;
using Avalonia.Controls;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Controls.Studio;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Activities;
using PdfEditorApp.Plugins.CSharpEditor.Services.Execution;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Catalogs.Blind75;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using Xunit;
using CSharpCodeStudioViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio.CSharpCodeStudioViewModel;
using CSharpManagerViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Hub.CSharpManagerViewModel;
using CSharpNotebookStudioViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Notebooks.CSharpNotebookStudioViewModel;
using CSharpStudioHostViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Common.CSharpStudioHostViewModel;
using ExplorerItemViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio.Explorer.ExplorerItemViewModel;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// The studio's loading behaviour: pages report work as activities (never a modal of their own), fast work such as a
/// tab switch reports nothing, a later open wins over an earlier one, and the Hub's list refresh never swallows a click.
/// </summary>
public class StudioLoadingOverlayTests
{
    /// <summary>An activity service that remembers what was started, where.</summary>
    private sealed class RecordingActivities : IActivityService
    {
        private readonly ActivityService _inner = new();
        public List<ActivityOptions> Started { get; } = new();
        public TimeProvider Time => _inner.Time;
        public long Version => _inner.Version;

        public IActivity Start(ActivityOptions options, CancellationToken linked = default)
        {
            lock (Started) Started.Add(options);
            return _inner.Start(options, linked);
        }

        public IReadOnlyList<ActivitySnapshot> Snapshot() => _inner.Snapshot();
        public void Cancel(long id) => _inner.Cancel(id);

        public event Action? Changed
        {
            add => _inner.Changed += value;
            remove => _inner.Changed -= value;
        }

        public event Action<ActivitySnapshot, Exception>? Failed
        {
            add => _inner.Failed += value;
            remove => _inner.Failed -= value;
        }
    }

    /// <summary>Storage whose chosen calls wait for a gate, to make "slow" loads without timing tricks.</summary>
    public class GatedStorage : DispatchProxy
    {
        public IScriptStorageService Inner { get; set; } = null!;
        public Func<MethodInfo, object?[]?, Task?> Gate { get; set; } = (_, _) => null;

        public static IScriptStorageService Wrap(IScriptStorageService inner, Func<MethodInfo, object?[]?, Task?> gate)
        {
            var proxy = Create<IScriptStorageService, GatedStorage>();
            var gated = (GatedStorage)(object)proxy;
            gated.Inner = inner;
            gated.Gate = gate;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var gate = Gate(targetMethod!, args);
            if (gate == null || !typeof(Task).IsAssignableFrom(targetMethod!.ReturnType)) return targetMethod!.Invoke(Inner, args);

            // Same task type as the real method, completed once the gate opens.
            var resultType = targetMethod.ReturnType.IsGenericType ? targetMethod.ReturnType.GetGenericArguments()[0] : null;
            var run = typeof(GatedStorage).GetMethod(resultType == null ? nameof(After) : nameof(AfterOf), BindingFlags.NonPublic | BindingFlags.Static)!;
            if (resultType != null) run = run.MakeGenericMethod(resultType);
            return run.Invoke(null, [gate, (Func<object?>)(() => targetMethod.Invoke(Inner, args))]);
        }

        private static async Task After(Task gate, Func<object?> call)
        {
            await gate;
            await (Task)call()!;
        }

        private static async Task<T> AfterOf<T>(Task gate, Func<object?> call)
        {
            await gate;
            return await (Task<T>)call()!;
        }
    }

    private static string NewTempFolder(string purpose)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"{purpose}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void OverlayControl_DefaultsAndBindableProperties()
    {
        var control = new StudioLoadingOverlayControl();
        Assert.False(control.IsLoading);
        Assert.Equal("Loading...", control.LoadingTitle);
        Assert.Equal(string.Empty, control.LoadingSubtitle);
        Assert.False(control.ShowCancel);

        control.IsLoading = true;
        control.LoadingTitle = "Opening folder";
        control.LoadingSubtitle = "big-repo";
        control.ShowLogo = false;
        control.IconKind = MaterialIconKind.Refresh;

        Assert.Equal("Opening folder", control.LoadingTitle);
        Assert.Equal("big-repo", control.LoadingSubtitle);
        Assert.Equal(MaterialIconKind.Refresh, control.IconKind);
    }

    [Fact]
    public void OverlayControl_DoesNotAnimate_WhileItIsNotOnScreen()
    {
        // The old card ran a 16 ms timer on the UI thread whenever IsLoading was set; now nothing runs off screen.
        var overlay = new StudioLoadingOverlayControl { IsLoading = true };
        Assert.False(overlay.IsAnimationRunning);
    }

    [Fact]
    public void OverlayControl_StepAnimation_PosesTheRings()
    {
        var overlay = new StudioLoadingOverlayControl { IsLoading = true };
        var spinnerBorder = overlay.FindControl<Avalonia.Controls.Border>("SpinnerRingBorder");
        Assert.NotNull(spinnerBorder);

        overlay.StepAnimation(TimeSpan.FromSeconds(0.3));
        var rotate = spinnerBorder.RenderTransform as Avalonia.Media.RotateTransform;
        Assert.NotNull(rotate);
        Assert.True(rotate.Angle > 0, $"Expected Angle > 0, got {rotate.Angle}");
    }

    [Fact]
    public async Task CodeStudio_TabSwitch_ReportsNoActivity_ButOpeningAFileDoes()
    {
        var dir = NewTempFolder("CodeLoadingTest");
        try
        {
            using var storage = new LocalScriptStorageService(dir);
            var first = new ScriptDocumentItem { Title = "Script 1", Code = "// one" };
            var second = new ScriptDocumentItem { Title = "Script 2", Code = "// two" };
            await storage.SaveScriptAsync(first);
            await storage.SaveScriptAsync(second);
            var activities = new RecordingActivities();
            var studio = new CSharpCodeStudioViewModel(first, storage, new RoslynCompilerService(), new ScriptExecutionEngine(),
                backToHubAction: () => { }, backToHomeAction: () => { }, activities: activities);

            await studio.SwitchToScriptAsync(new ExplorerItemViewModel { Name = "Script 2.frycs", DocumentId = second.Id, FileExtension = ".frycs" });
            var open = Assert.Single(activities.Started);
            Assert.Equal("Opening Script 2.frycs", open.Title);
            Assert.Equal(ActivityLocation.Editor, open.Location);
            Assert.False(open.Blocking);
            Assert.Equal(second.Id, studio.Script.Id);

            activities.Started.Clear();
            var firstTab = studio.OpenTabs.First(t => t.Id == first.Id);
            await studio.SwitchToTabAsync(firstTab);
            Assert.Equal(first.Id, studio.Script.Id);
            Assert.Empty(activities.Started);
            Assert.Empty(activities.Snapshot());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task CodeStudio_ALaterOpen_WinsOverASlowerEarlierOne()
    {
        var dir = NewTempFolder("CodeLatestWins");
        try
        {
            using var real = new LocalScriptStorageService(dir);
            var start = new ScriptDocumentItem { Title = "Start", Code = "// start" };
            var slow = new ScriptDocumentItem { Title = "Slow", Code = "// slow" };
            var fast = new ScriptDocumentItem { Title = "Fast", Code = "// fast" };
            foreach (var s in new[] { start, slow, fast }) await real.SaveScriptAsync(s);

            var slowGate = new TaskCompletionSource();
            var storage = GatedStorage.Wrap(real, (method, args) =>
                method.Name == nameof(IScriptStorageService.LoadScriptAsync) && Equals(args?[0], slow.Id) ? slowGate.Task : null);
            var studio = new CSharpCodeStudioViewModel(start, storage, new RoslynCompilerService(), new ScriptExecutionEngine(),
                backToHubAction: () => { }, backToHomeAction: () => { });

            var openSlow = studio.SwitchToScriptAsync(new ExplorerItemViewModel { Name = "Slow.frycs", DocumentId = slow.Id, FileExtension = ".frycs" });
            await studio.SwitchToScriptAsync(new ExplorerItemViewModel { Name = "Fast.frycs", DocumentId = fast.Id, FileExtension = ".frycs" });
            Assert.Equal(fast.Id, studio.Script.Id);

            slowGate.SetResult();
            await openSlow;

            // The slow file finished last, but the user had moved on: it must not take the editor.
            Assert.Equal(fast.Id, studio.Script.Id);
            Assert.DoesNotContain(studio.OpenTabs, t => t.Id == slow.Id);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task NotebookStudio_OpeningANotebook_ReportsANotebookActivity()
    {
        var dir = NewTempFolder("NotebookLoadingTest");
        try
        {
            using var storage = new LocalScriptStorageService(dir);
            var activities = new RecordingActivities();
            var studio = new CSharpNotebookStudioViewModel(new NotebookDocumentItem { Title = "Initial Notebook" }, storage,
                new RoslynCompilerService(), new ScriptExecutionEngine(), backToHubAction: () => { }, backToHomeAction: () => { },
                activities: activities);

            await studio.OpenDocumentAsync(new ExplorerItemViewModel { Name = "MachineLearning.frynb", DocumentId = "ml_doc_1", FileExtension = ".frynb" });

            var open = Assert.Single(activities.Started);
            Assert.Equal("Opening MachineLearning.frynb", open.Title);
            Assert.Equal(ActivityLocation.Notebook, open.Location);
            Assert.Empty(activities.Snapshot());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Hub_CreatingADocument_WorksWhileTheListIsRefreshing()
    {
        // The Hub used to guard "create" on the same flag its list refresh set, so a click during a refresh did nothing.
        var dir = NewTempFolder("HubCreateDuringRefresh");
        try
        {
            using var real = new LocalScriptStorageService(dir);
            var listGate = new TaskCompletionSource();
            var storage = GatedStorage.Wrap(real, (method, _) =>
                method.Name == nameof(IScriptStorageService.LoadWorkspaceSummariesAsync) ? listGate.Task : null);
            ScriptDocumentItem? opened = null;
            var hub = new CSharpManagerViewModel(storage, openScriptAction: s => opened = s);

            Assert.True(hub.IsRefreshingList);
            await hub.CreateNewScriptAsync();
            var create = hub.ConfirmCreateAsync();

            // The document opens before the list has caught up.
            Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref opened) != null, TimeSpan.FromSeconds(10)), "the new script was never opened");
            listGate.SetResult();
            await create;
            Assert.False(hub.IsRefreshingList);
            Assert.Contains(hub.AllItems, i => i.Id == opened!.Id);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Host_OwnsOneActivityPresenter_AndALaterNavigationWinsOverAnOpenWaitingForTheEngine()
    {
        var host = new CSharpStudioHostViewModel(blindProgress: new LocalBlindProgressService());
        Assert.Same(host.Activities, host.Activity.Service);

        // Opening a script right after start-up waits for the engine; going to the docs meanwhile must not be undone.
        var open = host.OpenInCodeStudioAsync(new ScriptDocumentItem { Title = "Early" });
        host.NavigateToDocs();
        await open.WaitAsync(TimeSpan.FromMinutes(2));

        Assert.Same(host.DocsViewModel, host.CurrentPage);
        host.Dispose();
    }
}
