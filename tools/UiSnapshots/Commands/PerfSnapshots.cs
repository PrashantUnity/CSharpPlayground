using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using PdfEditorApp.Plugins.CSharpEditor.Views;

namespace PdfEditorApp.Plugins.CSharpEditor.Tools.UiSnapshots;

/// <summary>
/// <c>perf</c>: times the real studio (host view, all six pages) over a generated workspace: the first visit of each page,
/// warm page switches, tab switches and file opens, plus how many times the workspace was re-scanned and how much memory
/// the visits leave behind. Numbers are milliseconds on the UI thread (view built, bindings run, layout done, one frame
/// rendered), so they are comparable between runs on one machine, not absolute.
/// </summary>
internal static class PerfSnapshots
{
    private readonly record struct Sample(double ToLayout, double ToFrame);

    public static void Run(Options options)
    {
        int files = Math.Max(5, options.Int("files", 200));
        int rounds = Math.Max(3, options.Int("rounds", 6));
        int notebooks = Math.Max(2, files / 20);

        var languages = new StudioLanguageServices(Snapshot.TempFolder("perf-languages"));
        var storage = new LocalScriptStorageService(Snapshot.TempFolder("perf-workspace"), languages.Registry);
        Console.WriteLine($"Workspace: {files} scripts, {notebooks} notebooks. Rounds: {rounds}.");
        for (int i = 0; i < files; i++) Pump(storage.CreateNewScriptAsync($"Script {i:D4}"));
        for (int i = 0; i < notebooks; i++) Pump(storage.CreateNewNotebookAsync($"Notebook {i:D3}"));

        var summaries = Pump(storage.LoadWorkspaceSummariesAsync());
        var scripts = summaries.Where(s => s.IsScript).Take(8).Select(s => Pump(storage.LoadScriptAsync(s.Id))!).ToList();
        var notebook = Pump(storage.LoadNotebookAsync(summaries.First(s => s.IsNotebook).Id))!;

        var clock = Stopwatch.StartNew();
        var host = new CSharpStudioHostViewModel(
            blindProgress: new LocalBlindProgressService(Snapshot.TempFolder("perf-blind")),
            languages: languages,
            storageService: storage);
        Console.WriteLine($"Studio view model created (runs on the UI thread when the plugin opens): {clock.Elapsed.TotalMilliseconds:F0} ms");

        clock.Restart();
        var view = new CSharpStudioHostView { DataContext = host };
        var window = new Window { Width = 1400, Height = 900, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        using (window.CaptureRenderedFrame()) { }
        Console.WriteLine($"Studio window shown (Hub built, laid out, one frame): {clock.Elapsed.TotalMilliseconds:F0} ms");

        clock.Restart();
        if (!Snapshot.WaitFor(() => host.CodeStudioViewModel != null && host.NotebookStudioViewModel != null && !host.ManagerViewModel.IsLoading, TimeSpan.FromMinutes(3)))
        {
            throw new InvalidOperationException("The studio never finished starting up.");
        }
        Console.WriteLine($"Engine and Hub data ready: {clock.Elapsed.TotalMilliseconds:F0} ms (background)");

        var pages = new (string Name, Action Go, Func<bool> Arrived)[]
        {
            ("Docs", () => host.NavigateToDocs(), () => ReferenceEquals(host.CurrentPage, host.DocsViewModel)),
            ("Blind 75", () => host.NavigateToBlindProblems(), () => ReferenceEquals(host.CurrentPage, host.BlindProblemsViewModel)),
            ("Settings", () => host.NavigateToSettings(), () => ReferenceEquals(host.CurrentPage, host.SettingsViewModel)),
            ("Code Studio", () => host.NavigateToCodeStudio(scripts[0]), () => ReferenceEquals(host.CurrentPage, host.CodeStudioViewModel)),
            ("Notebook", () => host.NavigateToNotebookStudio(notebook), () => ReferenceEquals(host.CurrentPage, host.NotebookStudioViewModel)),
            ("Hub", () => host.NavigateToManager(), () => ReferenceEquals(host.CurrentPage, host.ManagerViewModel)),
        };

        var samples = pages.ToDictionary(p => p.Name, _ => new List<Sample>());
        long memoryAfterFirstRound = 0;
        int scansBefore = storage.WorkspaceScanCount;
        for (int round = 0; round < rounds; round++)
        {
            foreach (var page in pages)
            {
                samples[page.Name].Add(Navigate(window, page.Go, page.Arrived));
                // --shots: what the page looks like in the studio host, last round only (kept out of the timing).
                if (options.Flag("shots") && round == rounds - 1) Snapshot.Save(window, $"host-{page.Name.ToLowerInvariant().Replace(' ', '-')}");
                // The Hub reloads on return; let that finish so it does not bleed into the next page's timing.
                if (page.Name == "Hub") Snapshot.WaitFor(() => !host.ManagerViewModel.IsLoading, TimeSpan.FromMinutes(1));
            }

            if (round == 0) memoryAfterFirstRound = SettledMemory();
        }

        long memoryAtEnd = SettledMemory();
        int scansDuringPages = storage.WorkspaceScanCount - scansBefore;

        Console.WriteLine();
        Console.WriteLine($"{"page",-14}{"first visit",14}{"warm switch",14}{"warm + frame",14}   (ms)");
        foreach (var page in pages)
        {
            var list = samples[page.Name];
            var warm = list.Skip(1).ToList();
            Console.WriteLine($"{page.Name,-14}{list[0].ToLayout,14:F1}{Median(warm.Select(s => s.ToLayout)),14:F1}{Median(warm.Select(s => s.ToFrame)),14:F1}");
        }

        // Tabs and file opens in the Code Studio.
        Navigate(window, () => host.NavigateToCodeStudio(scripts[0]), () => ReferenceEquals(host.CurrentPage, host.CodeStudioViewModel));
        var codeVm = host.CodeStudioViewModel!;
        var opens = new List<Sample>();
        foreach (var script in scripts.Skip(1))
        {
            opens.Add(Timed(window, () => Pump(codeVm.UpdateActiveScriptAsync(script))));
        }

        var switches = new List<Sample>();
        int scansBeforeSwitches = storage.WorkspaceScanCount;
        for (int i = 0; i < rounds * codeVm.OpenTabs.Count; i++)
        {
            var tab = codeVm.OpenTabs[i % codeVm.OpenTabs.Count];
            switches.Add(Timed(window, () => Pump(codeVm.SwitchToTabAsync(tab))));
        }

        int scansDuringSwitches = storage.WorkspaceScanCount - scansBeforeSwitches;

        Console.WriteLine();
        Console.WriteLine($"{"file open",-14}{Median(opens.Select(s => s.ToLayout)),14:F1}{"",14}{Median(opens.Select(s => s.ToFrame)),14:F1}   ({opens.Count} opens)");
        Console.WriteLine($"{"tab switch",-14}{Median(switches.Select(s => s.ToLayout)),14:F1}{"",14}{Median(switches.Select(s => s.ToFrame)),14:F1}   ({switches.Count} switches, {codeVm.OpenTabs.Count} tabs)");
        Console.WriteLine();
        Console.WriteLine($"Workspace scans: {scansDuringPages} during {rounds} rounds of 6 page switches, {scansDuringSwitches} during {switches.Count} tab switches");
        Console.WriteLine($"Memory after round 1: {memoryAfterFirstRound / 1048576.0:F1} MB, after round {rounds}: {memoryAtEnd / 1048576.0:F1} MB " +
                          $"(+{(memoryAtEnd - memoryAfterFirstRound) / 1048576.0 / (rounds - 1):F2} MB per round)");
        // --big-kb n: one large source file among the open tabs, to see what a big document costs to open and switch to.
        if (options.Int("big-kb", 0) is > 0 and var bigKb)
        {
            var big = Pump(storage.CreateNewScriptAsync("Big file"));
            big.Code = BigCode(bigKb);
            Pump(storage.SaveScriptAsync(big));
            var bigOpen = Timed(window, () => Pump(codeVm.UpdateActiveScriptAsync(big)));
            var bigTab = codeVm.OpenTabs.First(t => t.Id == big.Id);
            var smallTab = codeVm.OpenTabs.First(t => t.Id != big.Id);
            var toBig = new List<Sample>();
            var fromBig = new List<Sample>();
            for (int i = 0; i < 5; i++)
            {
                fromBig.Add(Timed(window, () => Pump(codeVm.SwitchToTabAsync(smallTab))));
                toBig.Add(Timed(window, () => Pump(codeVm.SwitchToTabAsync(bigTab))));
            }

            // One keystroke at a time into the big document, the way typing reaches the studio (TextChanged -> Code).
            Pump(codeVm.SwitchToTabAsync(bigTab));
            var editor = (view.Pages.ViewFor(codeVm) as CSharpCodeStudioView)!.FindControl<AvaloniaEdit.TextEditor>("Editor")!;
            var typing = new List<double>();
            var insertOnly = new List<double>();
            int gen2Before = GC.CollectionCount(2);
            for (int i = 0; i < 10; i++)
            {
                var clock2 = Stopwatch.StartNew();
                editor.Document.Insert(editor.Document.TextLength - 2, "x");
                insertOnly.Add(clock2.Elapsed.TotalMilliseconds);
                Dispatcher.UIThread.RunJobs();
                typing.Add(clock2.Elapsed.TotalMilliseconds);
            }

            if (options.Flag("typing-experiments"))
            {
                // Where the queued work after a keystroke sits: run the jobs band by band, highest priority first.
                var bands = new (string Name, DispatcherPriority Priority)[]
                {
                    ("Render and above", DispatcherPriority.Render), ("Input", DispatcherPriority.Input),
                    ("Background", DispatcherPriority.Background), ("everything else", DispatcherPriority.SystemIdle)
                };
                var bandTimes = bands.ToDictionary(b => b.Name, _ => new List<double>());
                for (int i = 0; i < 10; i++)
                {
                    editor.Document.Insert(editor.Document.TextLength - 2, "x");
                    foreach (var band in bands)
                    {
                        var clock3 = Stopwatch.StartNew();
                        Dispatcher.UIThread.RunJobs(band.Priority);
                        bandTimes[band.Name].Add(clock3.Elapsed.TotalMilliseconds);
                    }
                }

                Console.WriteLine("  experiment: queued jobs after a keystroke, by priority: " +
                                  string.Join(", ", bands.Select(b => $"{b.Name} {Median(bandTimes[b.Name]):F1} ms")));

                double WithJobs() => Median(Repeat(10, () =>
                {
                    editor.Document.Insert(editor.Document.TextLength - 2, "x");
                    Dispatcher.UIThread.RunJobs();
                }));
                Console.WriteLine($"  experiment: keystroke + jobs, as is                    {WithJobs():F1} ms");
                codeVm.IsSideBarVisible = false;
                Dispatcher.UIThread.RunJobs();
                Console.WriteLine($"  experiment: keystroke + jobs, sidebar hidden           {WithJobs():F1} ms");
                codeVm.IsBottomDeckExpanded = false;
                Dispatcher.UIThread.RunJobs();
                Console.WriteLine($"  experiment: keystroke + jobs, sidebar and deck hidden  {WithJobs():F1} ms");

                double Keystrokes() => Median(Repeat(10, () => editor.Document.Insert(editor.Document.TextLength - 2, "x")));
                Console.WriteLine($"  experiment: typing as is                       {Keystrokes():F1} ms");
                var highlighting = editor.SyntaxHighlighting;
                editor.SyntaxHighlighting = null;
                Console.WriteLine($"  experiment: without syntax highlighting        {Keystrokes():F1} ms");
                if (editor.TextArea.GetService(typeof(AvaloniaEdit.Folding.FoldingManager)) is AvaloniaEdit.Folding.FoldingManager folding)
                {
                    AvaloniaEdit.Folding.FoldingManager.Uninstall(folding);
                    Console.WriteLine($"  experiment: + without foldings                 {Keystrokes():F1} ms");
                }

                editor.TextArea.TextView.LineTransformers.Clear();
                Console.WriteLine($"  experiment: + without line transformers        {Keystrokes():F1} ms ({editor.TextArea.TextView.BackgroundRenderers.Count} background renderers)");
                editor.TextArea.TextView.BackgroundRenderers.Clear();
                Console.WriteLine($"  experiment: + without background renderers     {Keystrokes():F1} ms");
                editor.SyntaxHighlighting = highlighting;

                var text = editor.Text;
                int unique = 0;
                Console.WriteLine($"  experiment: setting the view model's Code alone {Median(Repeat(10, () => codeVm.Code = text + new string('y', ++unique))):F1} ms");
                Console.WriteLine($"  experiment: RefreshDocumentNuGetPackages alone  {Median(Repeat(10, () => codeVm.RefreshDocumentNuGetPackages())):F1} ms");
                Console.WriteLine($"  experiment: ScriptDocumentItem.Code set alone   {Median(Repeat(10, () => codeVm.Script.Code = text + new string('z', ++unique))):F1} ms");
                var bare = new AvaloniaEdit.Document.TextDocument(text);
                Console.WriteLine($"  experiment: a bare TextDocument, no editor      {Median(Repeat(10, () => bare.Insert(bare.TextLength - 2, "x"))):F1} ms");
            }

            var textCopy = Repeat(10, () => editor.Text.Length);
            Console.WriteLine();
            Console.WriteLine($"  full garbage collections during those {typing.Count} keystrokes: {GC.CollectionCount(2) - gen2Before}");
            Console.WriteLine($"  running the queued UI jobs with nothing typed: {Median(Repeat(10, () => Dispatcher.UIThread.RunJobs())):F1} ms");
            Console.WriteLine($"  one rendered frame with nothing typed: {Median(Repeat(10, () => { using (window.CaptureRenderedFrame()) { } })):F1} ms");
            Console.WriteLine($"Typing into the big file: {Median(typing):F1} ms per keystroke on the UI thread (median of {typing.Count}); " +
                              $"of that the edit and its handlers {Median(insertOnly):F1} ms, queued UI jobs {Median(typing) - Median(insertOnly):F1} ms; " +
                              $"just reading editor.Text {Median(textCopy):F1} ms");
            Console.WriteLine($"Big file ({bigKb} KB, {big.Code!.Count(c => c == '\n')} lines): open {bigOpen.ToLayout:F0} ms (+frame {bigOpen.ToFrame:F0}), " +
                              $"switch to it {Median(toBig.Select(s => s.ToLayout)):F0} ms (+frame {Median(toBig.Select(s => s.ToFrame)):F0}), " +
                              $"switch away {Median(fromBig.Select(s => s.ToLayout)):F0} ms");
        }

        // What one tab switch is made of, measured one piece at a time.
        Console.WriteLine();
        Console.WriteLine("Breakdown (median of 5, ms):");
        Console.WriteLine($"  scan + read + parse the workspace (LoadWorkspaceSummariesAsync): {Median(Repeat(5, () => Pump(storage.LoadWorkspaceSummariesAsync()))),9:F1}");
        Console.WriteLine($"  list the folders (LoadFolderPathsAsync):                         {Median(Repeat(5, () => Pump(storage.LoadFolderPathsAsync()))),9:F1}");
        Console.WriteLine($"  RefreshExplorerAsync (scan + rebuild tree + layout):             {Median(Repeat(5, () => { Pump(codeVm.RefreshExplorerAsync()); Dispatcher.UIThread.RunJobs(); })),9:F1}");
        Console.WriteLine($"  RefreshExplorerAsync + one rendered frame:                       {Median(Repeat(5, () => { Pump(codeVm.RefreshExplorerAsync()); Dispatcher.UIThread.RunJobs(); using (window.CaptureRenderedFrame()) { } })),9:F1}");
        Console.WriteLine($"  one rendered frame, nothing changed:                             {Median(Repeat(5, () => { using (window.CaptureRenderedFrame()) { } })),9:F1}");

        // The Hub draws a capped number of cards; the button that reveals the rest must really be on screen.
        var hub = host.ManagerViewModel;
        var showMore = (view.Pages.ViewFor(hub) as Control)?.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Command == hub.ShowMoreItemsCommand);
        // Its own flag (bound to HasHiddenItems): the Hub itself is hidden by now, another page is showing.
        Console.WriteLine($"Hub list: {hub.FilteredItems.Count} of {hub.FilteredItemCount} drawn; \"Show more\" button {(showMore == null ? "MISSING" : showMore.IsVisible ? "shown" : "hidden")}");

        var pageHost = view.GetType().GetProperty("Pages")?.GetValue(view);
        if (pageHost?.GetType().GetProperty("BuiltViewCount")?.GetValue(pageHost) is int built)
        {
            Console.WriteLine($"Page views built over the whole run: {built} (one per page)");
        }

        if (options.Int("cells", 0) is > 0 and var cellCount) LongNotebook(window, host, storage, cellCount);
        if (options.Int("external", 0) is > 0 and var externalFiles) ExternalWorkspace(window, host, storage, externalFiles);

        window.Close();
    }

