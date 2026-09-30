using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Services;

/// <summary>
/// A 3D plot as a page any browser draws with three.js: its points, lines, bars, surface or graph, scaled into a box
/// with z up (as the studio draws it), and orbit controls. The data goes in as JSON, so nothing in it (a title, a node's
/// id) ever becomes markup or code.
/// </summary>
public static class Plot3DHtmlExporter
{
    private const string ThreeModule = "https://cdn.jsdelivr.net/npm/three@0.160.0/build/three.module.js";
    private const string OrbitControlsModule = "https://cdn.jsdelivr.net/npm/three@0.160.0/examples/jsm/controls/OrbitControls.js";

    // Each axis is scaled into [-Box, Box].
    private const double Box = 4;

    private static readonly JsonSerializerOptions PageJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static string GenerateThreeJsHtml(Plot3DOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var title = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(options.Title) ? "3D plot" : options.Title);

        // The default encoder escapes <, > and &, so the JSON can't close the script element it sits in.
        var data = JsonSerializer.Serialize(Scene(options), PageJson);

        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <title>{{title}}</title>
              <style>
                body { margin: 0; overflow: hidden; background: #070B10; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; }
                #info { position: absolute; top: 12px; left: 16px; color: #e6edf3; font-size: 13px; font-weight: 600; pointer-events: none; }
              </style>
              <script type="importmap">{ "imports": { "three": "{{ThreeModule}}" } }</script>
            </head>
            <body>
              <div id="info">{{title}}</div>
              <script type="application/json" id="plot-data">{{data}}</script>
              <script type="module">
                import * as THREE from 'three';
                import { OrbitControls } from '{{OrbitControlsModule}}';

                const data = JSON.parse(document.getElementById('plot-data').textContent);
                const scene = new THREE.Scene();
                scene.background = new THREE.Color(0x070b10);
                const camera = new THREE.PerspectiveCamera(45, innerWidth / innerHeight, 0.1, 1000);
                camera.up.set(0, 0, 1); // z is up, as in the studio
                camera.position.set(11, -11, 8);
                const renderer = new THREE.WebGLRenderer({ antialias: true });
                renderer.setSize(innerWidth, innerHeight);
                document.body.appendChild(renderer.domElement);
                const controls = new OrbitControls(camera, renderer.domElement);
                controls.enableDamping = true;
                scene.add(new THREE.AmbientLight(0xffffff, 0.7));
                const light = new THREE.DirectionalLight(0xffffff, 0.9);
                light.position.set(8, -6, 14);
                scene.add(light);

                const floor = new THREE.GridHelper(8, 8, 0x30363d, 0x161b22);
                floor.rotation.x = Math.PI / 2;
                floor.position.z = -4;
                scene.add(floor);
                const axes = new THREE.AxesHelper(8.6);
                axes.position.set(-4, -4, -4);
                scene.add(axes);

                const colorsOf = list => new THREE.Float32BufferAttribute(list.flatMap(hex => new THREE.Color(hex).toArray()), 3);
                for (const cloud of data.clouds) {
                  const geometry = new THREE.BufferGeometry();
                  geometry.setAttribute('position', new THREE.Float32BufferAttribute(cloud.positions, 3));
                  geometry.setAttribute('color', colorsOf(cloud.colors));
                  scene.add(new THREE.Points(geometry, new THREE.PointsMaterial({ size: 0.14, vertexColors: true })));
                }
                for (const line of data.lines) {
                  const geometry = new THREE.BufferGeometry();
                  geometry.setAttribute('position', new THREE.Float32BufferAttribute(line.positions, 3));
                  scene.add(new THREE.Line(geometry, new THREE.LineBasicMaterial({ color: line.color })));
                }
                for (const bar of data.bars) {
                  const mesh = new THREE.Mesh(new THREE.BoxGeometry(0.5, 0.5, bar.height), new THREE.MeshStandardMaterial({ color: bar.color }));
                  mesh.position.set(bar.x, bar.y, -4 + bar.height / 2);
                  scene.add(mesh);
                }
                if (data.surface) {
                  const geometry = new THREE.BufferGeometry();
                  geometry.setAttribute('position', new THREE.Float32BufferAttribute(data.surface.positions, 3));
                  geometry.setAttribute('color', colorsOf(data.surface.colors));
                  geometry.setIndex(data.surface.triangles);
                  geometry.computeVertexNormals();
                  scene.add(new THREE.Mesh(geometry, new THREE.MeshStandardMaterial({ vertexColors: true, side: THREE.DoubleSide, wireframe: data.surface.wireframe })));
                }

                addEventListener('resize', () => {
                  camera.aspect = innerWidth / innerHeight;
                  camera.updateProjectionMatrix();
                  renderer.setSize(innerWidth, innerHeight);
                });
                (function animate() {
                  requestAnimationFrame(animate);
                  controls.update();
                  renderer.render(scene, camera);
                })();
              </script>
            </body>
            </html>
            """;
    }

    /// <summary>What the page draws, in its coordinates (each axis in [-4, 4], z up) and CSS colours.</summary>
    internal sealed record ExportScene(List<ExportCloud> Clouds, List<ExportLine> Lines, List<ExportBar> Bars, ExportSurface? Surface);

    internal sealed record ExportCloud(List<double> Positions, List<string> Colors);

    internal sealed record ExportLine(List<double> Positions, string Color);

    internal sealed record ExportBar(double X, double Y, double Height, string Color);

    internal sealed record ExportSurface(List<double> Positions, List<string> Colors, List<int> Triangles, bool Wireframe);

    internal static ExportScene Scene(Plot3DOptions options)
    {
        var scene = new ExportScene([], [], [], null);
        var primary = options.PrimaryColor;
        if (options.Surface is { } surface) return scene with { Surface = Surface(options, surface) };

        if (options.Graph is { } graph)
        {
            var nodes = graph.Nodes.Where(n => IsFinite(n.X, n.Y, n.Z)).ToList();
            scene.Clouds.Add(new ExportCloud(nodes.SelectMany(n => Place(options, n.X, n.Y, n.Z)).ToList(), nodes.Select(n => n.Color ?? primary).ToList()));
            var at = nodes.ToDictionary(n => n.Id);
            foreach (var edge in graph.Edges.Where(e => at.ContainsKey(e.FromId) && at.ContainsKey(e.ToId)))
            {
                var (a, b) = (at[edge.FromId], at[edge.ToId]);
                scene.Lines.Add(new ExportLine([.. Place(options, a.X, a.Y, a.Z), .. Place(options, b.X, b.Y, b.Z)], edge.Color ?? "#8b949e"));
            }

            return scene;
        }

        foreach (var series in options.Series)
        {
            var color = series.Color ?? primary;
            var points = series.Points.Where(p => IsFinite(p.X, p.Y, p.Z)).ToList();
            if (options.Type == Plot3DType.VoxelBar)
            {
                foreach (var p in points)
                {
                    var (x, y, z) = Placed(options, p.X, p.Y, p.Z);
                    scene.Bars.Add(new ExportBar(x, y, Math.Max(0.02, z + Box), p.CustomColor ?? color));
                }

                continue;
            }

            if (options.Type == Plot3DType.Trajectory) scene.Lines.Add(new ExportLine(points.SelectMany(p => Place(options, p.X, p.Y, p.Z)).ToList(), color));
            scene.Clouds.Add(new ExportCloud(points.SelectMany(p => Place(options, p.X, p.Y, p.Z)).ToList(), points.Select(p => p.CustomColor ?? color).ToList()));
        }

        return scene;
    }

    // Every height a vertex, coloured by the colour map; each quad whose four heights are there two triangles, so a
    // missing height is a hole, as in the studio.
    private static ExportSurface Surface(Plot3DOptions options, Surface3DData surface)
    {
        int columns = surface.ResolutionX, rows = surface.ResolutionY;
        var positions = new List<double>(columns * rows * 3);
        var colors = new List<string>(columns * rows);
        var triangles = new List<int>();
        double stepX = columns > 1 ? (surface.MaxX - surface.MinX) / (columns - 1) : 0;
        double stepY = rows > 1 ? (surface.MaxY - surface.MinY) / (rows - 1) : 0;
        double heights = Math.Max(1e-9, surface.MaxZ - surface.MinZ);

        for (int j = 0; j < rows; j++)
        {
            for (int i = 0; i < columns; i++)
            {
                double z = surface.ZValues[i, j];
                positions.AddRange(Place(options, surface.MinX + i * stepX, surface.MinY + j * stepY, double.IsFinite(z) ? z : surface.MinZ));
                colors.Add(Css(ColorMapService.GetColor(options.ColorMap, double.IsFinite(z) ? (z - surface.MinZ) / heights : 0)));
            }
        }

        for (int j = 0; j < rows - 1; j++)
        {
            for (int i = 0; i < columns - 1; i++)
            {
                if (!double.IsFinite(surface.ZValues[i, j]) || !double.IsFinite(surface.ZValues[i + 1, j]) ||
                    !double.IsFinite(surface.ZValues[i, j + 1]) || !double.IsFinite(surface.ZValues[i + 1, j + 1])) continue;
                int corner = j * columns + i;
                triangles.AddRange([corner, corner + 1, corner + columns, corner + 1, corner + columns + 1, corner + columns]);
            }
        }

        return new ExportSurface(positions, colors, triangles, options.Wireframe);
    }

    private static double[] Place(Plot3DOptions options, double x, double y, double z)
    {
        var (px, py, pz) = Placed(options, x, y, z);
        return [px, py, pz];
    }

    // Data into the page's box, rounded so the page stays small.
    private static (double X, double Y, double Z) Placed(Plot3DOptions options, double x, double y, double z) => (
        Math.Round(Scale(x, options.MinX, options.MaxX), 4),
        Math.Round(Scale(y, options.MinY, options.MaxY), 4),
        Math.Round(Scale(z, options.MinZ, options.MaxZ), 4));

    private static double Scale(double value, double min, double max) => ((value - min) / Math.Max(1e-9, max - min) - 0.5) * 2 * Box;

    private static bool IsFinite(double x, double y, double z) => double.IsFinite(x) && double.IsFinite(y) && double.IsFinite(z);

    private static string Css(Color color) => $"#{color.R:x2}{color.G:x2}{color.B:x2}";
}
