using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Documentation;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.Docs;

public enum SnippetRunStatus
{
    Idle,
    Running,
    Done,
    Failed,
    Stopped,
    TimedOut
}

/// <summary>
/// One documentation sample's Run button and the output under it: what it printed, and the tables, charts and plots it drew.
/// Cheap until run: it holds no kernel and does nothing until <see cref="RunCommand"/> fires.
/// </summary>
public sealed partial class SnippetRunViewModel : ObservableObject
{
    // What a sample may print or draw before the rest is dropped, so a runaway loop can't fill the page.
    private const int MaxConsoleChars = 200_000;
    private const int MaxOutputs = 50;

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private readonly SnippetRunService _service;
    private readonly DocCodeSnippet _snippet;
    private readonly TimeSpan _timeout;
    private readonly object _consoleGate = new();
    private readonly StringBuilder _console = new();
    private bool _consoleRefreshQueued;
    private CancellationTokenSource? _cts;
    private int _runId;
    private bool _stoppedByUser;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning), nameof(IsOutputOpen), nameof(CanRunNow), nameof(StatusText))]
    private SnippetRunStatus _status;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOutputOpen))]
    private string _consoleText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private string _detail = string.Empty;

    public SnippetRunViewModel(SnippetRunService service, DocCodeSnippet snippet, TimeSpan? timeout = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _snippet = snippet ?? throw new ArgumentNullException(nameof(snippet));
        _timeout = timeout ?? DefaultTimeout;
        Outputs.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsOutputOpen));
            OnPropertyChanged(nameof(HasVisuals));
        };
        _snippet.PropertyChanged += OnSnippetChanged;
    }

    /// <summary>The tables, images, charts and plots the sample drew, in order (what it printed is <see cref="ConsoleText"/>).</summary>
    public ObservableCollection<RichCellOutput> Outputs { get; } = new();

    /// <summary>True when the sample's language can run here and the sample is meant to be run.</summary>
    public bool CanRun => !_snippet.NotRunnable && _service.CanRun(_snippet.Language);

    /// <summary>True when the sample drew a chart, plot or visualizer (which zoom with Ctrl + scroll on this page).</summary>
    public bool HasVisuals => Outputs.Any(o => o.IsVisualKind);

    public bool IsRunning => Status == SnippetRunStatus.Running;

    public bool CanRunNow => CanRun && !IsRunning;

    /// <summary>True once there is anything to show under the sample.</summary>
    public bool IsOutputOpen => Status != SnippetRunStatus.Idle || ConsoleText.Length > 0 || Outputs.Count > 0;

    public string StatusText => Status switch
    {
        SnippetRunStatus.Running => "Running…",
        SnippetRunStatus.Done => string.IsNullOrEmpty(Detail) ? "Done" : $"Done in {Detail}",
        SnippetRunStatus.Failed => string.IsNullOrEmpty(Detail) ? "Failed" : $"Failed: {Detail}",
        SnippetRunStatus.Stopped => "Stopped",
        SnippetRunStatus.TimedOut => $"Stopped after {_timeout.TotalSeconds:0} s",
        _ => string.Empty
    };

    private void OnSnippetChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DocCodeSnippet.Language) or nameof(DocCodeSnippet.NotRunnable))
        {
            OnPropertyChanged(nameof(CanRun));
            OnPropertyChanged(nameof(CanRunNow));
        }
    }

    [RelayCommand]
    public async Task RunAsync()
    {
        if (!CanRunNow || string.IsNullOrWhiteSpace(_snippet.Code)) return;

        Clear();
        var runId = ++_runId;
        var cts = _cts = new CancellationTokenSource();
        _stoppedByUser = false;
        using var timeout = new CancellationTokenSource(_timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, timeout.Token);
        bool TimedOut() => timeout.IsCancellationRequested && !_stoppedByUser;
        Status = SnippetRunStatus.Running;
        var clock = Stopwatch.StartNew();

        try
        {
            var result = await _service.RunAsync(
                _snippet.Language, _snippet.Code,
                text => OnUi(() => AppendConsole(runId, text)),
                output => OnUi(() => AddOutput(runId, output)),
                linked.Token);

            await FlushConsoleAsync(runId);
            if (runId != _runId) return;

            clock.Stop();
            if (result.WasCancelled)
            {
                Status = TimedOut() ? SnippetRunStatus.TimedOut : SnippetRunStatus.Stopped;
            }
            else if (result.Success)
            {
                Detail = FormatElapsed(result.Elapsed > TimeSpan.Zero ? result.Elapsed : clock.Elapsed);
                Status = SnippetRunStatus.Done;
            }
            else
            {
                Detail = FirstLine(result.ErrorMessage);
                Status = SnippetRunStatus.Failed;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (runId != _runId) return;
            Detail = FirstLine(ex.Message);
            Status = SnippetRunStatus.Failed;
        }
        catch (OperationCanceledException)
        {
            if (runId == _runId) Status = TimedOut() ? SnippetRunStatus.TimedOut : SnippetRunStatus.Stopped;
        }
        finally
        {
            if (ReferenceEquals(_cts, cts)) _cts = null;
            cts.Dispose();
        }
    }

    /// <summary>Stops a running sample; a C# sample that doesn't answer is given up on after a few seconds.</summary>
    [RelayCommand]
    public void Stop()
    {
        _stoppedByUser = true;
        CancelRun();
    }

    // The run that owns the source may have ended (and disposed it) a moment before Stop or Clear got here.
    private void CancelRun()
    {
        try
        {
            _cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    /// <summary>Removes the output (and stops the sample if it is running).</summary>
    [RelayCommand]
    public void Clear()
    {
        CancelRun();
        _runId++;
        lock (_consoleGate)
        {
            _console.Clear();
            _consoleRefreshQueued = false;
        }

        ConsoleText = string.Empty;
        Detail = string.Empty;
        Outputs.Clear();
        Status = SnippetRunStatus.Idle;
    }

    private void AppendConsole(int runId, string text)
    {
        if (runId != _runId || string.IsNullOrEmpty(text)) return;
        lock (_consoleGate)
        {
            if (_console.Length >= MaxConsoleChars) return;
            _console.Append(text);
            if (_console.Length >= MaxConsoleChars) _console.Append("\n… output stopped: too much to show here.");
            if (_consoleRefreshQueued) return;
            _consoleRefreshQueued = true;
        }

        // One refresh for a burst of writes (straight away where there is no UI thread).
        if (Avalonia.Application.Current == null) PublishConsole(runId);
        else Dispatcher.UIThread.Post(() => PublishConsole(runId), DispatcherPriority.Background);
    }

    private void PublishConsole(int runId)
    {
        if (runId != _runId) return;
        string text;
        lock (_consoleGate)
        {
            _consoleRefreshQueued = false;
            text = _console.ToString();
        }

        ConsoleText = text;
    }

    private Task FlushConsoleAsync(int runId)
    {
        if (runId == _runId) PublishConsole(runId);
        return Task.CompletedTask;
    }

    private void AddOutput(int runId, RichCellOutput output)
    {
        if (runId != _runId) return;
        switch (output.Kind)
        {
            case CellOutputKind.Text:
                return;
            case CellOutputKind.Error:
                // A visual that couldn't be drawn says why where the sample printed.
                if (!string.IsNullOrEmpty(output.Text)) AppendConsole(runId, $"⚠️ {output.Text}\n");
                return;
        }

        if (Outputs.Count < MaxOutputs) Outputs.Add(output);
    }

    private static string FormatElapsed(TimeSpan elapsed) =>
        elapsed.TotalSeconds >= 1 ? $"{elapsed.TotalSeconds:0.0} s" : $"{elapsed.TotalMilliseconds:0} ms";

    private static string FirstLine(string? text) =>
        string.IsNullOrWhiteSpace(text) ? string.Empty : text.Split('\n', 2)[0].Trim();

    // The kernel reports from its own threads; the page is only touched on the UI thread (directly, where there is none).
    private static void OnUi(Action action)
    {
        if (Avalonia.Application.Current == null || Dispatcher.UIThread.CheckAccess()) action();
        else Dispatcher.UIThread.Post(action);
    }
}
