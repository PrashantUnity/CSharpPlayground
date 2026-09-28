using System.IO;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.JavaScript;

/// <summary>
/// Provides zero-configuration runtime support for Node.js scripts in C# Code Studio,
/// injecting <c>dump()</c>, <c>display()</c>, and <c>Display</c> into JavaScript scripts.
/// </summary>
public static class JavaScriptDisplayRuntime
{
    public static async Task<string> EnsureRuntimeFilesAsync(IHostEnvironment host, CancellationToken ct = default)
    {
        var runtimeDir = Path.Combine(Path.GetTempPath(), "FryStudio", "js_runtime");
        try
        {
            Directory.CreateDirectory(runtimeDir);

            var preloadPath = Path.Combine(runtimeDir, "preload.js");
            var fryPath = Path.Combine(runtimeDir, "fry.js");
            var packageJsonPath = Path.Combine(runtimeDir, "package.json");

            var preloadCode = """
                try {
                    const fry = require('./fry');
                    globalThis.Display = fry.Display;
                    globalThis.dump = fry.dump;
                    globalThis.display = fry.display;
                } catch (e) {}
                """;

            var packageJsonCode = """
                {
                    "name": "fry",
                    "version": "1.0.0",
                    "main": "fry.js"
                }
                """;

            var fryCode = """
                const fs = require('fs');
                const DISPLAY_MARKER = '__FRY_DISPLAY__';
                const TABLE_MIME = 'application/vnd.fry.table+json';

                function emit(bundle) {
                    try {
                        const msg = JSON.stringify({ type: 'display', data: bundle, metadata: {} });
                        process.stdout.write('\n' + DISPLAY_MARKER + ' ' + msg + '\n');
                    } catch (e) {}
                }

                function dump(obj, title) {
                    return Display.dump(obj, title);
                }

                function display(...objects) {
                    for (const obj of objects) {
                        Display.dump(obj);
                    }
                }

                class Display {
                    static dump(obj, title) {
                        return Display.table(obj, title);
                    }

                    static table(obj, title) {
                        try {
                            const bundle = formatTableBundle(obj, title);
                            emit(bundle);
                        } catch (e) {
                            process.stderr.write(`[Display] table error: ${e.message}\n`);
                        }
                        return obj;
                    }

                    static show(obj, title) {
                        return Display.dump(obj, title);
                    }

                    static html(htmlContent) {
                        emit({ 'text/html': String(htmlContent || '') });
                    }

                    static image(dataOrPath, format = 'PNG') {
                        if (!dataOrPath) return;
                        if (typeof dataOrPath === 'string' && fs.existsSync(dataOrPath)) {
                            try {
                                const b64 = fs.readFileSync(dataOrPath).toString('base64');
                                const ext = dataOrPath.toLowerCase();
                                const mime = (ext.endsWith('.jpg') || ext.endsWith('.jpeg')) ? 'image/jpeg' : 'image/png';
                                emit({ [mime]: b64 });
                                return;
                            } catch (e) {}
                        }
                        if (typeof dataOrPath === 'string') {
                            let raw = dataOrPath.trim();
                            if (raw.startsWith('data:image')) {
                                const comma = raw.indexOf(',');
                                if (comma >= 0) raw = raw.substring(comma + 1);
                            }
                            emit({ 'image/png': raw });
                        }
                    }
                }

                function formatTableBundle(obj, title) {
                    if (obj === null || obj === undefined) {
                        const t = title || 'null';
                        return { [TABLE_MIME]: { title: t, columns: ['Value'], numeric: [false], rows: [['null']], totalRows: 1, totalColumns: 1 } };
                    }

                    // Array
                    if (Array.isArray(obj)) {
                        const len = obj.length;
                        const t = title || `Array[${len}]`;
                        if (len === 0) {
                            return { [TABLE_MIME]: { title: t, columns: ['Value'], numeric: [false], rows: [], totalRows: 0, totalColumns: 1 } };
                        }

                        const first = obj[0];
                        // Array of objects
                        if (typeof first === 'object' && first !== null && !Array.isArray(first)) {
                            const keys = Object.keys(first);
                            const cols = keys;
                            const rows = obj.map(item => keys.map(k => (item && item[k] !== undefined ? item[k] : null)));
                            const numeric = keys.map((_, i) => rows.every(r => typeof r[i] === 'number'));
                            return { [TABLE_MIME]: { title: t, columns: cols, numeric, rows, totalRows: len, totalColumns: cols.length } };
                        }

                        // 2D Array
                        if (Array.isArray(first)) {
                            const maxCols = Math.max(...obj.map(r => Array.isArray(r) ? r.length : 0));
                            const cols = Array.from({ length: maxCols }, (_, i) => `[${i}]`);
                            const rows = obj.map(r => Array.from({ length: maxCols }, (_, c) => (Array.isArray(r) && c < r.length ? r[c] : null)));
                            return { [TABLE_MIME]: { title: t, columns: cols, numeric: cols.map(() => false), rows, totalRows: len, totalColumns: maxCols } };
                        }

                        // 1D Array of scalars
                        const rows = obj.map((v, i) => [i, v]);
                        const isNum = obj.every(v => typeof v === 'number');
                        return { [TABLE_MIME]: { title: t, columns: ['Index', 'Value'], numeric: [true, isNum], rows, totalRows: len, totalColumns: 2 } };
                    }

                    // Plain Object
                    if (typeof obj === 'object') {
                        const keys = Object.keys(obj);
                        const t = title || 'Object';
                        const rows = keys.map(k => [k, obj[k]]);
                        return { [TABLE_MIME]: { title: t, columns: ['Key', 'Value'], numeric: [false, false], rows, totalRows: rows.length, totalColumns: 2 } };
                    }

                    // Scalar
                    const t = title || typeof obj;
                    return { [TABLE_MIME]: { title: t, columns: ['Value'], numeric: [typeof obj === 'number'], rows: [[obj]], totalRows: 1, totalColumns: 1 } };
                }

                module.exports = { Display, dump, display };
                """;

            await File.WriteAllTextAsync(preloadPath, preloadCode, ct).ConfigureAwait(false);
            await File.WriteAllTextAsync(packageJsonPath, packageJsonCode, ct).ConfigureAwait(false);
            await File.WriteAllTextAsync(fryPath, fryCode, ct).ConfigureAwait(false);
        }
        catch
        {
            // Defensive ignore
        }

        return runtimeDir;
    }
}
