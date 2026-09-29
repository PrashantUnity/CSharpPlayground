using System;
using System.Globalization;
using System.Text;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Services;

public static class Plot3DHtmlExporter
{
    public static string GenerateThreeJsHtml(Plot3DOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\">");
        sb.AppendLine($"  <title>{options.Title} - FrySharp 3D</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    body { margin: 0; overflow: hidden; background: #070B10; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; }");
        sb.AppendLine("    #info { position: absolute; top: 12px; left: 16px; color: #e6edf3; font-size: 13px; font-weight: 600; pointer-events: none; z-index: 10; }");
        sb.AppendLine("    #tooltip { position: absolute; display: none; background: rgba(18, 24, 34, 0.92); border: 1px solid #4ec9b0; border-radius: 4px; padding: 6px 10px; color: #fff; font-size: 11px; pointer-events: none; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("  <script src=\"https://cdn.jsdelivr.net/npm/three@0.160.0/build/three.min.js\"></script>");
        sb.AppendLine("  <script src=\"https://cdn.jsdelivr.net/npm/three@0.160.0/examples/js/controls/OrbitControls.js\"></script>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine($"  <div id=\"info\">{options.Title}</div>");
        sb.AppendLine("  <div id=\"tooltip\"></div>");
        sb.AppendLine("  <script>");
        sb.AppendLine("    const scene = new THREE.Scene();");
        sb.AppendLine("    scene.background = new THREE.Color(0x070B10);");
        sb.AppendLine("    const camera = new THREE.PerspectiveCamera(45, window.innerWidth / window.innerHeight, 0.1, 1000);");
        sb.AppendLine("    camera.position.set(15, 12, 15);");
        sb.AppendLine("    const renderer = new THREE.WebGLRenderer({ antialias: true });");
        sb.AppendLine("    renderer.setSize(window.innerWidth, window.innerHeight);");
        sb.AppendLine("    document.body.appendChild(renderer.domElement);");
        sb.AppendLine("    const controls = new THREE.OrbitControls(camera, renderer.domElement);");
        sb.AppendLine("    controls.enableDamping = true;");
        sb.AppendLine("    scene.add(new THREE.AmbientLight(0xffffff, 0.6));");
        sb.AppendLine("    const dirLight = new THREE.DirectionalLight(0xffffff, 0.8);");
        sb.AppendLine("    dirLight.position.set(10, 20, 15);");
        sb.AppendLine("    scene.add(dirLight);");
        sb.AppendLine("    const grid = new THREE.GridHelper(16, 16, 0x30363d, 0x161b22);");
        sb.AppendLine("    grid.position.y = -4;");
        sb.AppendLine("    scene.add(grid);");

        AppendDataToScript(sb, options);

        sb.AppendLine("    window.addEventListener('resize', () => {");
        sb.AppendLine("      camera.aspect = window.innerWidth / window.innerHeight;");
        sb.AppendLine("      camera.updateProjectionMatrix();");
        sb.AppendLine("      renderer.setSize(window.innerWidth, window.innerHeight);");
        sb.AppendLine("    });");
        sb.AppendLine("    function animate() {");
        sb.AppendLine("      requestAnimationFrame(animate);");
        sb.AppendLine("      controls.update();");
        sb.AppendLine("      renderer.render(scene, camera);");
        sb.AppendLine("    }");
        sb.AppendLine("    animate();");
        sb.AppendLine("  </script>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static void AppendDataToScript(StringBuilder sb, Plot3DOptions options)
    {
        if (options.Surface != null)
        {
            var s = options.Surface;
            int rx = s.ResolutionX;
            int ry = s.ResolutionY;
            sb.AppendLine($"    const geom = new THREE.PlaneGeometry(10, 10, {rx - 1}, {ry - 1});");
            sb.AppendLine("    geom.rotateX(-Math.PI / 2);");
            sb.AppendLine("    const pos = geom.attributes.position;");
            sb.AppendLine("    const zVals = [");
            for (int i = 0; i < rx; i++)
            {
                for (int j = 0; j < ry; j++)
                {
                    double z = s.ZValues[i, j];
                    sb.Append(z.ToString("0.###", CultureInfo.InvariantCulture)).Append(",");
                }
            }
            sb.AppendLine("    ];");
            sb.AppendLine("    for (let k = 0; k < pos.count; k++) { pos.setY(k, zVals[k] || 0); }");
            sb.AppendLine("    geom.computeVertexNormals();");
            sb.AppendLine("    const mat = new THREE.MeshStandardMaterial({ color: 0x4ec9b0, wireframe: " + (options.Wireframe ? "true" : "false") + ", roughness: 0.4 });");
            sb.AppendLine("    scene.add(new THREE.Mesh(geom, mat));");
        }
        else if (options.Graph != null)
        {
            sb.AppendLine("    const nodeGeom = new THREE.SphereGeometry(0.3, 16, 16);");
            sb.AppendLine("    const nodeMat = new THREE.MeshStandardMaterial({ color: 0x4ec9b0 });");
            foreach (var node in options.Graph.Nodes)
            {
                string x = (node.X * 0.4).ToString("0.###", CultureInfo.InvariantCulture);
                string y = (node.Y * 0.4).ToString("0.###", CultureInfo.InvariantCulture);
                string z = (node.Z * 0.4).ToString("0.###", CultureInfo.InvariantCulture);
                sb.AppendLine($"    const nMesh_{node.Id} = new THREE.Mesh(nodeGeom, nodeMat); nMesh_{node.Id}.position.set({x}, {y}, {z}); scene.add(nMesh_{node.Id});");
            }
        }
        else if (options.Series.Count > 0)
        {
            sb.AppendLine("    const pts = [];");
            foreach (var pt in options.Series[0].Points)
            {
                string x = (pt.X * 0.4).ToString("0.###", CultureInfo.InvariantCulture);
                string y = (pt.Y * 0.4).ToString("0.###", CultureInfo.InvariantCulture);
                string z = (pt.Z * 0.4).ToString("0.###", CultureInfo.InvariantCulture);
                sb.AppendLine($"    pts.push(new THREE.Vector3({x}, {y}, {z}));");
            }
            sb.AppendLine("    const ptGeom = new THREE.BufferGeometry().setFromPoints(pts);");
            sb.AppendLine("    const ptMat = new THREE.PointsMaterial({ color: 0x4ec9b0, size: 0.25 });");
            sb.AppendLine("    scene.add(new THREE.Points(ptGeom, ptMat));");
        }
    }
}
