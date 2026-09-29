using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Material.Icons.Avalonia;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Controls;

public partial class InteractivePlot3DWindow : Window
{
    private readonly Plot3DOptions _options;
    private DispatcherTimer? _autoRotateTimer;

    public InteractivePlot3DWindow() : this(new Plot3DOptions()) { }

    public InteractivePlot3DWindow(Plot3DOptions options)
    {
        _options = options ?? new Plot3DOptions();
        InitializeComponent();
        Title = $"3D Visualizer — {_options.Title}";
        if (WindowTitleText != null) WindowTitleText.Text = _options.Title;
        if (CanvasControl != null) CanvasControl.Options = _options;
        AttachHandlers();
        UpdateStats();
        if (_options.AutoRotate) StartAutoRotateTimer();
    }

    private void AttachHandlers()
    {
        KeyDown += OnWindowKeyDown;
        Closing += (_, _) => StopAutoRotateTimer();
        Bind("WinIsoViewBtn", () => { _options.Camera.SetIsometric(); Repaint(); });
        Bind("WinTopViewBtn", () => { _options.Camera.SetTop(); Repaint(); });
        Bind("WinFrontViewBtn", () => { _options.Camera.SetFront(); Repaint(); });
        Bind("WinSideViewBtn", () => { _options.Camera.SetSide(); Repaint(); });
        Bind("WinResetCameraBtn", () => { _options.Camera.Reset(); Repaint(); });
        Bind("WinAutoRotateBtn", ToggleAutoRotate);
        Bind("WinProjectionToggleBtn", () => { _options.Camera.IsOrthographic = !_options.Camera.IsOrthographic; Repaint(); });
        Bind("WinGridToggleBtn", () => { _options.ShowFloorGrid = !_options.ShowFloorGrid; _options.ShowBoundingBox = _options.ShowFloorGrid; Repaint(); });
        Bind("WinWireframeToggleBtn", () => { _options.Wireframe = !_options.Wireframe; Repaint(); });
        Bind("WinColorMapCycleBtn", CycleColorMap);
        Bind("WinExportHtmlBtn", ExportStandaloneHtml);
        Bind("WinFullscreenToggleBtn", ToggleFullscreen);
        Bind("WinCloseBtn", Close);
    }

    private void Bind(string name, Action act) { var b = this.FindControl<Button>(name); if (b != null) b.Click += (_, _) => act(); }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape: Close(); e.Handled = true; break;
            case Key.F11: ToggleFullscreen(); e.Handled = true; break;
            case Key.Space: ToggleAutoRotate(); e.Handled = true; break;
            case Key.R: _options.Camera.Reset(); Repaint(); e.Handled = true; break;
            case Key.G: _options.ShowFloorGrid = !_options.ShowFloorGrid; _options.ShowBoundingBox = _options.ShowFloorGrid; Repaint(); e.Handled = true; break;
            case Key.P: _options.Camera.IsOrthographic = !_options.Camera.IsOrthographic; Repaint(); e.Handled = true; break;
            case Key.W: _options.Wireframe = !_options.Wireframe; Repaint(); e.Handled = true; break;
            case Key.D1 or Key.NumPad1: _options.Camera.SetIsometric(); Repaint(); e.Handled = true; break;
            case Key.D2 or Key.NumPad2: _options.Camera.SetTop(); Repaint(); e.Handled = true; break;
            case Key.D3 or Key.NumPad3: _options.Camera.SetFront(); Repaint(); e.Handled = true; break;
            case Key.D4 or Key.NumPad4: _options.Camera.SetSide(); Repaint(); e.Handled = true; break;
        }
    }

    private void ToggleFullscreen()
    {
        WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;
        var icon = this.FindControl<MaterialIcon>("WinFullscreenIcon");
        if (icon != null) icon.Kind = WindowState == WindowState.FullScreen ? Material.Icons.MaterialIconKind.FullscreenExit : Material.Icons.MaterialIconKind.Fullscreen;
        Repaint();
    }

    private void ToggleAutoRotate()
    {
        _options.AutoRotate = !_options.AutoRotate;
        if (_options.AutoRotate) StartAutoRotateTimer(); else StopAutoRotateTimer();
    }

    private void CycleColorMap()
    {
        _options.ColorMap = (ColorMapPreset)(((int)_options.ColorMap + 1) % Enum.GetValues<ColorMapPreset>().Length);
        Repaint();
    }

    private void Repaint() { CanvasControl?.InvalidateVisual(); UpdateStats(); }

    private void UpdateStats()
    {
        if (WindowStatsText != null) {
            string camStr = _options.Camera.IsOrthographic ? "Ortho" : "Persp";
            int ptCount = _options.Series.Count > 0 ? _options.Series[0].Points.Count :
                         (_options.Surface != null ? _options.Surface.ResolutionX * _options.Surface.ResolutionY :
                         (_options.Graph != null ? _options.Graph.Nodes.Count : 0));
            WindowStatsText.Text = $"{_options.Type} • {_options.ColorMap} • {camStr} • N={ptCount}";
        }
        if (WinCameraInfoText != null)
            WinCameraInfoText.Text = $"Pitch: {_options.Camera.Pitch:F1}°  Yaw: {_options.Camera.Yaw:F1}°  Dist: {_options.Camera.Distance:F1}";
    }

    private void StartAutoRotateTimer()
    {
        if (_autoRotateTimer != null) return;
        _autoRotateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
        _autoRotateTimer.Tick += (_, _) => {
            if (!_options.AutoRotate) { StopAutoRotateTimer(); return; }
            _options.Camera.Orbit(_options.AutoRotateSpeed, 0);
            Repaint();
        };
        _autoRotateTimer.Start();
    }

    private void StopAutoRotateTimer() { _autoRotateTimer?.Stop(); _autoRotateTimer = null; }

    private async void ExportStandaloneHtml()
    {
        try {
            string html = Plot3DHtmlExporter.GenerateThreeJsHtml(_options);
            if (Clipboard != null) await Clipboard.SetTextAsync(html);
        } catch { }
    }
}
