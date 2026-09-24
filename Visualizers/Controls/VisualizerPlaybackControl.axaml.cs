using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Material.Icons;
using Material.Icons.Avalonia;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Controls;

public sealed record WatchStripRow(string Name, string Summary, IReadOnlyList<WatchStripCell> Cells);

public sealed record WatchStripCell(string Text, bool IsAdded, bool IsMarker)
{
    public static WatchStripCell Marker(string text) => new(text, IsAdded: false, IsMarker: true);
}

public partial class VisualizerPlaybackControl : UserControl
{
    public static readonly StyledProperty<VisualizerSequence?> SequenceProperty =
        AvaloniaProperty.Register<VisualizerPlaybackControl, VisualizerSequence?>(nameof(Sequence));

    private bool _isUpdatingSlider;

    public VisualizerSequence? Sequence
    {
        get => GetValue(SequenceProperty);
        set => SetValue(SequenceProperty, value);
    }

    static VisualizerPlaybackControl()
    {
        SequenceProperty.Changed.AddClassHandler<VisualizerPlaybackControl>((ctrl, e) =>
            ctrl.OnSequenceChanged(e.OldValue as VisualizerSequence, e.NewValue as VisualizerSequence));
    }

    public VisualizerPlaybackControl()
    {
        try { InitializeComponent(); WireEvents(); }
        catch { /* Headless test runner */ }
    }

    private void OnSequenceChanged(VisualizerSequence? oldSeq, VisualizerSequence? newSeq)
    {
        if (oldSeq != null) { oldSeq.PropertyChanged -= OnSeqChanged; oldSeq.StepChanged -= OnStepChanged; }
        if (newSeq != null) { newSeq.PropertyChanged += OnSeqChanged; newSeq.StepChanged += OnStepChanged; }
        UpdateUi();
    }

    private void OnSeqChanged(object? s, PropertyChangedEventArgs e) => UpdateUi();
    private void OnStepChanged(object? s, int idx) => UpdateUi();

    private void WireEvents()
    {
        BindBtn("FirstBtn", () => Sequence?.FirstStep());
        BindBtn("PrevBtn", () => Sequence?.PrevStep());
        BindBtn("PlayPauseBtn", () => Sequence?.TogglePlay());
        BindBtn("NextBtn", () => Sequence?.NextStep());
        BindBtn("LastBtn", () => Sequence?.LastStep());
        BindBtn("SpeedBtn", CycleSpeed);
        BindBtn("StepLineBtn", RevealStepLine);

        var slider = this.FindControl<Slider>("StepSlider");
        if (slider != null)
        {
            slider.ValueChanged += (_, e) =>
            {
                if (!_isUpdatingSlider && Sequence != null)
                    Sequence.SeekStep((int)e.NewValue);
            };
        }

        KeyDown += OnControlKeyDown;
    }

    private void BindBtn(string name, Action act)
    {
        var btn = this.FindControl<Button>(name);
        if (btn != null) btn.Click += (_, _) => act();
    }

    private void OnControlKeyDown(object? sender, KeyEventArgs e)
    {
        if (Sequence == null) return;
        if (e.Key == Key.Left || e.Key == Key.OemOpenBrackets) { Sequence.PrevStep(); e.Handled = true; }
        else if (e.Key == Key.Right || e.Key == Key.OemCloseBrackets) { Sequence.NextStep(); e.Handled = true; }
        else if (e.Key == Key.Space) { Sequence.TogglePlay(); e.Handled = true; }
    }

    internal void SetStepLineTip(string tip)
    {
        if (this.FindControl<Button>("StepLineBtn") is { } button) ToolTip.SetTip(button, tip);
    }

    private void RevealStepLine()
    {
        var step = Sequence?.CurrentStep;
        if (step is { SourceLine: > 0 })
        {
            RaiseEvent(new VisualizerStepLineEventArgs(step.SourceLine, step.SourceFile, reveal: true));
        }
    }

    private void CycleSpeed()
    {
        if (Sequence == null) return;
        Sequence.PlaybackSpeed = Sequence.PlaybackSpeed switch { <= 0.5 => 1.0, <= 1.0 => 2.0, <= 2.0 => 4.0, _ => 0.5 };
        UpdateUi();
    }

