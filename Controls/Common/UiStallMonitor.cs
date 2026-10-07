using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Avalonia.Threading;
using PdfEditorApp.Plugins.CSharpEditor.Services.Activities;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Common;

/// <summary>
/// Notices when the UI thread stops answering. A background thread keeps one tiny job queued at input priority; when it
/// waits longer than <see cref="Threshold"/> the stall is written to the debug log with the work that was running
/// (from <see cref="IActivityService"/>), so a hang is seen while developing instead of reported by a user later.
/// The studio host starts it in Debug builds only; it costs one queued job every quarter second.
/// </summary>
public sealed class UiStallMonitor : IDisposable
{
    /// <summary>A UI-thread stall longer than this is reported.</summary>
    public static readonly TimeSpan Threshold = TimeSpan.FromMilliseconds(200);

    private readonly IActivityService _activities;
    private readonly Action<string> _report;
    private readonly Thread _thread;
    private volatile bool _stop;

    public UiStallMonitor(IActivityService activities, Action<string>? report = null)
    {
        _activities = activities;
        _report = report ?? (message => Debug.WriteLine(message));
        _thread = new Thread(Watch) { IsBackground = true, Name = "ui-stall-monitor" };
        _thread.Start();
    }

    /// <summary>The longest stall seen so far.</summary>
    public TimeSpan LongestStall { get; private set; }

    private void Watch()
    {
        using var answered = new ManualResetEventSlim(false);
        while (!_stop)
        {
            answered.Reset();
            long posted = Stopwatch.GetTimestamp();
            Dispatcher.UIThread.Post(() => answered.Set(), DispatcherPriority.Input);

            // Wait in steps, so a stall is reported once, with what was running while it lasted.
            bool reported = false;
            string? runningDuringStall = null;
            while (!answered.Wait(50))
            {
                if (_stop) return;
                var waited = Stopwatch.GetElapsedTime(posted);
                if (!reported && waited > Threshold)
                {
                    runningDuringStall = DescribeRunningWork();
                    reported = true;
                }
            }

            var stall = Stopwatch.GetElapsedTime(posted);
            if (stall > LongestStall) LongestStall = stall;
            if (reported)
            {
                _report($"[UiStallMonitor] The UI thread was busy for {stall.TotalMilliseconds:F0} ms (input waited that long). Running: {runningDuringStall ?? "nothing reported"}.");
            }

            Thread.Sleep(250);
        }
    }

    private string? DescribeRunningWork()
    {
        var running = _activities.Snapshot();
        return running.Count == 0 ? null : string.Join("; ", running.Select(a => string.IsNullOrEmpty(a.Detail) ? a.Title : $"{a.Title} ({a.Detail})"));
    }

    public void Dispose()
    {
        _stop = true;
    }
}