    // --cells n: one notebook of n code cells, opened in the Notebook Studio: what a long notebook costs to show.
    private static void LongNotebook(Window window, CSharpStudioHostViewModel host, LocalScriptStorageService storage, int cells)
    {
        var notebook = Pump(storage.CreateNewNotebookAsync("Long notebook"));
        notebook.Cells.Clear();
        for (int i = 0; i < cells; i++)
        {
            notebook.Cells.Add(new NotebookCellItem
            {
                Type = CellType.Code,
                Source = $"var value{i} = {i} * 2;\nConsole.WriteLine(value{i});",
                OutputText = $"{i * 2}",
                ExecutionCount = i + 1
            });
        }

        Pump(storage.SaveNotebookAsync(notebook));
        long before = SettledMemory();
        var opened = Navigate(window, () => host.NavigateToNotebookStudio(notebook), () => ReferenceEquals(host.CurrentPage, host.NotebookStudioViewModel) && host.NotebookStudioViewModel!.ActiveTab?.Notebook.Id == notebook.Id);
        long after = SettledMemory();
        var away = Navigate(window, () => host.NavigateToDocs(), () => ReferenceEquals(host.CurrentPage, host.DocsViewModel));
        var back = Navigate(window, () => host.NavigateToNotebookStudio(notebook), () => ReferenceEquals(host.CurrentPage, host.NotebookStudioViewModel));

        Console.WriteLine();
        Console.WriteLine($"Long notebook ({cells} cells): open {opened.ToLayout:F0} ms (+frame {opened.ToFrame:F0}), memory +{(after - before) / 1048576.0:F0} MB, " +
                          $"back to it {back.ToLayout:F0} ms (+frame {back.ToFrame:F0}); {host.NotebookStudioViewModel!.ActiveTab?.Cells.Count} cell view models");
        _ = away;

        // Scrolling: the canvas builds cells as they come into view, so a page at a time should stay quick, and the cells
        // that appear must show the right text.
        if (window.Content is CSharpStudioHostView hostView && hostView.Pages.ViewFor(host.NotebookStudioViewModel!) is Control notebookView)
        {
            Navigate(window, () => host.NavigateToNotebookStudio(notebook), () => ReferenceEquals(host.CurrentPage, host.NotebookStudioViewModel));
            var scroller = notebookView.GetVisualDescendants().OfType<ScrollViewer>().First(s => s.Name == "NotebookCanvasScrollViewer");
            int Realized() => notebookView.GetVisualDescendants().OfType<PdfEditorApp.Plugins.CSharpEditor.Controls.BindableTextEditor>().Count();
            var steps = new List<double>();
            for (int i = 0; i < 25; i++)
            {
                steps.Add(Timed(window, () => { scroller.PageDown(); Dispatcher.UIThread.RunJobs(); }).ToFrame);
            }

            Console.WriteLine($"  scrolling a page at a time: {Median(steps):F0} ms per page (slowest {steps.Max():F0} ms); code editors alive: {Realized()} of {cells}");

            scroller.ScrollToHome();
            Dispatcher.UIThread.RunJobs();
            var wheel = new List<double>();
            for (int i = 0; i < 60; i++)
            {
                wheel.Add(Timed(window, () => { scroller.Offset = new Avalonia.Vector(0, scroller.Offset.Y + 120); Dispatcher.UIThread.RunJobs(); }).ToFrame);
            }

            Console.WriteLine($"  a mouse-wheel notch (120 px) at a time: {Median(wheel):F0} ms median, {wheel.Max():F0} ms slowest, {wheel.Count(w => w > 100)} of {wheel.Count} over 100 ms");

            scroller.ScrollToEnd();
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();
            var lastRealized = notebookView.GetVisualDescendants().OfType<PdfEditorApp.Plugins.CSharpEditor.Controls.BindableTextEditor>()
                .Select(e => e.DataContext as NotebookCellViewModel).Where(c => c != null)
                .Select(c => host.NotebookStudioViewModel!.Cells.IndexOf(c!)).DefaultIfEmpty(-1).Max();
            Console.WriteLine($"  after Ctrl+End the bottom-most built cell is #{lastRealized} of {cells - 1}");
        }

        // The side panels that list every cell: what opening them costs on a long notebook.
        var notebookVm = host.NotebookStudioViewModel!;
        var outline = Timed(window, () => { notebookVm.SelectedActivityBarIndex = 1; Dispatcher.UIThread.RunJobs(); });
        var search = Timed(window, () => { notebookVm.SelectedActivityBarIndex = 3; Dispatcher.UIThread.RunJobs(); });
        notebookVm.SelectedActivityBarIndex = 0;
        Dispatcher.UIThread.RunJobs();
        Console.WriteLine($"  opening the Outline panel: {outline.ToFrame:F0} ms; the Search panel: {search.ToFrame:F0} ms ({cells} cells)");

        // What one cell is made of: the two heaviest parts built on their own, 40 of each.
        Console.WriteLine($"  one code editor:        {Cost(40, () => new PdfEditorApp.Plugins.CSharpEditor.Controls.BindableTextEditor())}");
        Console.WriteLine($"  one cell output control: {Cost(40, () => new PdfEditorApp.Plugins.CSharpEditor.Controls.NotebookCellOutputControl())}");
    }

