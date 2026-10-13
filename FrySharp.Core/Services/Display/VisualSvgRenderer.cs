using System.Globalization;
using System.Net;
using System.Text;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Spec;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

/// <summary>
/// Renders VisualSpec (2D charts, 3D parametric surfaces, visualizers) into responsive, high-performance,
/// dark-mode HTML/SVG markup for webview-based hosts (such as VS Code notebook output, Jupyter, browser).
/// Requires zero client-side scripts, zero external CDNs, and zero WebAssembly overhead.
/// </summary>
public static class VisualSvgRenderer
{
    private static readonly string[] Palette =
    [
        "#58a6ff", // Blue / Cyan
        "#3fb950", // Green
        "#d29922", // Amber
        "#f85149", // Coral / Red
        "#bc8cff", // Purple
        "#2dd4bf", // Teal
        "#fb7185", // Rose
        "#38bdf8"  // Sky
    ];

    public static bool TryRender(VisualSpec spec, out string html)
    {
        html = string.Empty;
        if (spec == null) return false;

        try
        {
            var sb = new StringBuilder(4096);
            var title = WebUtility.HtmlEncode(spec.Title ?? spec.Family.ToString());
            var subtitle = !string.IsNullOrEmpty(spec.Subtitle) ? WebUtility.HtmlEncode(spec.Subtitle) : null;
            var kindBadge = spec switch
            {
                ChartSpec c => c.Kind.ToString().ToUpperInvariant(),
                Plot3DSpec p => p.Kind.ToString().ToUpperInvariant(),
                VisualizerSpec v => v.Kind.ToString().ToUpperInvariant(),
                _ => spec.Family.ToString().ToUpperInvariant()
            };

            sb.Append("<div class=\"fry-visual-card\" style=\"font-family:system-ui,-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;");
            sb.Append("background:#0d1117;border:1px solid #30363d;border-radius:10px;padding:14px;margin:8px 0;max-width:840px;box-shadow:0 4px 16px rgba(0,0,0,0.35);overflow-x:auto;\">");

            // Header
            sb.Append("<div style=\"display:flex;align-items:center;justify-content:space-between;margin-bottom:10px;gap:12px;\">");
            sb.Append("<div>");
            sb.Append($"<div style=\"font-weight:600;font-size:13.5px;color:#58a6ff;letter-spacing:0.2px;\">📊 {title}</div>");
            if (subtitle != null)
            {
                sb.Append($"<div style=\"font-size:11.5px;color:#8b949e;margin-top:2px;\">{subtitle}</div>");
            }
            sb.Append("</div>");
            sb.Append($"<div style=\"font-size:10.5px;font-weight:600;color:#8b949e;background:#161b22;padding:3px 9px;border-radius:12px;border:1px solid #30363d;letter-spacing:0.5px;\">{kindBadge}</div>");
            sb.Append("</div>");

            // Render SVG based on Spec Type
            switch (spec)
            {
                case ChartSpec chart:
                    RenderChartSvg(sb, chart);
                    break;
                case Plot3DSpec plot3d:
                    RenderPlot3DSvg(sb, plot3d);
                    break;
                case VisualizerSpec viz:
                    RenderVisualizerSvg(sb, viz);
                    break;
                default:
                    RenderFallbackSvg(sb, spec);
                    break;
            }

            sb.Append("</div>");
            html = sb.ToString();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void RenderChartSvg(StringBuilder sb, ChartSpec chart)
    {
        if (chart.Kind is ChartType.Pie or ChartType.Donut)
        {
            RenderPieChartSvg(sb, chart);
            return;
        }

        const double width = 760;
        const double height = 360;
        const double plotLeft = 60;
        const double plotRight = 720;
        const double plotTop = 30;
        const double plotBottom = 300;
        const double plotWidth = plotRight - plotLeft;
        const double plotHeight = plotBottom - plotTop;

        // Gather all numeric data bounds
        double minY = double.MaxValue;
        double maxY = double.MinValue;
        int maxPoints = 0;

        foreach (var s in chart.Series)
        {
            maxPoints = Math.Max(maxPoints, s.Y.Count);
            foreach (var y in s.Y)
            {
                if (y.HasValue && double.IsFinite(y.Value))
                {
                    minY = Math.Min(minY, y.Value);
                    maxY = Math.Max(maxY, y.Value);
                }
            }
        }

        if (minY > maxY) { minY = 0; maxY = 100; }
        if (Math.Abs(maxY - minY) < 1e-9) { maxY += 10; minY -= 10; }
        if (minY > 0 && chart.Kind == ChartType.Bar) minY = 0; // Bars start at 0

        // SVG Container
        sb.Append($"<svg viewBox=\"0 0 {width} {height}\" width=\"100%\" height=\"auto\" style=\"display:block;border-radius:6px;background:#161b22;\" xmlns=\"http://www.w3.org/2000/svg\">");

        // Definitions: Gradients
        sb.Append("<defs>");
        for (int i = 0; i < Math.Min(chart.Series.Count, Palette.Length); i++)
        {
            var color = chart.Series[i].Color ?? Palette[i % Palette.Length];
            sb.Append($"<linearGradient id=\"area-grad-{i}\" x1=\"0%\" y1=\"0%\" x2=\"0%\" y2=\"100%\">");
            sb.Append($"<stop offset=\"0%\" stop-color=\"{color}\" stop-opacity=\"0.35\"/>");
            sb.Append($"<stop offset=\"100%\" stop-color=\"{color}\" stop-opacity=\"0.02\"/>");
            sb.Append("</linearGradient>");
        }
        sb.Append("</defs>");

        // Grid lines & Y Axis Ticks (5 horizontal lines)
        const int tickCount = 5;
        for (int i = 0; i <= tickCount; i++)
        {
            double frac = (double)i / tickCount;
            double yVal = maxY - (frac * (maxY - minY));
            double yPx = plotTop + (frac * plotHeight);

            sb.Append($"<line x1=\"{plotLeft}\" y1=\"{yPx:F1}\" x2=\"{plotRight}\" y2=\"{yPx:F1}\" stroke=\"#21262d\" stroke-width=\"1\" stroke-dasharray=\"3,3\"/>");
            sb.Append($"<text x=\"{plotLeft - 8}\" y=\"{yPx + 4:F1}\" fill=\"#8b949e\" font-size=\"10.5\" font-family=\"monospace\" text-anchor=\"end\">{FormatNumber(yVal)}</text>");
        }

        // Draw Series
        bool isBar = chart.Kind == ChartType.Bar;
        bool isArea = chart.Kind == ChartType.Area;

        if (isBar && maxPoints > 0)
        {
            double groupWidth = plotWidth / maxPoints;
            int seriesCount = chart.Series.Count;
            double barWidth = Math.Max(4, (groupWidth * 0.7) / seriesCount);

            for (int sIdx = 0; sIdx < seriesCount; sIdx++)
            {
                var s = chart.Series[sIdx];
                var color = s.Color ?? Palette[sIdx % Palette.Length];

                for (int pIdx = 0; pIdx < s.Y.Count; pIdx++)
                {
                    var y = s.Y[pIdx];
                    if (!y.HasValue || !double.IsFinite(y.Value)) continue;

                    double groupStart = plotLeft + (pIdx * groupWidth) + (groupWidth * 0.15);
                    double barX = groupStart + (sIdx * barWidth);
                    double zeroY = plotTop + ((maxY - 0) / (maxY - minY) * plotHeight);
                    zeroY = Math.Clamp(zeroY, plotTop, plotBottom);
                    double barY = plotTop + ((maxY - y.Value) / (maxY - minY) * plotHeight);
                    double h = Math.Abs(zeroY - barY);
                    double top = Math.Min(zeroY, barY);

                    var label = (s.Labels != null && pIdx < s.Labels.Count) ? s.Labels[pIdx] : $"#{pIdx + 1}";
                    sb.Append($"<rect x=\"{barX:F1}\" y=\"{top:F1}\" width=\"{barWidth * 0.9:F1}\" height=\"{h:F1}\" fill=\"{color}\" rx=\"3\">");
                    sb.Append($"<title>{WebUtility.HtmlEncode(s.Name ?? "Series")}: {y.Value:N1} ({label})</title>");
                    sb.Append("</rect>");
                }
            }
        }
        else if (maxPoints > 1)
        {
            for (int sIdx = 0; sIdx < chart.Series.Count; sIdx++)
            {
                var s = chart.Series[sIdx];
                var color = s.Color ?? Palette[sIdx % Palette.Length];
                var points = new List<(double X, double Y)>();

                for (int pIdx = 0; pIdx < s.Y.Count; pIdx++)
                {
                    var y = s.Y[pIdx];
                    if (!y.HasValue || !double.IsFinite(y.Value)) continue;

                    double px = plotLeft + ((double)pIdx / (maxPoints - 1) * plotWidth);
                    double py = plotTop + ((maxY - y.Value) / (maxY - minY) * plotHeight);
                    points.Add((px, py));
                }

                if (points.Count < 2) continue;

                // If Area chart, draw filled path first
                if (isArea)
                {
                    var areaSb = new StringBuilder();
                    areaSb.Append($"M {points[0].X:F1} {plotBottom:F1} L {points[0].X:F1} {points[0].Y:F1} ");
                    for (int i = 1; i < points.Count; i++)
                    {
                        areaSb.Append($"L {points[i].X:F1} {points[i].Y:F1} ");
                    }
                    areaSb.Append($"L {points[^1].X:F1} {plotBottom:F1} Z");
                    sb.Append($"<path d=\"{areaSb}\" fill=\"url(#area-grad-{sIdx})\" />");
                }

                // Line Path
                var pathSb = new StringBuilder();
                pathSb.Append($"M {points[0].X:F1} {points[0].Y:F1} ");
                for (int i = 1; i < points.Count; i++)
                {
                    pathSb.Append($"L {points[i].X:F1} {points[i].Y:F1} ");
                }

                sb.Append($"<path d=\"{pathSb}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"2.5\" stroke-linejoin=\"round\" stroke-linecap=\"round\"/>");

                // Draw Dots & Tooltips
                for (int i = 0; i < points.Count; i++)
                {
                    var yVal = s.Y[i]!.Value;
                    var label = (s.Labels != null && i < s.Labels.Count) ? s.Labels[i] : $"#{i + 1}";
                    sb.Append($"<circle cx=\"{points[i].X:F1}\" cy=\"{points[i].Y:F1}\" r=\"3.5\" fill=\"{color}\" stroke=\"#161b22\" stroke-width=\"1.5\">");
                    sb.Append($"<title>{WebUtility.HtmlEncode(s.Name ?? "Series")}: {yVal:N2} ({label})</title>");
                    sb.Append("</circle>");
                }
            }
        }

        // X-Axis Baseline
        sb.Append($"<line x1=\"{plotLeft}\" y1=\"{plotBottom}\" x2=\"{plotRight}\" y2=\"{plotBottom}\" stroke=\"#30363d\" stroke-width=\"1.5\"/>");

        // Legend at bottom
        RenderLegend(sb, chart.Series.Select((s, i) => (s.Name ?? $"Series {i + 1}", s.Color ?? Palette[i % Palette.Length])), 335);

        sb.Append("</svg>");
    }

    private static void RenderPieChartSvg(StringBuilder sb, ChartSpec chart)
    {
        const double width = 760;
        const double height = 360;
        const double cx = 280;
        const double cy = 180;
        const double radius = 130;
        double cutout = (chart.Kind == ChartType.Donut ? 65 : 0);

        var firstSeries = chart.Series.FirstOrDefault();
        if (firstSeries == null || firstSeries.Y.Count == 0)
        {
            RenderFallbackSvg(sb, chart);
            return;
        }

        double total = firstSeries.Y.Where(y => y.HasValue && y.Value > 0).Sum(y => y!.Value);
        if (total <= 0) total = 1;

        var uid = "fry_pie_" + Guid.NewGuid().ToString("N")[..8];

        sb.Append($$"""
        <div id="{{uid}}" style="position:relative;">
          <style>
            #{{uid}} path.fry-pie-slice {
              transform-origin: {{cx}}px {{cy}}px;
              transition: transform 0.2s cubic-bezier(0.4, 0, 0.2, 1), opacity 0.2s;
              cursor: pointer;
            }
            #{{uid}} path.fry-pie-slice:hover {
              transform: scale(1.05);
              filter: drop-shadow(0 2px 8px rgba(0,0,0,0.5));
            }
            #{{uid}} .fry-pie-legend-item {
              cursor: pointer; transition: opacity 0.15s;
            }
            #{{uid}} .fry-pie-legend-item:hover {
              opacity: 0.75;
            }
          </style>
          <svg viewBox="0 0 {{width}} {{height}}" width="100%" height="auto" style="display:block;border-radius:6px;background:#161b22;" xmlns="http://www.w3.org/2000/svg">
        """);

        double currentAngle = -Math.PI / 2.0;
        var legendItems = new List<(string Name, string Color, double Value, double Percent, int Index)>();

        for (int i = 0; i < firstSeries.Y.Count; i++)
        {
            var val = firstSeries.Y[i];
            if (!val.HasValue || val.Value <= 0) continue;

            double sliceAngle = (val.Value / total) * 2 * Math.PI;
            double nextAngle = currentAngle + sliceAngle;
            var color = Palette[i % Palette.Length];
            var label = (firstSeries.Labels != null && i < firstSeries.Labels.Count && !string.IsNullOrEmpty(firstSeries.Labels[i])) ? firstSeries.Labels[i]! : $"Item {i + 1}";
            double percent = (val.Value / total) * 100.0;
            legendItems.Add((label, color, val.Value, percent, i));

            double x1 = cx + (radius * Math.Cos(currentAngle));
            double y1 = cy + (radius * Math.Sin(currentAngle));
            double x2 = cx + (radius * Math.Cos(nextAngle));
            double y2 = cy + (radius * Math.Sin(nextAngle));
            int largeArc = sliceAngle > Math.PI ? 1 : 0;

            if (cutout > 0)
            {
                double ix1 = cx + (cutout * Math.Cos(nextAngle));
                double iy1 = cy + (cutout * Math.Sin(nextAngle));
                double ix2 = cx + (cutout * Math.Cos(currentAngle));
                double iy2 = cy + (cutout * Math.Sin(currentAngle));

                sb.Append($$"""
                <path class="fry-pie-slice" data-slice="{{i}}" d="M {{x1:F1}} {{y1:F1}} A {{radius}} {{radius}} 0 {{largeArc}} 1 {{x2:F1}} {{y2:F1}} L {{ix1:F1}} {{iy1:F1}} A {{cutout}} {{cutout}} 0 {{largeArc}} 0 {{ix2:F1}} {{iy2:F1}} Z" fill="{{color}}" stroke="#161b22" stroke-width="2">
                  <title>{{WebUtility.HtmlEncode(label)}}: {{val.Value:N1}} ({{percent:F1}}%)</title>
                </path>
                """);
            }
            else
            {
                sb.Append($$"""
                <path class="fry-pie-slice" data-slice="{{i}}" d="M {{cx}} {{cy}} L {{x1:F1}} {{y1:F1}} A {{radius}} {{radius}} 0 {{largeArc}} 1 {{x2:F1}} {{y2:F1}} Z" fill="{{color}}" stroke="#161b22" stroke-width="2">
                  <title>{{WebUtility.HtmlEncode(label)}}: {{val.Value:N1}} ({{percent:F1}}%)</title>
                </path>
                """);
            }

            currentAngle = nextAngle;
        }

        // Side Legend (X: 470, Y: 60)
        double legY = 60;
        foreach (var (name, color, val, pct, idx) in legendItems.Take(8))
        {
            sb.Append($$"""
            <g class="fry-pie-legend-item" data-slice="{{idx}}">
              <rect x="470" y="{{legY}}" width="12" height="12" rx="2" fill="{{color}}"/>
              <text x="490" y="{{legY + 10}}" fill="#c9d1d9" font-size="11.5" font-family="system-ui,sans-serif">{{WebUtility.HtmlEncode(name)}}: {{pct:F1}}%</text>
            </g>
            """);
            legY += 24;
        }

        sb.Append("</svg></div>");
    }

    private static void RenderPlot3DSvg(StringBuilder sb, Plot3DSpec plot)
    {
        const double width = 760;
        const double height = 380;
        const double cx = 380;
        const double cy = 190;

        var uid = "fry_3d_" + Guid.NewGuid().ToString("N")[..8];

        sb.Append($$"""
        <div id="{{uid}}" style="position:relative;user-select:none;">
          <div style="display:flex;align-items:center;justify-content:flex-end;gap:12px;margin-bottom:8px;font-size:11px;color:#8b949e;">
            <div style="display:flex;align-items:center;gap:6px;">
              <span>🔄 Rotate</span>
              <input type="range" class="fry-3d-az-slider" min="0" max="360" value="30" style="width:90px;height:4px;accent-color:#58a6ff;cursor:pointer;" oninput="window.fryRotate3D && window.fryRotate3D('{{uid}}', this.value, null)" />
            </div>
            <div style="display:flex;align-items:center;gap:6px;">
              <span>📐 Tilt</span>
              <input type="range" class="fry-3d-el-slider" min="15" max="80" value="35" style="width:70px;height:4px;accent-color:#58a6ff;cursor:pointer;" oninput="window.fryRotate3D && window.fryRotate3D('{{uid}}', null, this.value)" />
            </div>
            <span style="opacity:0.6;font-size:10px;">(Drag on canvas to rotate)</span>
          </div>

          <svg class="fry-3d-canvas" viewBox="0 0 {{width}} {{height}}" width="100%" height="auto" style="display:block;border-radius:6px;background:#161b22;cursor:grab;" xmlns="http://www.w3.org/2000/svg">
            <g class="fry-3d-mesh"></g>
          </svg>
        """);

        if (plot.Surface != null && plot.Surface.Z.Count > 0)
        {
            var zGrid = plot.Surface.Z;
            int rows = zGrid.Count;
            int cols = zGrid[0].Count;

            double minZ = double.MaxValue;
            double maxZ = double.MinValue;
            foreach (var row in zGrid)
            {
                foreach (var v in row)
                {
                    if (v.HasValue)
                    {
                        minZ = Math.Min(minZ, v.Value);
                        maxZ = Math.Max(maxZ, v.Value);
                    }
                }
            }
            if (minZ >= maxZ) { minZ = -1; maxZ = 1; }

            // Serialize surface points for interactive JS projection
            var gridJson = new StringBuilder("[");
            for (int r = 0; r < rows; r++)
            {
                gridJson.Append('[');
                for (int c = 0; c < cols; c++)
                {
                    var zv = zGrid[r][c] ?? 0;
                    double u = ((double)c / (cols - 1) * 2.0) - 1.0;
                    double v = ((double)r / (rows - 1) * 2.0) - 1.0;
                    double zNorm = (zv - minZ) / (maxZ - minZ);
                    gridJson.Append($"[{u:F3},{v:F3},{zNorm:F3}],");
                }
                gridJson.Append("],");
            }
            gridJson.Append(']');

            sb.Append($$"""
            <script>
            (function() {
              const root = document.getElementById('{{uid}}');
              if (!root) return;
              const grid = {{gridJson}};
              const rows = grid.length;
              const cols = grid[0].length;
              const svgMesh = root.querySelector('.fry-3d-mesh');
              const canvas = root.querySelector('.fry-3d-canvas');

              let azDeg = 30;
              let elDeg = 35;
              const cx = {{cx}};
              const cy = {{cy}};
              const scaleX = 180.0 * 0.7;
              const scaleY = 90.0 * 0.7;
              const scaleZ = 120.0 * 0.6;

              function colorMap(t) {
                const hue = 240.0 - (Math.max(0, Math.min(1, t)) * 195.0);
                return 'hsl(' + Math.round(hue) + ', 85%, 55%)';
              }

              function render(az, el) {
                const azRad = az * Math.PI / 180.0;
                const elRad = el * Math.PI / 180.0;
                const cosAz = Math.cos(azRad);
                const sinAz = Math.sin(azRad);
                const sinEl = Math.sin(elRad);

                function project(u, v, z) {
                  const zScaled = (z * 1.8) - 0.9;
                  const xRot = (u * cosAz - v * sinAz);
                  const yRot = (u * sinAz + v * cosAz);
                  const px = cx + xRot * scaleX;
                  const py = cy + (yRot * sinEl * scaleY) - (zScaled * scaleZ);
                  return [px, py];
                }

                let svgContent = '';

                // Row curves
                for (let r = 0; r < rows; r++) {
                  let path = '';
                  let rowZSum = 0;
                  for (let c = 0; c < cols; c++) {
                    const pt = grid[r][c];
                    rowZSum += pt[2];
                    const p = project(pt[0], pt[1], pt[2]);
                    path += (c === 0 ? 'M ' : 'L ') + p[0].toFixed(1) + ' ' + p[1].toFixed(1) + ' ';
                  }
                  const col = colorMap(rowZSum / cols);
                  svgContent += '<path d="' + path + '" fill="none" stroke="' + col + '" stroke-width="1.4" opacity="0.85" />';
                }

                // Col curves (mesh cross lines)
                const colStep = Math.max(1, Math.floor(cols / 25));
                for (let c = 0; c < cols; c += colStep) {
                  let path = '';
                  for (let r = 0; r < rows; r++) {
                    const pt = grid[r][c];
                    const p = project(pt[0], pt[1], pt[2]);
                    path += (r === 0 ? 'M ' : 'L ') + p[0].toFixed(1) + ' ' + p[1].toFixed(1) + ' ';
                  }
                  svgContent += '<path d="' + path + '" fill="none" stroke="#58a6ff" stroke-width="0.8" opacity="0.35" />';
                }

                svgMesh.innerHTML = svgContent;
              }

              render(azDeg, elDeg);

              window.fryRotate3D = function(id, az, el) {
                if (id !== '{{uid}}') return;
                if (az !== null) azDeg = parseFloat(az);
                if (el !== null) elDeg = parseFloat(el);
                render(azDeg, elDeg);
              };

              // Drag to rotate interaction
              let isDragging = false;
              let startX = 0, startY = 0;

              canvas.addEventListener('mousedown', function(e) {
                isDragging = true;
                startX = e.clientX;
                startY = e.clientY;
                canvas.style.cursor = 'grabbing';
              });

              window.addEventListener('mousemove', function(e) {
                if (!isDragging) return;
                const dx = e.clientX - startX;
                const dy = e.clientY - startY;
                startX = e.clientX;
                startY = e.clientY;
                azDeg = (azDeg + dx * 0.6 + 360) % 360;
                elDeg = Math.max(15, Math.min(80, elDeg - dy * 0.4));
                const azSlider = root.querySelector('.fry-3d-az-slider');
                if (azSlider) azSlider.value = Math.round(azDeg);
                const elSlider = root.querySelector('.fry-3d-el-slider');
                if (elSlider) elSlider.value = Math.round(elDeg);
                render(azDeg, elDeg);
              });

              window.addEventListener('mouseup', function() {
                if (isDragging) {
                  isDragging = false;
                  canvas.style.cursor = 'grab';
                }
              });
            })();
            </script>
            """);
        }
        else
        {
            RenderFallbackSvg(sb, plot);
        }

        sb.Append("</div>");
    }

    private static void RenderVisualizerSvg(StringBuilder sb, VisualizerSpec viz)
    {
        const double width = 760;
        const double height = 300;
        sb.Append($"<svg viewBox=\"0 0 {width} {height}\" width=\"100%\" height=\"auto\" style=\"display:block;border-radius:6px;background:#161b22;\" xmlns=\"http://www.w3.org/2000/svg\">");
        sb.Append($"<rect x=\"30\" y=\"30\" width=\"{width - 60}\" height=\"{height - 60}\" rx=\"8\" fill=\"#0d1117\" stroke=\"#30363d\" stroke-width=\"1.5\"/>");
        sb.Append($"<text x=\"{width / 2}\" y=\"{height / 2 - 10}\" fill=\"#58a6ff\" font-size=\"16\" font-weight=\"600\" text-anchor=\"middle\">{viz.Kind} Visualizer</text>");
        sb.Append($"<text x=\"{width / 2}\" y=\"{height / 2 + 18}\" fill=\"#8b949e\" font-size=\"12\" text-anchor=\"middle\">Interactive Data Structure Spec</text>");
        sb.Append("</svg>");
    }

    private static void RenderFallbackSvg(StringBuilder sb, VisualSpec spec)
    {
        const double width = 760;
        const double height = 240;
        sb.Append($"<svg viewBox=\"0 0 {width} {height}\" width=\"100%\" height=\"auto\" style=\"display:block;border-radius:6px;background:#161b22;\" xmlns=\"http://www.w3.org/2000/svg\">");
        sb.Append($"<text x=\"{width / 2}\" y=\"{height / 2}\" fill=\"#58a6ff\" font-size=\"14\" text-anchor=\"middle\">{spec.Family} Visual Output</text>");
        sb.Append("</svg>");
    }

    private static (double X, double Y) ProjectIso(double x, double y, double z, double cx, double cy)
    {
        // Isometric 3D Projection transformation
        const double scaleX = 180.0;
        const double scaleY = 90.0;
        const double scaleZ = 120.0;
        double px = cx + ((x - y) * Math.Cos(Math.PI / 6.0) * scaleX * 0.7);
        double py = cy + ((x + y) * Math.Sin(Math.PI / 6.0) * scaleY * 0.7) - (z * scaleZ * 0.6);
        return (px, py);
    }

    private static string ColorMap(double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        // Viridis/Cyan-Purple gradient: HSL 220 -> 140 -> 45
        double hue = 240.0 - (t * 195.0);
        return $"hsl({hue:F0}, 85%, 55%)";
    }

    private static void RenderLegend(StringBuilder sb, IEnumerable<(string Name, string Color)> items, double y)
    {
        var list = items.Take(6).ToList();
        if (list.Count == 0) return;

        double startX = 60;
        foreach (var (name, color) in list)
        {
            sb.Append($"<circle cx=\"{startX + 5:F1}\" cy=\"{y}\" r=\"4.5\" fill=\"{color}\"/>");
            sb.Append($"<text x=\"{startX + 14:F1}\" y=\"{y + 3.5:F1}\" fill=\"#8b949e\" font-size=\"11\" font-family=\"system-ui,sans-serif\">{WebUtility.HtmlEncode(name)}</text>");
            startX += Math.Max(90, name.Length * 7 + 35);
            if (startX > 680) break;
        }
    }

    private static string FormatNumber(double val)
    {
        if (Math.Abs(val) >= 1_000_000) return (val / 1_000_000.0).ToString("0.#M", CultureInfo.InvariantCulture);
        if (Math.Abs(val) >= 1_000) return (val / 1_000.0).ToString("0.#k", CultureInfo.InvariantCulture);
        if (Math.Abs(val) < 0.01 && Math.Abs(val) > 0) return val.ToString("0.##e+0", CultureInfo.InvariantCulture);
        return val.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
