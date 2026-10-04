#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction;

namespace DartSupportExtension;

/// <summary>
/// Polyglot interactive notebook kernel for Dart.
/// Evaluates Dart cell snippets, accumulates imports and top-level classes/functions across cells,
/// and streams console outputs to notebook cells.
/// </summary>
public sealed class DartNotebookKernel : INotebookKernel
{
    private readonly DartToolchainProvider _toolchain;
    private readonly IProcessLauncher _processes;
    private readonly IHostEnvironment _host;
    private readonly KernelCreationContext _context;

    private readonly HashSet<string> _cumulativeImports = new(StringComparer.Ordinal)
    {
        "import 'dart:core';",
        "import 'dart:io';",
        "import 'dart:convert';",
        "import 'dart:math';",
        "import 'display.dart';"
    };

    private readonly Dictionary<string, string> _cumulativeTopLevel = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string TypeName, string JsonValue)> _sharedVariables = new(StringComparer.Ordinal);
    private int _executionCount;
    private bool _isDisposed;

    public DartNotebookKernel(
        DartToolchainProvider toolchain,
        IProcessLauncher processes,
        IHostEnvironment host,
        KernelCreationContext context)
    {
        _toolchain = toolchain ?? throw new ArgumentNullException(nameof(toolchain));
        _processes = processes ?? throw new ArgumentNullException(nameof(processes));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public string LanguageId => "dart";
    public string DisplayName => "Dart (SDK)";
    public bool IsSessionActive => _executionCount > 0 || _cumulativeImports.Count > 0 || _cumulativeTopLevel.Count > 0;
    public bool CanForceStop => true;

    public async Task<KernelExecutionResult> ExecuteAsync(KernelExecutionRequest request, CancellationToken ct)
    {
        var clock = Stopwatch.StartNew();
        _executionCount++;

        var workingFolder = _context.WorkingDirectory();
        if (string.IsNullOrWhiteSpace(workingFolder) || !Directory.Exists(workingFolder))
        {
            workingFolder = _host.HomeDirectory;
        }

        var resolution = await _toolchain.ResolveAsync(new ToolchainQuery(workingFolder, _context.WorkspaceRoot?.Invoke()), ct).ConfigureAwait(false);
        if (!resolution.IsFound || resolution.Toolchain == null)
        {
            var guidance = resolution.Missing?.Summary ?? "Dart SDK not found on PATH. Install via 'brew install dart' or download from https://dart.dev/get-dart.";
            var err = guidance + "\n";
            request.OnConsole?.Invoke(err);
            return new KernelExecutionResult
            {
                Success = false,
                ErrorMessage = guidance,
                ConsoleOutput = err,
                Elapsed = clock.Elapsed
            };
        }

        var code = request.Code?.Trim() ?? string.Empty;
        ExtractImportsAndTopLevel(code, out var imports, out var topLevelItems, out var body);

        foreach (var imp in imports) _cumulativeImports.Add(imp);

        var hash = Math.Abs((workingFolder + "_dart_cell_" + _executionCount).GetHashCode(StringComparison.OrdinalIgnoreCase)).ToString("x8");
        var outDir = Path.Combine(Path.GetTempPath(), "FryStudio", "dart_cells", hash);
        Directory.CreateDirectory(outDir);

        await DartDisplayRuntime.EnsureInDirectoryAsync(outDir, ct).ConfigureAwait(false);

        var fullProgram = BuildCellProgram(body, topLevelItems);
        var sourcePath = Path.Combine(outDir, "cell.dart");
        await File.WriteAllTextAsync(sourcePath, fullProgram, ct).ConfigureAwait(false);

        var dartExecutable = resolution.Toolchain.ExecutablePath;
        using var visuals = new ExternalVisualSession();
        var consoleBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();
        var processor = new ExternalOutputProcessor(
            onConsoleText: text =>
            {
                consoleBuilder.Append(text);
                request.OnConsole?.Invoke(text);
            },
            onRichOutput: bundle =>
            {
                request.OnRichOutput?.Invoke(bundle);
            },
            onShare: (name, json) =>
            {
                _sharedVariables[name] = ("dynamic", json);
            },
            visuals: visuals.Visuals);

        int exitCode;
        try
        {
            var startSpec = visuals.Apply(new ProcessStartSpec
            {
                FileName = dartExecutable,
                Arguments = ["run", sourcePath],
                WorkingDirectory = workingFolder
            });

            using var runProcess = _processes.Start(
                startSpec,
                outText => processor.ProcessChunk(outText),
                errText =>
                {
                    errorBuilder.Append(errText);
                    processor.ProcessChunk(errText);
                });

            runProcess.CloseInput();
            exitCode = await runProcess.WaitForExitOrKillAsync(ct).ConfigureAwait(false);
            processor.Flush();
        }
        catch (OperationCanceledException)
        {
            return new KernelExecutionResult
            {
                WasCancelled = true,
                Success = false,
                ErrorMessage = "Cell execution cancelled.",
                ConsoleOutput = consoleBuilder.ToString(),
                Elapsed = clock.Elapsed
            };
        }

        if (exitCode == 0)
        {
            foreach (var item in topLevelItems)
            {
                var key = GetDeclarationKey(item);
                _cumulativeTopLevel[key] = item;
            }
        }

        clock.Stop();
        bool success = exitCode == 0;
        var errorOutput = errorBuilder.ToString().Trim();

        return new KernelExecutionResult
        {
            Success = success,
            ErrorMessage = success ? null : (!string.IsNullOrEmpty(errorOutput) ? errorOutput : $"Process exited with code {exitCode}"),
            ConsoleOutput = consoleBuilder.ToString(),
            Elapsed = clock.Elapsed
        };
    }

    private void ExtractImportsAndTopLevel(
        string code,
        out List<string> imports,
        out List<string> topLevel,
        out string body)
    {
        imports = new List<string>();
        topLevel = new List<string>();
        var bodyBuilder = new StringBuilder();

        var lines = code.Replace("\r\n", "\n").Split('\n');
        bool inTopLevel = false;
        int braceDepth = 0;
        int bracketDepth = 0;
        int parenDepth = 0;
        var currentBlock = new StringBuilder();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("import ") && trimmed.EndsWith(";"))
            {
                imports.Add(trimmed);
                continue;
            }

            if (!inTopLevel && braceDepth == 0 && bracketDepth == 0 && parenDepth == 0)
            {
                if (IsTopLevelStart(trimmed))
                {
                    inTopLevel = true;
                    currentBlock.Clear();
                }
            }

            if (inTopLevel)
            {
                currentBlock.AppendLine(line);
                braceDepth += CountChar(line, '{') - CountChar(line, '}');
                bracketDepth += CountChar(line, '[') - CountChar(line, ']');
                parenDepth += CountChar(line, '(') - CountChar(line, ')');

                if (braceDepth <= 0 && bracketDepth <= 0 && parenDepth <= 0 &&
                    (trimmed.EndsWith("}") || trimmed.EndsWith(";")))
                {
                    inTopLevel = false;
                    braceDepth = 0;
                    bracketDepth = 0;
                    parenDepth = 0;
                    topLevel.Add(currentBlock.ToString().Trim());
                    currentBlock.Clear();
                }
                continue;
            }

            bodyBuilder.AppendLine(line);
        }

        if (currentBlock.Length > 0)
        {
            if (inTopLevel)
            {
                topLevel.Add(currentBlock.ToString().Trim());
            }
            else
            {
                bodyBuilder.AppendLine(currentBlock.ToString().Trim());
            }
        }

        body = bodyBuilder.ToString().Trim();
    }

    private static bool IsTopLevelStart(string trimmed)
    {
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("//") || trimmed.StartsWith("/*"))
            return false;

        // Never treat statements or display calls as top-level declarations
        if (trimmed.StartsWith("print(") ||
            trimmed.StartsWith("print ") ||
            trimmed.StartsWith("Display.") ||
            trimmed.StartsWith("Visualizer.") ||
            trimmed.StartsWith("assert(") ||
            trimmed.StartsWith("if (") ||
            trimmed.StartsWith("if(") ||
            trimmed.StartsWith("for (") ||
            trimmed.StartsWith("for(") ||
            trimmed.StartsWith("while (") ||
            trimmed.StartsWith("while(") ||
            trimmed.StartsWith("switch (") ||
            trimmed.StartsWith("switch(") ||
            trimmed.StartsWith("try ") ||
            trimmed.StartsWith("try{") ||
            trimmed.StartsWith("return ") ||
            trimmed.StartsWith("throw "))
        {
            return false;
        }

        // Classes, enums, mixins, extensions, typedefs
        if (trimmed.StartsWith("class ") ||
            trimmed.StartsWith("abstract class ") ||
            trimmed.StartsWith("sealed class ") ||
            trimmed.StartsWith("enum ") ||
            trimmed.StartsWith("mixin ") ||
            trimmed.StartsWith("extension ") ||
            trimmed.StartsWith("typedef "))
        {
            return true;
        }

        // Variables: var, final, const, late
        if (trimmed.StartsWith("var ") ||
            trimmed.StartsWith("final ") ||
            trimmed.StartsWith("const ") ||
            trimmed.StartsWith("late "))
        {
            return true;
        }

        // Type-annotated declarations: List<...>, Map<...>, Set<...>, int, double, num, String, bool, dynamic, void
        if (trimmed.StartsWith("List<") ||
            trimmed.StartsWith("Map<") ||
            trimmed.StartsWith("Set<") ||
            trimmed.StartsWith("int ") ||
            trimmed.StartsWith("double ") ||
            trimmed.StartsWith("num ") ||
            trimmed.StartsWith("String ") ||
            trimmed.StartsWith("bool ") ||
            trimmed.StartsWith("dynamic ") ||
            trimmed.StartsWith("void "))
        {
            return true;
        }

        return false;
    }

    private static string GetDeclarationKey(string item)
    {
        var trimmed = item.Trim();
        if (trimmed.StartsWith("class ") || trimmed.StartsWith("abstract class ") || trimmed.StartsWith("sealed class ") || trimmed.StartsWith("enum ") || trimmed.StartsWith("mixin ") || trimmed.StartsWith("extension ") || trimmed.StartsWith("typedef "))
        {
            var parts = trimmed.Split(new[] { ' ', '<', '{', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                if ((parts[i] is "class" or "enum" or "mixin" or "extension" or "typedef") && i + 1 < parts.Length)
                {
                    return $"type_{parts[i + 1]}";
                }
            }
        }

        var eqIdx = trimmed.IndexOf('=');
        var parenIdx = trimmed.IndexOf('(');
        var semiIdx = trimmed.IndexOf(';');
        int end = trimmed.Length;
        if (eqIdx >= 0) end = Math.Min(end, eqIdx);
        if (parenIdx >= 0) end = Math.Min(end, parenIdx);
        if (semiIdx >= 0) end = Math.Min(end, semiIdx);

        var header = trimmed[..end].Trim();
        var headerWords = header.Split(new[] { ' ', '\t', '<', '>', ',', '?', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (headerWords.Length > 0)
        {
            var id = headerWords[^1];
            return $"decl_{id}";
        }

        return $"item_{Math.Abs(item.GetHashCode(StringComparison.Ordinal))}";
    }

    private static int CountChar(string s, char c)
    {
        int count = 0;
        foreach (var ch in s) if (ch == c) count++;
        return count;
    }

    private string BuildCellProgram(string body, List<string> newTopLevel)
    {
        var sb = new StringBuilder();

        // 1. Imports
        foreach (var imp in _cumulativeImports)
        {
            sb.AppendLine(imp);
        }
        sb.AppendLine();

        // 2. Accumulated top-level items
        foreach (var item in _cumulativeTopLevel.Values)
        {
            sb.AppendLine(item);
            sb.AppendLine();
        }
        foreach (var item in newTopLevel)
        {
            sb.AppendLine(item);
            sb.AppendLine();
        }

        // 3. Shared variables bridge
        if (_sharedVariables.Count > 0)
        {
            sb.AppendLine("// Injected shared variables:");
            foreach (var kvp in _sharedVariables)
            {
                sb.AppendLine($"final {kvp.Key} = jsonDecode('''{kvp.Value.JsonValue}''');");
            }
            sb.AppendLine();
        }

        // 4. Execution entry point
        if (body.Contains("void main(") || body.Contains("main()"))
        {
            sb.AppendLine(body);
        }
        else
        {
            sb.AppendLine("Future<void> main() async {");
            if (!string.IsNullOrWhiteSpace(body))
            {
                sb.AppendLine(body);
            }
            sb.AppendLine("}");
        }

        return sb.ToString();
    }

    public Task<IReadOnlyList<NotebookVariableInfo>> GetVariablesAsync(CancellationToken ct)
    {
        var list = new List<NotebookVariableInfo>();
        foreach (var kvp in _sharedVariables)
        {
            list.Add(new NotebookVariableInfo
            {
                Name = kvp.Key,
                TypeName = kvp.Value.TypeName,
                ValueDisplay = kvp.Value.JsonValue,
                Kernel = DisplayName
            });
        }
        return Task.FromResult<IReadOnlyList<NotebookVariableInfo>>(list);
    }

    public Task<string> GetValueJsonAsync(string name, CancellationToken ct)
    {
        if (_sharedVariables.TryGetValue(name, out var val))
        {
            return Task.FromResult(val.JsonValue);
        }
        return Task.FromResult("null");
    }

    public Task SetValueFromJsonAsync(string name, string json, CancellationToken ct)
    {
        _sharedVariables[name] = ("dynamic", json);
        return Task.CompletedTask;
    }

    public void HardReset()
    {
        _cumulativeImports.Clear();
        _cumulativeImports.Add("import 'dart:core';");
        _cumulativeImports.Add("import 'dart:io';");
        _cumulativeImports.Add("import 'dart:convert';");
        _cumulativeImports.Add("import 'dart:math';");
        _cumulativeImports.Add("import 'display.dart';");
        _cumulativeTopLevel.Clear();
        _sharedVariables.Clear();
        _executionCount = 0;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _cumulativeImports.Clear();
        _cumulativeTopLevel.Clear();
        _sharedVariables.Clear();
    }
}