    // --external n: open a folder of n source files (25 per folder), the way a big project is opened, and see what the
    // Explorer and the Hub make of it.
    private static void ExternalWorkspace(Window window, CSharpStudioHostViewModel host, LocalScriptStorageService storage, int files)
    {
        var root = Snapshot.TempFolder("perf-external");
        int folders = files / 25 + 1;
        for (int f = 0; f < folders; f++)
        {
            var folder = Path.Combine(root, "src", $"module{f:D4}");
            Directory.CreateDirectory(folder);
            for (int i = 0; i < 25 && f * 25 + i < files; i++)
            {
                File.WriteAllText(Path.Combine(folder, $"file{i:D2}.py"), $"print({f * 25 + i})\n");
            }
        }

        long before = SettledMemory();
        var clock = Stopwatch.StartNew();
        var result = Pump(storage.OpenExternalProjectAsync(root));
        double openMs = clock.Elapsed.TotalMilliseconds;

        var codeVm = host.CodeStudioViewModel!;
        var explorer = Timed(window, () => Pump(codeVm.RefreshExplorerAsync()));
        int inExplorer = CountFiles(codeVm.ExplorerRootItems);
        var hubReload = Timed(window, () => Pump(host.ManagerViewModel.LoadWorkspaceItemsAsync()));
        long after = SettledMemory();

        Console.WriteLine();
        Console.WriteLine($"External folder ({files} files in {folders} folders): open {openMs:F0} ms ({result.Message}); " +
                          $"Explorer refresh {explorer.ToLayout:F0} ms showing {inExplorer:N0} of {files:N0} files; Hub reload {hubReload.ToLayout:F0} ms; memory +{(after - before) / 1048576.0:F0} MB");

        // The workspace file index behind Go to File: the background walk, then what a search costs.
        var index = storage.FileIndex;
        long beforeIndex = SettledMemory();
        var indexClock = Stopwatch.StartNew();
        Pump(index.RebuildAsync(storage.ActiveWorkspaceRootPath));
        double indexMs = indexClock.Elapsed.TotalMilliseconds;
        long afterIndex = SettledMemory();
        var queries = new[] { "file0042", "module0007", "f1", "py", "zzzz", "m0007/file" };
        var searchMs = queries.Select(q => Median(Repeat(15, () => index.Find(q, 30)))).ToList();
        Console.WriteLine($"  file index: {index.Count:N0} files indexed in {indexMs:F0} ms (in the background), {(afterIndex - beforeIndex) / 1048576.0:F1} MB; " +
                          $"a Go to File search takes {searchMs.Max():F1} ms at worst, {Median(searchMs):F1} ms typically" +
                          (index.IsTruncated ? " (workspace bigger than the index limit)" : string.Empty));
    }