    private void UpdateUi()
    {
        if (Sequence == null) return;
        var playIcon = this.FindControl<MaterialIcon>("PlayPauseIcon");
        var descText = this.FindControl<TextBlock>("StepDescriptionText");
        var counterText = this.FindControl<TextBlock>("StepCounterText");
        var slider = this.FindControl<Slider>("StepSlider");
        var speedText = this.FindControl<TextBlock>("SpeedText");
        var auxPanel = this.FindControl<StackPanel>("AuxInfoPanel");
        var lineBtn = this.FindControl<Button>("StepLineBtn");
        var lineText = this.FindControl<TextBlock>("StepLineText");

        if (playIcon != null) playIcon.Kind = Sequence.IsPlaying ? MaterialIconKind.Pause : MaterialIconKind.Play;
        if (descText != null) descText.Text = Sequence.CurrentDescription;

        int sourceLine = Sequence.CurrentStep?.SourceLine ?? 0;
        if (lineBtn != null) lineBtn.IsVisible = sourceLine > 0;
        if (lineText != null) lineText.Text = $"Ln {sourceLine}";
        if (counterText != null) counterText.Text = Sequence.StepProgressText;
        if (speedText != null) speedText.Text = $"{Sequence.PlaybackSpeed:0.#}x";

        if (slider != null)
        {
            _isUpdatingSlider = true;
            slider.Maximum = Math.Max(0, Sequence.TotalSteps - 1);
            slider.Value = Sequence.CurrentIndex;
            slider.IsEnabled = Sequence.TotalSteps > 1;
            _isUpdatingSlider = false;
        }

        if (auxPanel != null)
        {
            auxPanel.Children.Clear();
            var step = Sequence.CurrentStep;
            if (step != null)
            {
                foreach (var kvp in step.AuxiliaryInfo)
                    auxPanel.Children.Add(CreateBadge(kvp.Key, kvp.Value));
            }
        }

        var watchList = this.FindControl<ItemsControl>("WatchList");
        if (watchList != null)
        {
            var rows = BuildWatchRows(Sequence);
            watchList.ItemsSource = rows;
            watchList.IsVisible = rows.Count > 0;
        }
    }

    internal static List<WatchStripRow> BuildWatchRows(VisualizerSequence sequence)
    {
        var step = sequence.CurrentStep;
        if (step == null || step.Watches.Count == 0) return new List<WatchStripRow>();

        var previous = sequence.CurrentIndex > 0 ? sequence.Steps[sequence.CurrentIndex - 1].Watches : null;
        var rows = new List<WatchStripRow>();

        foreach (var watch in step.Watches)
        {
            var added = StepChanges.AddedWatchItems(previous?.FirstOrDefault(p => p.Name == watch.Name), watch);
            var (leading, trailing) = watch.Kind switch
            {
                WatchKind.Queue => ("front", "back"),
                WatchKind.Stack => ("top", null),
                WatchKind.PriorityQueue => ("next", null),
                _ => ((string?)null, (string?)null)
            };

            var cells = new List<WatchStripCell>();
            if (watch.Count == 0)
            {
                cells.Add(WatchStripCell.Marker("empty"));
            }
            else
            {
                if (leading != null) cells.Add(WatchStripCell.Marker(leading));
                for (int i = 0; i < watch.Items.Count; i++)
                {
                    cells.Add(new WatchStripCell(watch.Items[i], added[i], IsMarker: false));
                }
                int hidden = watch.Count - watch.Items.Count;
                if (hidden > 0) cells.Add(WatchStripCell.Marker($"+{hidden} more"));
                if (trailing != null) cells.Add(WatchStripCell.Marker(trailing));
            }

            string summary = watch.Kind == WatchKind.Value
                ? watch.Name
                : $"{watch.Kind} • {watch.Count} item{(watch.Count == 1 ? "" : "s")}";
            rows.Add(new WatchStripRow(watch.Name, summary, cells));
        }

        return rows;
    }

    private static Border CreateBadge(string label, string val) => new()
    {
        Background = new SolidColorBrush(Color.FromArgb(50, 56, 189, 248)),
        BorderBrush = new SolidColorBrush(Color.FromArgb(120, 56, 189, 248)),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(3),
        Padding = new Thickness(5, 1),
        Child = new TextBlock
        {
            Text = $"{label}: {val}",
            FontSize = 9.5,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248))
        }
    };
}
