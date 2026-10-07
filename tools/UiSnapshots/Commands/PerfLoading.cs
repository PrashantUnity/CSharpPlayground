using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio;
using PdfEditorApp.Plugins.CSharpEditor.Views;
using SkiaSharp;

namespace PdfEditorApp.Plugins.CSharpEditor.Tools.UiSnapshots.Commands;

/// <summary>
/// Measures how long input would wait on the UI thread. A background thread keeps one tiny job queued at input priority
/// and records how long it waited before it ran: the longest wait is the longest stretch the UI thread was busy with
/// something else, which is what a user feels as a hang. Read it with <see cref="Measure"/> around one scenario.
/// </summary>
internal sealed class StallProbe : IDisposable
{
    private readonly Thread _thread;
    private volatile bool _stop;
    private long _maxTicks;
    private long _scopeStart = Stopwatch.GetTimestamp();
    private readonly ManualResetEventSlim _ran = new(true);

    public StallProbe()
    {
        _thread = new Thread(Loop) { IsBackground = true, Name = "ui-stall-probe" };
        _thread.Start();
    }

    private void Loop()
    {
        while (!_stop)
        {
            _ran.Reset();
            long posted = Stopwatch.GetTimestamp();
            Dispatcher.UIThread.Post(() =>
            {
                Record(posted);
                _ran.Set();
            }, DispatcherPriority.Input);

            // A wait that is still growing counts too, or a hang that outlasts the scope would never be seen.
            while (!_ran.Wait(20))
            {
                if (_stop) return;
                Record(posted);
            }

            Thread.Sleep(2);
        }
    }

    private void Record(long posted)
    {
        long from = Math.Max(posted, Interlocked.Read(ref _scopeStart));
        long waited = Stopwatch.GetTimestamp() - from;
        long seen;
        while (waited > (seen = Interlocked.Read(ref _maxTicks)) && Interlocked.CompareExchange(ref _maxTicks, waited, seen) != seen)
        {
        }
    }

    /// <summary>Runs <paramref name="action"/> and returns the longest time input would have waited meanwhile (ms).</summary>
    public double Measure(Action action)
    {
        Interlocked.Exchange(ref _scopeStart, Stopwatch.GetTimestamp());
        Interlocked.Exchange(ref _maxTicks, 0);
        action();
        // Let a job queued during the action run, so its wait is counted.
        Dispatcher.UIThread.RunJobs();
        return Interlocked.Read(ref _maxTicks) * 1000.0 / Stopwatch.Frequency;
    }

    public void Dispose()
    {
        _stop = true;
        _thread.Join(500);
    }
}

/// <summary>
/// The loading scenarios of <c>perf</c>: opening the Code Studio while the engine is still starting, and opening big
/// artifacts (a CSV shown as a table, a camera-sized image), each with the longest input wait it causes.
/// </summary>
internal static class PerfLoading
{
    private static void Pump(Task task)
    {
        var limit = Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Yield();
            if (limit.Elapsed > TimeSpan.FromMinutes(5)) throw new TimeoutException("A task never finished.");
        }

