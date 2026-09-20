using System;
using System.Collections.Generic;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

public partial class VisualizerSequence : ObservableObject
{
    private DispatcherTimer? _playbackTimer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentStep))]
    [NotifyPropertyChangedFor(nameof(CanStepBack))]
    [NotifyPropertyChangedFor(nameof(CanStepForward))]
    [NotifyPropertyChangedFor(nameof(StepProgressText))]
    [NotifyPropertyChangedFor(nameof(CurrentDescription))]
    private int _currentIndex;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private double _playbackSpeed = 1.0; // 0.5x, 1.0x, 2.0x, 4.0x

    public List<VisualizerStep> Steps { get; set; } = new();

    public int TotalSteps => Steps.Count;
    public bool HasSteps => Steps.Count > 0;

    public VisualizerStep? CurrentStep =>
        (CurrentIndex >= 0 && CurrentIndex < Steps.Count) ? Steps[CurrentIndex] : null;

    public bool CanStepBack => CurrentIndex > 0;
    public bool CanStepForward => CurrentIndex < Steps.Count - 1;

    public string StepProgressText =>
        TotalSteps == 0 ? "0 / 0" : $"{CurrentIndex + 1} / {TotalSteps}";

    public string CurrentDescription =>
        CurrentStep?.Description ?? (TotalSteps > 0 ? "Initial State" : "No steps recorded");

    public event EventHandler<int>? StepChanged;

    public VisualizerSequence() { }

    public VisualizerSequence(IEnumerable<VisualizerStep> steps)
    {
        Steps = new List<VisualizerStep>(steps);
        if (Steps.Count > 0)
        {
            CurrentIndex = 0;
        }
    }

    public void AddStep(VisualizerStep step)
    {
        step.StepIndex = Steps.Count;
        Steps.Add(step);
        OnPropertyChanged(nameof(TotalSteps));
        OnPropertyChanged(nameof(HasSteps));
        OnPropertyChanged(nameof(CanStepForward));
        OnPropertyChanged(nameof(StepProgressText));
    }

    public void SeekStep(int index)
    {
        if (Steps.Count == 0) return;
        int clamped = Math.Clamp(index, 0, Steps.Count - 1);
        if (CurrentIndex != clamped)
        {
            CurrentIndex = clamped;
            StepChanged?.Invoke(this, CurrentIndex);
        }
    }

    public void NextStep()
    {
        if (CurrentIndex < Steps.Count - 1)
        {
            SeekStep(CurrentIndex + 1);
        }
        else if (IsPlaying)
        {
            Pause();
        }
    }

    public void PrevStep()
    {
        if (CurrentIndex > 0)
        {
            SeekStep(CurrentIndex - 1);
        }
    }

    public void FirstStep() => SeekStep(0);

    public void LastStep() => SeekStep(Steps.Count - 1);

    public void TogglePlay()
    {
        if (IsPlaying)
        {
            Pause();
        }
        else
        {
            Play();
        }
    }

    public void Play()
    {
        if (Steps.Count <= 1) return;
        if (CurrentIndex >= Steps.Count - 1)
        {
            SeekStep(0);
        }

        IsPlaying = true;
        StartTimer();
    }

    public void Pause()
    {
        IsPlaying = false;
        StopTimer();
    }

    private void StartTimer()
    {
        StopTimer();
        int intervalMs = Math.Max(50, (int)(600 / Math.Max(0.1, PlaybackSpeed)));

        try
        {
            _playbackTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(intervalMs)
            };
            _playbackTimer.Tick += OnTimerTick;
            _playbackTimer.Start();
        }
        catch
        {
            // Headless unit tests where DispatcherTimer may not have an active platform loop
        }
    }

    private void StopTimer()
    {
        if (_playbackTimer != null)
        {
            _playbackTimer.Stop();
            _playbackTimer.Tick -= OnTimerTick;
            _playbackTimer = null;
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (!IsPlaying) return;

        if (CurrentIndex < Steps.Count - 1)
        {
            NextStep();
        }
        else
        {
            Pause();
        }
    }

    partial void OnPlaybackSpeedChanged(double value)
    {
        if (IsPlaying)
        {
            StartTimer();
        }
    }
}