    // Time and memory for building one control, averaged over `count` of them (kept alive while measuring).
    private static string Cost(int count, Func<object> build)
    {
        build(); // Warm-up: the first one pays for loading the XAML.
        long before = SettledMemory();
        var clock = Stopwatch.StartNew();
        var kept = new List<object>();
        for (int i = 0; i < count; i++) kept.Add(build());
        double ms = clock.Elapsed.TotalMilliseconds / count;
        long after = SettledMemory();
        GC.KeepAlive(kept);
        return $"{ms:F1} ms and {(after - before) / 1048576.0 / count:F2} MB each";
    }

    private static int CountFiles(IEnumerable<ExplorerItemViewModel> items) =>
        items.Sum(i => i.IsDirectory ? CountFiles(i.Children) : 1);

    // One page switch: the call, the wait for the page to become current, the layout it triggers, then one rendered frame.
    private static Sample Navigate(Window window, Action go, Func<bool> arrived)
    {
        var clock = Stopwatch.StartNew();
        go();
        var limit = Stopwatch.StartNew();
        while (!arrived())
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Yield();
            if (limit.Elapsed > TimeSpan.FromMinutes(1)) throw new TimeoutException("The page never became current.");
        }

        Dispatcher.UIThread.RunJobs();
        double toLayout = clock.Elapsed.TotalMilliseconds;
        using (window.CaptureRenderedFrame()) { }
        return new Sample(toLayout, clock.Elapsed.TotalMilliseconds);
    }

    private static Sample Timed(Window window, Action action)
    {
        var clock = Stopwatch.StartNew();
        action();
        Dispatcher.UIThread.RunJobs();
        double toLayout = clock.Elapsed.TotalMilliseconds;
        using (window.CaptureRenderedFrame()) { }
        return new Sample(toLayout, clock.Elapsed.TotalMilliseconds);
    }

    // Like Snapshot.Wait, but without its 5 ms sleep, so short operations are not rounded up to it.
    private static void Pump(Task task)
    {
        var limit = Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Yield();
            if (limit.Elapsed > TimeSpan.FromMinutes(3)) throw new TimeoutException("A task never finished.");
        }

        task.GetAwaiter().GetResult();
    }

    private static T Pump<T>(Task<T> task)
    {
        Pump((Task)task);
        return task.Result;
    }

    // Valid C# of about the requested size: a class of small methods, so highlighting and folding have real work to do.
    private static string BigCode(int kilobytes)
    {
        var code = new System.Text.StringBuilder("public class Generated\n{\n");
        for (int i = 0; code.Length < kilobytes * 1024; i++)
        {
            code.Append("    public int Method").Append(i).Append("(int value)\n    {\n        var doubled = value * 2;\n        return doubled + ").Append(i).Append(";\n    }\n\n");
        }

        return code.Append("}\n").ToString();
    }

    private static long SettledMemory()
    {
        Snapshot.Settle(3);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return GC.GetTotalMemory(forceFullCollection: true);
    }

    private static List<double> Repeat(int times, Action action)
    {
        var results = new List<double>();
        for (int i = 0; i < times; i++)
        {
            var clock = Stopwatch.StartNew();
            action();
            results.Add(clock.Elapsed.TotalMilliseconds);
        }

        return results;
    }

    private static List<double> Repeat<T>(int times, Func<T> action) => Repeat(times, () => { action(); });

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.Order().ToList();
        return sorted.Count == 0 ? 0 : sorted[sorted.Count / 2];
    }
}