        task.GetAwaiter().GetResult();
    }

    private static double Frame(Window window)
    {
        var clock = Stopwatch.StartNew();
        Dispatcher.UIThread.RunJobs();
        using (window.CaptureRenderedFrame()) { }
        return clock.Elapsed.TotalMilliseconds;
    }

    // --theme-switch: apply theme presets from the Settings page (every page already built), the way a user tries them.
    public static void ThemeSwitch(Window window, PdfEditorApp.Plugins.CSharpEditor.ViewModels.Common.CSharpStudioHostViewModel host, StallProbe probe, bool shots = false)
    {
        var settings = host.SettingsViewModel;
        host.NavigateToSettings("Themes");
        Pump(Task.CompletedTask);
        Dispatcher.UIThread.RunJobs();
        using (window.CaptureRenderedFrame()) { }
        var presets = settings.ThemePresets.Select(p => p.Id).ToList();
        int resourceChanges = 0;
        void Count(object? sender, Avalonia.Controls.ResourcesChangedEventArgs e) => resourceChanges++;
        window.ResourcesChanged += Count;
        foreach (var id in presets.Take(4).Concat(presets.Take(1)))
        {
            resourceChanges = 0;
            var gc = GC.GetTotalPauseDuration();
            var clock = Stopwatch.StartNew();
            double refreshMs = 0, layoutMs = 0;
            double stall = probe.Measure(() =>
            {
                // What the Settings button does, split: the engine's swap, the token inspector refresh, then layout + a frame.
                var part = Stopwatch.StartNew();
                settings.ApplyThemePreset(id);
                refreshMs = part.Elapsed.TotalMilliseconds;
                var reflected = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
                var layoutManager = typeof(TopLevel).GetProperty("LayoutManager", reflected)?.GetValue(window) ?? typeof(TopLevel).GetField("_layoutManager", reflected)?.GetValue(window);
                var queued = new SortedDictionary<string, int>();
                int total = 0;
                if (layoutManager?.GetType().GetField("_toMeasure", reflected)?.GetValue(layoutManager) is System.Collections.IEnumerable toMeasure)
                {
                    foreach (var control in toMeasure)
                    {
                        total++;
                        string where = "";
                        for (var v = control as Avalonia.Visual; v != null; v = v.GetVisualParent())
                        {
                            if (v.GetType().Name.StartsWith("Theme") || v.GetType().Name.EndsWith("SectionControl")) { where = v.GetType().Name; break; }
                        }

                        queued[where] = queued.GetValueOrDefault(where) + 1;
                    }
                }

                Console.WriteLine($"   queued for measure: {total} — " + string.Join(", ", queued.OrderByDescending(kv => kv.Value).Take(8).Select(kv => $"{(kv.Key.Length == 0 ? "(elsewhere)" : kv.Key)} x{kv.Value}")));
                part.Restart();
                var executeLayoutPass = layoutManager?.GetType().GetMethod("ExecuteLayoutPass", reflected, Type.EmptyTypes);
                executeLayoutPass?.Invoke(layoutManager, null);
                double layoutOnly = part.Elapsed.TotalMilliseconds;
                part.Restart();
                Dispatcher.UIThread.RunJobs();
                double jobsOnly = part.Elapsed.TotalMilliseconds;
                part.Restart();
                using (window.CaptureRenderedFrame()) { }
                Console.WriteLine($"   after the apply: layout pass {layoutOnly:F0} ms, queued jobs {jobsOnly:F0} ms, one frame {part.Elapsed.TotalMilliseconds:F0} ms");
                layoutMs = layoutOnly + jobsOnly + part.Elapsed.TotalMilliseconds;
            });
            Console.WriteLine($"   split: the Settings command (theme swap + token inspector) {refreshMs:F0} ms, layout + jobs + frame {layoutMs:F0} ms");
            if (shots) Snapshot.Save(window, $"theme-{id}");
            Console.WriteLine($"theme {id}: {clock.Elapsed.TotalMilliseconds:F0} ms, longest input wait {stall:F0} ms, " +
                              $"resource-change notifications reaching the window {resourceChanges}, GC pauses {(GC.GetTotalPauseDuration() - gc).TotalMilliseconds:F0} ms");
        }

        window.ResourcesChanged -= Count;

        // The swap alone (every dynamic resource re-resolving), ten themes in a row: steadier than a whole apply, which
        // also lays out, draws and collects garbage.
        var engine = PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.StudioAppContext.Instance.ThemeEngine;
        var swaps = new List<double>();
        var applies = new List<double>();
        for (int i = 0; i < 10; i++)
        {
            var apply = Stopwatch.StartNew();
            settings.ApplyThemePreset(i % 2 == 0 ? "dracula" : "dark-plus");
            Dispatcher.UIThread.RunJobs();
            using (window.CaptureRenderedFrame()) { }
            applies.Add(apply.Elapsed.TotalMilliseconds);
            swaps.Add(engine.LastCommitDuration.TotalMilliseconds);
        }

        // What a restyle of the whole studio costs (every page built): a style added to the host and removed again.
        var hostView = window.GetVisualDescendants().OfType<PdfEditorApp.Plugins.CSharpEditor.Views.CSharpStudioHostView>().FirstOrDefault();
        if (hostView != null)
        {
            var restyles = new List<double>();
            for (int i = 0; i < 3; i++)
            {
                var probeStyle = new Avalonia.Styling.Style(x => Avalonia.Styling.Selectors.Class(Avalonia.Styling.Selectors.OfType(x, typeof(TextBlock)), "restyle-probe"))
                {
                    Setters = { new Avalonia.Styling.Setter(TextBlock.FontSizeProperty, 11.0) },
                };
                var clock = Stopwatch.StartNew();
                hostView.Styles.Add(probeStyle);
                Dispatcher.UIThread.RunJobs();
                using (window.CaptureRenderedFrame()) { }
                restyles.Add(clock.Elapsed.TotalMilliseconds);
                hostView.Styles.Remove(probeStyle);
                Dispatcher.UIThread.RunJobs();
            }

            Console.WriteLine($"restyle of the whole studio (a style added to the host): {string.Join(", ", restyles.Select(r => r.ToString("F0")))} ms");
        }

        Console.WriteLine($"theme switch x10: swap (resources re-resolving) median {swaps.Order().ElementAt(5):F1} ms, " +
                          $"whole apply median {applies.Order().ElementAt(5):F0} ms, slowest {applies.Max():F0} ms");

        // Dragging the hue wheel: one change per frame, each followed by its jobs and a frame, as the user sees it.
        var frames = new List<double>();
        double dragStall = probe.Measure(() =>
        {
            for (int i = 1; i <= 30; i++)
            {
                var frame = Stopwatch.StartNew();
                settings.SelectedHueDegrees = i * 12f;
                Dispatcher.UIThread.RunJobs();
                using (window.CaptureRenderedFrame()) { }
                frames.Add(frame.Elapsed.TotalMilliseconds);
            }
        });
        Console.WriteLine($"hue wheel drag (30 steps): median {frames.Order().ElementAt(frames.Count / 2):F0} ms per step, slowest {frames.Max():F0} ms; longest input wait {dragStall:F0} ms");

        // The palette studio: Generate (Space) twenty times in a row, one section regenerated, the engine switched — each
        // previewed (sections recoloured) and drawn; budget 10 ms of palette work per press.
        void Step(string name, Action action, int times = 1)
        {
            var steps = new List<double>();
            double stall = probe.Measure(() =>
            {
                for (int i = 0; i < times; i++)
                {
                    var step = Stopwatch.StartNew();
                    action();
                    Dispatcher.UIThread.RunJobs();
                    using (window.CaptureRenderedFrame()) { }
                    steps.Add(step.Elapsed.TotalMilliseconds);
                }
            });
            Console.WriteLine($"{name}: median {steps.Order().ElementAt(steps.Count / 2):F0} ms, slowest {steps.Max():F0} ms (x{times}); longest input wait {stall:F0} ms");
        }

        var paletteOnly = Stopwatch.StartNew();
        var spec = settings.PaletteSpec;
        for (int i = 0; i < 20; i++)
        {
            spec = PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Palette.PaletteGenerator.Regenerate(spec);
            PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Palette.ThemeTokenMapper.Map(
                PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Palette.PaletteGenerator.Generate(spec));
        }

        Console.WriteLine($"palette generate + token map alone: {paletteOnly.Elapsed.TotalMilliseconds / 20:F1} ms each");
        Step("palette Generate (Space)", settings.GeneratePalette, times: 20);
        Step("palette regenerate one section", () => settings.RegenerateSection(settings.CoreSections[1]), times: 5);
        Step("palette lock a section", () => settings.ToggleSectionLock(settings.CoreSections[0]), times: 2);
        Step("palette engine switch", () => settings.SelectedColorEngine = settings.SelectedColorEngine == PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.ColorMath.ColorEngineKind.Hct
            ? PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.ColorMath.ColorEngineKind.Oklch
            : PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.ColorMath.ColorEngineKind.Hct, times: 4);
        Step("palette undo", settings.UndoPalette, times: 5);
        // Layouts: every keyed control gets its new value, then the studio lays out again.
        foreach (var presetId in new[] { "layout-material", "layout-vscode", "layout-large", "layout-studio" })
        {
            var preset = PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Layout.LayoutPresets.Get(presetId)!;
            var clock = Stopwatch.StartNew();
            double stall = probe.Measure(() =>
            {
                PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.StudioAppContext.Instance.ThemeEngine.ApplyLayout(preset.Spec);
                Dispatcher.UIThread.RunJobs();
                using (window.CaptureRenderedFrame()) { }
            });
            Console.WriteLine($"layout {preset.Name}: {clock.Elapsed.TotalMilliseconds:F0} ms ({PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Layout.Tokens.RegisteredCount:N0} keyed controls), longest input wait {stall:F0} ms");
        }

        Step("palette apply (generate + apply as the studio theme)", () => { settings.GeneratePalette(); settings.ApplyHarmonicConfiguration(); }, times: 3);

        foreach (var (name, action) in new (string, Func<Task>)[]
        {
            ("apply the harmonic theme", () => settings.ApplyHarmonicConfigurationAsync()),
            ("randomize the wheel", () => settings.RandomizeHarmonicWheelAsync()),
            ("density compact", () => settings.SetDensityAsync("Compact")),
            ("density comfortable", () => settings.SetDensityAsync("Comfortable")),
            ("theme dark-plus again", () => settings.ApplyThemePresetAsync("dark-plus")),
        })
        {
            var clock = Stopwatch.StartNew();
            double stall = probe.Measure(() =>
            {
                Pump(action());
                Dispatcher.UIThread.RunJobs();
                using (window.CaptureRenderedFrame()) { }
            });
            Console.WriteLine($"{name}: {clock.Elapsed.TotalMilliseconds:F0} ms, longest input wait {stall:F0} ms");
        }
    }

    // --table-rows n: a table of n rows shown in a window of its own, the way the CSV preview shows it.
    public static void TableOnly(StallProbe probe, int rows, string? shot = null)
    {
        var table = new PdfEditorApp.Plugins.CSharpEditor.Models.DumpTableResult("Generated", "CSV Table");
        foreach (var header in new[] { "id", "name", "value", "date", "flag" })
        {
            table.Columns.Add(new PdfEditorApp.Plugins.CSharpEditor.Models.DumpTableColumn { Header = header, IsNumeric = header is "id" or "value" });
        }

        var clock = Stopwatch.StartNew();
        table.AddRows(Enumerable.Range(0, rows).Select(i => new PdfEditorApp.Plugins.CSharpEditor.Models.DumpTableRow(i, new[]
        {
            new PdfEditorApp.Plugins.CSharpEditor.Models.DumpTableCell { DisplayText = i.ToString(), IsNumeric = true },
            new PdfEditorApp.Plugins.CSharpEditor.Models.DumpTableCell { DisplayText = $"item {i}" },
            new PdfEditorApp.Plugins.CSharpEditor.Models.DumpTableCell { DisplayText = (i * 3.25).ToString(System.Globalization.CultureInfo.InvariantCulture), IsNumeric = true },
            new PdfEditorApp.Plugins.CSharpEditor.Models.DumpTableCell { DisplayText = "2026-10-06" },
            new PdfEditorApp.Plugins.CSharpEditor.Models.DumpTableCell { DisplayText = (i % 2 == 0).ToString() },
        })).ToList());
        Console.WriteLine($"table of {rows:N0} rows built: {clock.Elapsed.TotalMilliseconds:F0} ms");

        var view = new DumpTableView { FillHeight = true };
        var window = new Window { Width = 1000, Height = 700, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        clock.Restart();
        double stall = probe.Measure(() =>
        {
            view.DataContext = table;
            Dispatcher.UIThread.RunJobs();
            using (window.CaptureRenderedFrame()) { }
        });
        Console.WriteLine($"table of {rows:N0} rows shown: {clock.Elapsed.TotalMilliseconds:F0} ms; rows built as controls: {view.RowsList?.RealizedRows.Count}; longest input wait {stall:F0} ms");
        Console.WriteLine($"  rows list: panel {view.RowsList?.ItemsPanelRoot?.GetType().Name}, bounds {view.RowsList?.Bounds}, scroll viewer {(view.RowsList?.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault() is { } sv ? $"viewport {sv.Viewport} extent {sv.Extent}" : "none")}");
        for (Avalonia.Visual? v = view.RowsList; v != null; v = v.GetVisualParent()) Console.WriteLine($"    {v.GetType().Name} {v.Bounds.Size}");

        clock.Restart();
        double sortStall = probe.Measure(() =>
        {
            table.SortByColumn(1);
            Dispatcher.UIThread.RunJobs();
            using (window.CaptureRenderedFrame()) { }
        });
        Console.WriteLine($"sorted by a text column: {clock.Elapsed.TotalMilliseconds:F0} ms; longest input wait {sortStall:F0} ms; rows built {view.RowsList?.RealizedRows.Count}");
        if (shot != null) Snapshot.Save(window, shot);
        window.Close();
    }

    // --big-csv-mb n: a CSV of about n MB opened in the Code Studio (shown as a table), then one keystroke in it.
    public static void BigCsv(Window window, CSharpStudioHostView view, CSharpCodeStudioViewModel codeVm, IScriptStorageService storage, StallProbe probe, double megabytes, bool experiments = false)
    {
        var path = Path.Combine(storage.ActiveWorkspaceRootPath, $"big-{megabytes:0.##}mb.csv");
        var text = new System.Text.StringBuilder("id,name,value,date,flag\n");
        for (int i = 0; text.Length < megabytes * 1048576; i++)
        {
            text.Append(i).Append(",item ").Append(i).Append(',').Append(i * 3.25).Append(",2026-10-").Append(1 + i % 28).Append(',').Append(i % 2 == 0).Append('\n');
        }

        File.WriteAllText(path, text.ToString());
        int rows = text.ToString().Count(c => c == '\n') - 1;

        if (experiments)
        {
            // Which part costs: the same text as a plain text document (no table), then the table preview alone.
            var plain = new PdfEditorApp.Plugins.CSharpEditor.Models.ScriptDocumentItem
            {
                Title = "plain.txt", Code = text.ToString(), LanguageId = PdfEditorApp.Plugins.CSharpEditor.Services.Languages.LanguageIds.Text
            };
            // The view model alone (no view attached).
            var bare = new CSharpCodeStudioViewModel(new PdfEditorApp.Plugins.CSharpEditor.Models.ScriptDocumentItem { Title = "start" }, storage,
                codeVm.CompilerService, new PdfEditorApp.Plugins.CSharpEditor.Services.Execution.ScriptExecutionEngine(), backToHubAction: () => { });
            var c0 = Stopwatch.StartNew();
            Pump(bare.UpdateActiveScriptAsync(new PdfEditorApp.Plugins.CSharpEditor.Models.ScriptDocumentItem { Title = "plain2.txt", Code = text.ToString(), LanguageId = PdfEditorApp.Plugins.CSharpEditor.Services.Languages.LanguageIds.Text }));
            Console.WriteLine($"  experiment: view model alone opens it: {c0.Elapsed.TotalMilliseconds:F0} ms");

            // The editor alone.
            var editorOnly = (view.Pages.ViewFor(codeVm) as CSharpCodeStudioView)!.FindControl<AvaloniaEdit.TextEditor>("Editor")!;
            var c00 = Stopwatch.StartNew();
            var docOnly = new AvaloniaEdit.Document.TextDocument(text.ToString());
            Console.WriteLine($"  experiment: new TextDocument: {c00.Elapsed.TotalMilliseconds:F0} ms");
            {
                double T(Action a) { var c = Stopwatch.StartNew(); a(); return c.Elapsed.TotalMilliseconds; }
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var script = codeVm.Script;
                var oldCode = script.Code;
                Console.WriteLine($"  experiment:   Script.Code = it: {T(() => script.Code = text.ToString()):F0} ms");
                var tab = codeVm.OpenTabs.First(t => t.Id == script.Id);
                Console.WriteLine($"  experiment:   tab.Document.Code = it: {T(() => tab.Document.Code = text.ToString()):F0} ms");
                Console.WriteLine($"  experiment:   tab.IsDirty = true: {T(() => tab.IsDirty = true):F0} ms");
                Console.WriteLine($"  experiment:   Script.LastModified = now: {T(() => script.LastModified = DateTime.UtcNow):F0} ms");
                var restoring = typeof(CSharpCodeStudioViewModel).GetField("_isRestoringTabState", flags)!;
                restoring.SetValue(codeVm, true);
                Console.WriteLine($"  experiment:   Code = it while restoring a tab: {T(() => codeVm.Code = text.ToString()):F0} ms");
                restoring.SetValue(codeVm, false);
                Console.WriteLine($"  experiment:   Code = it + 'x' (a keystroke): {T(() => codeVm.Code = text + "x"):F0} ms");
                codeVm.Code = oldCode;
                var codeField = typeof(CSharpCodeStudioViewModel).GetField("_code", flags)!;
                var saved = codeField.GetValue(codeVm);
                codeField.SetValue(codeVm, text.ToString());
                Console.WriteLine($"  experiment:   TriggerDiagnosticsCheck: {T(() => typeof(CSharpCodeStudioViewModel).GetMethod("TriggerDiagnosticsCheck", flags)!.Invoke(codeVm, null)):F0} ms");
                Console.WriteLine($"  experiment:   RefreshDocumentNuGetPackages: {T(() => codeVm.RefreshDocumentNuGetPackages()):F0} ms");
                codeField.SetValue(codeVm, saved);
                script.Code = oldCode;
                tab.Document.Code = oldCode;
            }

            var codeBefore = codeVm.Code;
            var markdownViews = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants((view.Pages.ViewFor(codeVm) as Control)!).OfType<PdfEditorApp.Plugins.CSharpEditor.Controls.Docs.MarkdownView>().ToList();
            var previews = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants((view.Pages.ViewFor(codeVm) as Control)!).OfType<PdfEditorApp.Plugins.CSharpEditor.Controls.Studio.StudioMarkdownPreviewControl>().ToList();
            Console.WriteLine($"  experiment: markdown previews in the logical tree: {previews.Count}, visible {string.Join(",", previews.Select(p => p.IsVisible + "/" + p.IsEffectivelyVisible))}");
            var rendersBefore = markdownViews.Sum(m => m.RenderCount);
            var c01 = Stopwatch.StartNew();
            codeVm.Code = text.ToString();
            Console.WriteLine($"  experiment: the live view model's Code = it: {c01.Elapsed.TotalMilliseconds:F0} ms; markdown views {markdownViews.Count}, renders {markdownViews.Sum(m => m.RenderCount) - rendersBefore}, effectively visible {string.Join(",", markdownViews.Select(m => m.IsEffectivelyVisible))}");
            var field = typeof(CommunityToolkit.Mvvm.ComponentModel.ObservableObject).GetField("PropertyChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field?.GetValue(codeVm) is System.ComponentModel.PropertyChangedEventHandler handlers)
            {
                var codeField2 = typeof(CSharpCodeStudioViewModel).GetField("_code", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                int variant = 0;
                foreach (var handler in handlers.GetInvocationList())
                {
                    codeField2.SetValue(codeVm, text.ToString() + new string(' ', ++variant));
                    var hc = Stopwatch.StartNew();
                    handler.DynamicInvoke(codeVm, new System.ComponentModel.PropertyChangedEventArgs(nameof(codeVm.Code)));
                    double ms = hc.Elapsed.TotalMilliseconds;
                    Console.WriteLine($"  experiment:   PropertyChanged(Code) subscriber {handler.Method.DeclaringType?.FullName}.{handler.Method.Name} (target {handler.Target?.GetType().FullName}): {ms:F0} ms");
                }
            }

            codeVm.Code = codeBefore;
            Dispatcher.UIThread.RunJobs();
            var previousDoc = editorOnly.Document;
            var gcBefore = GC.GetTotalPauseDuration();
            c00.Restart();
            editorOnly.Document = docOnly;
            Console.WriteLine($"  experiment: editor.Document = it (the assignment): {c00.Elapsed.TotalMilliseconds:F0} ms");
            foreach (var band in new[] { DispatcherPriority.Render, DispatcherPriority.Input, DispatcherPriority.Background, DispatcherPriority.SystemIdle })
            {
                c00.Restart();
                Dispatcher.UIThread.RunJobs(band);
                Console.WriteLine($"  experiment:   then jobs down to {band}: {c00.Elapsed.TotalMilliseconds:F0} ms");
            }
            Console.WriteLine($"  experiment:   GC pauses meanwhile: {(GC.GetTotalPauseDuration() - gcBefore).TotalMilliseconds:F0} ms");
            var tv = editorOnly.TextArea.TextView;
            Console.WriteLine($"  experiment: editor bounds {editorOnly.Bounds.Size}, text view {tv.Bounds.Size}, visual lines {(tv.VisualLinesValid ? tv.VisualLines.Count : -1)} of {docOnly.LineCount}, visible {editorOnly.IsEffectivelyVisible}");
            for (Avalonia.Visual? v = editorOnly.GetVisualParent(); v != null && v is not CSharpCodeStudioView; v = v.GetVisualParent()) Console.WriteLine($"    {v.GetType().Name} {(v as Control)?.Name} {v.Bounds.Size}");
            editorOnly.Document = previousDoc;
            Dispatcher.UIThread.RunJobs();

            double Time(Action a) { var c = Stopwatch.StartNew(); a(); Dispatcher.UIThread.RunJobs(); return c.Elapsed.TotalMilliseconds; }
            var highlighting = editorOnly.SyntaxHighlighting;
            editorOnly.SyntaxHighlighting = null;
            Console.WriteLine($"  experiment: same, highlighting off: {Time(() => editorOnly.Document = new AvaloniaEdit.Document.TextDocument(text.ToString())):F0} ms");
            editorOnly.Document = previousDoc; Dispatcher.UIThread.RunJobs();
            editorOnly.SyntaxHighlighting = highlighting;
            var folding = new PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn.CSharpFoldingStrategy();
            var foldDoc = new AvaloniaEdit.Document.TextDocument(text.ToString());
            Console.WriteLine($"  experiment: C# folding strategy over it: {Time(() => folding.CreateNewFoldings(foldDoc, out _).Count()):F0} ms");
            var bareWindow = new Window { Width = 1000, Height = 800, Content = new AvaloniaEdit.TextEditor() };
            bareWindow.Show(); Dispatcher.UIThread.RunJobs();
            var bareEditor = (AvaloniaEdit.TextEditor)bareWindow.Content!;
            Console.WriteLine($"  experiment: a bare TextEditor shows it: {Time(() => bareEditor.Document = new AvaloniaEdit.Document.TextDocument(text.ToString())):F0} ms");
            bareWindow.Close();
            foreach (var withVm in new[] { false, true })
            {
                var freshView = new CSharpCodeStudioView();
                if (withVm) freshView.DataContext = bare;
                var freshWindow = new Window { Width = 1400, Height = 900, Content = freshView };
                freshWindow.Show(); Dispatcher.UIThread.RunJobs();
                var freshEditor = freshView.FindControl<AvaloniaEdit.TextEditor>("Editor")!;
                Console.WriteLine($"  experiment: a fresh Code Studio view {(withVm ? "with" : "without")} its view model shows it: {Time(() => freshEditor.Document = new AvaloniaEdit.Document.TextDocument(text.ToString())):F0} ms");
                freshView.Dispose();
                freshWindow.Close();
            }

            Console.WriteLine($"  experiment: editor type {editorOnly.GetType().Name}, word wrap {editorOnly.WordWrap}, show line numbers {editorOnly.ShowLineNumbers}, margins {string.Join(",", editorOnly.TextArea.LeftMargins.Select(m => m.GetType().Name))}, transformers {string.Join(",", editorOnly.TextArea.TextView.LineTransformers.Select(m => m.GetType().Name))}, renderers {string.Join(",", editorOnly.TextArea.TextView.BackgroundRenderers.Select(m => m.GetType().Name))}");

            {
                var other = new PdfEditorApp.Plugins.CSharpEditor.Models.ScriptDocumentItem { Title = "other.txt", Code = "a small text file\n", LanguageId = PdfEditorApp.Plugins.CSharpEditor.Services.Languages.LanguageIds.Text };
                var gcs = GC.GetTotalPauseDuration();
                var cs = Stopwatch.StartNew();
                var task = codeVm.UpdateActiveScriptAsync(other);
                Console.WriteLine($"  experiment: open a SMALL text file, synchronous part: {cs.Elapsed.TotalMilliseconds:F0} ms (done {task.IsCompleted})");
                foreach (var band in new[] { DispatcherPriority.Render, DispatcherPriority.Input, DispatcherPriority.Background, DispatcherPriority.SystemIdle })
                {
                    cs.Restart();
                    Dispatcher.UIThread.RunJobs(band);
                    Console.WriteLine($"  experiment:   then jobs down to {band}: {cs.Elapsed.TotalMilliseconds:F0} ms");
                }
                cs.Restart();
                Pump(task);
                using (window.CaptureRenderedFrame()) { }
                Console.WriteLine($"  experiment:   rest + a frame: {cs.Elapsed.TotalMilliseconds:F0} ms; GC pauses in all {(GC.GetTotalPauseDuration() - gcs).TotalMilliseconds:F0} ms");
                var csharpTab = codeVm.OpenTabs.First(t => t.Document.LanguageId != PdfEditorApp.Plugins.CSharpEditor.Services.Languages.LanguageIds.Text && t.Id != other.Id);
                cs.Restart();
                var backToCSharp = codeVm.SwitchToTabAsync(csharpTab);
                Console.WriteLine($"  experiment: back to a C# tab, synchronous part: {cs.Elapsed.TotalMilliseconds:F0} ms");
                var reflected = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
                var layoutManager = typeof(TopLevel).GetProperty("LayoutManager", reflected)?.GetValue(window) ?? typeof(TopLevel).GetField("_layoutManager", reflected)?.GetValue(window);
                var queued = new SortedDictionary<string, int>();
                if (layoutManager != null && layoutManager.GetType().GetField("_toMeasure", reflected)?.GetValue(layoutManager) is System.Collections.IEnumerable toMeasure)
                {
                    foreach (var control in toMeasure)
                    {
                        // Name the nearest named ancestor too, to tell which part of the page it is in.
                        string where = "";
                        for (var v = control as Avalonia.Visual; v != null; v = v.GetVisualParent())
                        {
                            if (v is Control { Name: { Length: > 0 } n }) { where = n; break; }
                            if (v.GetType().Name.StartsWith("Studio")) { where = v.GetType().Name; break; }
                        }

                        var key = $"{control.GetType().Name} in {where}";
                        queued[key] = queued.GetValueOrDefault(key) + 1;
                    }
                }

                Console.WriteLine($"  experiment:   queued for measure: {string.Join(", ", queued.OrderByDescending(kv => kv.Value).Take(25).Select(kv => $"{kv.Key} x{kv.Value}"))}");
                cs.Restart();
                Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
                Console.WriteLine($"  experiment:   then layout and render: {cs.Elapsed.TotalMilliseconds:F0} ms");
                Pump(backToCSharp);
            }

            var c1 = Stopwatch.StartNew();
            var gc1 = GC.GetTotalPauseDuration();
            int g2a = GC.CollectionCount(2);
            double plainStall = probe.Measure(() => Pump(codeVm.UpdateActiveScriptAsync(plain)));
            Console.WriteLine($"  experiment: the text as plain.txt: {c1.Elapsed.TotalMilliseconds:F0} ms, longest input wait {plainStall:F0} ms; GC pauses {(GC.GetTotalPauseDuration() - gc1).TotalMilliseconds:F0} ms, gen2 GCs {GC.CollectionCount(2) - g2a}; heap {GC.GetTotalMemory(false) / 1048576} MB");
            var back = codeVm.OpenTabs.First(t => t.Id != plain.Id);
            var c1b = Stopwatch.StartNew();
            double backStall = probe.Measure(() => Pump(codeVm.SwitchToTabAsync(back)));
            Console.WriteLine($"  experiment: switching back to a C# tab: {c1b.Elapsed.TotalMilliseconds:F0} ms, longest input wait {backStall:F0} ms");
            var plainTab = codeVm.OpenTabs.First(t => t.Id == plain.Id);
            c1b.Restart();
            backStall = probe.Measure(() => Pump(codeVm.SwitchToTabAsync(plainTab)));
            Console.WriteLine($"  experiment: and to the text tab again: {c1b.Elapsed.TotalMilliseconds:F0} ms, longest input wait {backStall:F0} ms");
            var c2 = Stopwatch.StartNew();
            double parseStall = probe.Measure(() =>
            {
                var records = CSharpCodeStudioViewModel.ParseCsvRecords(text.ToString(), ',');
                CSharpCodeStudioViewModel.BuildDumpTableFromRecords(records, "x.csv", ',');
            });
            Console.WriteLine($"  experiment: parse + build the table alone: {c2.Elapsed.TotalMilliseconds:F0} ms");
            var csvDoc = new PdfEditorApp.Plugins.CSharpEditor.Models.ScriptDocumentItem
            {
                Title = "data.csv", Code = text.ToString(), LanguageId = PdfEditorApp.Plugins.CSharpEditor.Services.Languages.LanguageIds.Text
            };
            var gc3 = GC.GetTotalPauseDuration();
            var c3 = Stopwatch.StartNew();
            double csvStall = probe.Measure(() => { Pump(codeVm.UpdateActiveScriptAsync(csvDoc)); Pump(codeVm.PendingCsvPreview); Dispatcher.UIThread.RunJobs(); });
            Console.WriteLine($"  experiment: the same text as data.csv (table preview): {c3.Elapsed.TotalMilliseconds:F0} ms, longest input wait {csvStall:F0} ms; GC pauses {(GC.GetTotalPauseDuration() - gc3).TotalMilliseconds:F0} ms");
            return;
        }

        var clock = Stopwatch.StartNew();
        double openStall = probe.Measure(() => Pump(codeVm.OpenWorkspaceFileAsync(path)));
        double openMs = clock.Elapsed.TotalMilliseconds;
        double frameMs = Frame(window);
        Console.WriteLine($"big CSV ({megabytes:0.##} MB, {rows:N0} rows): open {openMs:F0} ms + first frame {frameMs:F0} ms; longest input wait {openStall:F0} ms; preview {(codeVm.ShowCsvPreview ? "shown" : "off")}");
        var tableView = (view.Pages.ViewFor(codeVm) as Control)?.GetVisualDescendants().OfType<DumpTableView>().FirstOrDefault(v => v.IsEffectivelyVisible);
        Console.WriteLine($"  table rows built as controls: {tableView?.RowsList?.RealizedRows.Count.ToString() ?? "no table"}; panel {tableView?.RowsList?.ItemsPanelRoot?.GetType().Name}");
        if (tableView?.RowsList != null)
        {
            for (Avalonia.Visual? v = tableView.RowsList; v != null && v is not CSharpCodeStudioView; v = v.GetVisualParent()) Console.WriteLine($"    {v.GetType().Name} {v.Bounds.Size}");
        }

        var editor = (view.Pages.ViewFor(codeVm) as CSharpCodeStudioView)?.FindControl<AvaloniaEdit.TextEditor>("Editor");
        if (editor != null && editor.Document.TextLength > 0)
        {
            var keys = new List<double>();
            double keyStall = probe.Measure(() =>
            {
                for (int i = 0; i < 3; i++)
                {
                    var key = Stopwatch.StartNew();
                    editor.Document.Insert(editor.Document.TextLength, "x");
                    Dispatcher.UIThread.RunJobs();
                    keys.Add(key.Elapsed.TotalMilliseconds);
                }
            });
            Console.WriteLine($"big CSV: keystroke {keys.Order().ElementAt(keys.Count / 2):F1} ms (median of {keys.Count}); longest input wait {keyStall:F0} ms");
        }
    }

    // --big-image-mp n: a PNG of about n megapixels opened in the Code Studio, then switched away from and back to.
    public static void BigImage(Window window, CSharpCodeStudioViewModel codeVm, IScriptStorageService storage, StallProbe probe, double megapixels)
    {
        int width = (int)Math.Sqrt(megapixels * 1_000_000 * 1.5);
        int height = (int)(megapixels * 1_000_000 / width);
        var path = Path.Combine(storage.ActiveWorkspaceRootPath, $"big-{megapixels:0.##}mp.png");
        using (var bitmap = new SKBitmap(width, height))
        {
            using (var canvas = new SKCanvas(bitmap))
            using (var paint = new SKPaint { Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(width, height), [SKColors.SteelBlue, SKColors.Orange], SKShaderTileMode.Clamp) })
            {
                canvas.DrawRect(0, 0, width, height, paint);
            }

            using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            using var file = File.Create(path);
            data.SaveTo(file);
        }

        var other = codeVm.OpenTabs.FirstOrDefault(t => t.IsActive);
        var clock = Stopwatch.StartNew();
        double openStall = probe.Measure(() => Pump(codeVm.OpenWorkspaceFileAsync(path)));
        double openMs = clock.Elapsed.TotalMilliseconds;
        double frameMs = Frame(window);
        Console.WriteLine($"big image ({width} x {height}, {new FileInfo(path).Length / 1048576.0:F1} MB): open {openMs:F0} ms + first frame {frameMs:F0} ms; longest input wait {openStall:F0} ms; shown {(codeVm.ActiveImageBitmap != null ? "yes" : "NO")}");

        var imageTab = codeVm.OpenTabs.FirstOrDefault(t => t.IsActive);
        if (other != null && imageTab != null && other != imageTab)
        {
            double switchStall = probe.Measure(() =>
            {
                for (int i = 0; i < 3; i++)
                {
                    Pump(codeVm.SwitchToTabAsync(other));
                    Pump(codeVm.SwitchToTabAsync(imageTab));
                }
            });
            Console.WriteLine($"big image: 3 switches away and back; longest input wait {switchStall:F0} ms");
        }
    }
}
