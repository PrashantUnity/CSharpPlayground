using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;

public sealed partial class JavaDapAdapter
{
    private async Task DispatchDapCommandAsync(int reqSeq, string command, JsonElement? args)
    {
        switch (command.ToLowerInvariant())
        {
            case "initialize":
                await SendResponseAsync(reqSeq, command, true, new
                {
                    supportsConfigurationDoneRequest = true,
                    supportsConditionalBreakpoints = true,
                    supportsEvaluateForHovers = true
                }).ConfigureAwait(false);
                break;

            case "launch":
            case "attach":
                await SendResponseAsync(reqSeq, command, true, null).ConfigureAwait(false);
                break;

            case "setbreakpoints":
                var bps = await HandleSetBreakpointsAsync(args).ConfigureAwait(false);
                await SendResponseAsync(reqSeq, command, true, new { breakpoints = bps }).ConfigureAwait(false);
                break;

            case "configurationdone":
                lock (_stateLock)
                {
                    _isCurrentlyPaused = false;
                    _lastResumeIndex = _jdbOutputBuffer.Length;
                }
                await SendCommandAsync("run").ConfigureAwait(false);
                await SendResponseAsync(reqSeq, command, true, null).ConfigureAwait(false);
                break;

            case "threads":
                await SendResponseAsync(reqSeq, command, true, new
                {
                    threads = new[] { new { id = 1, name = "main" } }
                }).ConfigureAwait(false);
                break;

            case "stacktrace":
                var frames = await HandleStackTraceAsync().ConfigureAwait(false);
                await SendResponseAsync(reqSeq, command, true, new
                {
                    stackFrames = frames,
                    totalFrames = frames.Count
                }).ConfigureAwait(false);
                break;

            case "scopes":
                await SendResponseAsync(reqSeq, command, true, new
                {
                    scopes = new[]
                    {
                        new { name = "Locals", variablesReference = 1000, expensive = false }
                    }
                }).ConfigureAwait(false);
                break;

            case "variables":
                int varRef = args?.TryGetProperty("variablesReference", out var vr) == true ? vr.GetInt32() : 0;
                var vars = await HandleVariablesAsync(varRef).ConfigureAwait(false);
                await SendResponseAsync(reqSeq, command, true, new { variables = vars }).ConfigureAwait(false);
                break;

            case "evaluate":
                var evalResult = await HandleEvaluateAsync(args).ConfigureAwait(false);
                await SendResponseAsync(reqSeq, command, evalResult.Success, evalResult.Body).ConfigureAwait(false);
                break;

            case "continue":
                await ResumeAndCommandAsync(reqSeq, command, "cont").ConfigureAwait(false);
                break;

            case "next":
                await ResumeAndCommandAsync(reqSeq, command, "next").ConfigureAwait(false);
                break;

            case "stepin":
                await ResumeAndCommandAsync(reqSeq, command, "step").ConfigureAwait(false);
                break;

            case "stepout":
                await ResumeAndCommandAsync(reqSeq, command, "step up").ConfigureAwait(false);
                break;

            case "pause":
                await SendResponseAsync(reqSeq, command, true, null).ConfigureAwait(false);
                await SendCommandAsync("suspend").ConfigureAwait(false);
                break;

            case "disconnect":
                await SendResponseAsync(reqSeq, command, true, null).ConfigureAwait(false);
                try { _process.Kill(); } catch { }
                await EmitDapEventAsync("terminated", new { exitCode = 0 }).ConfigureAwait(false);
                break;

            default:
                await SendResponseAsync(reqSeq, command, true, null).ConfigureAwait(false);
                break;
        }
    }

    private async Task ResumeAndCommandAsync(int reqSeq, string command, string jdbCmd)
    {
        lock (_stateLock)
        {
            _isCurrentlyPaused = false;
            _lastResumeIndex = _jdbOutputBuffer.Length;
        }
        await SendCommandAsync(jdbCmd).ConfigureAwait(false);
        await SendResponseAsync(reqSeq, command, true, null).ConfigureAwait(false);
    }

    private async Task<List<object>> HandleSetBreakpointsAsync(JsonElement? args)
    {
        var results = new List<object>();
        if (!args.HasValue) return results;

        if (args.Value.TryGetProperty("breakpoints", out var bpArray) && bpArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var bp in bpArray.EnumerateArray())
            {
                int line = bp.GetProperty("line").GetInt32();
                await SendCommandAsync($"stop at {_fqn}:{line}").ConfigureAwait(false);
                results.Add(new { id = line, verified = true, line });
            }
        }

        return results;
    }

    private async Task<List<object>> HandleStackTraceAsync()
    {
        var frames = new List<object>();
        var output = await SendCommandAsync("where").ConfigureAwait(false);

        foreach (Match match in FrameRegex().Matches(output))
        {
            int idx = int.Parse(match.Groups["idx"].Value);
            string method = match.Groups["method"].Value.Trim();
            string file = match.Groups["file"].Value.Trim();
            int line = int.Parse(match.Groups["line"].Value);

            frames.Add(new
            {
                id = idx,
                name = method,
                line,
                column = 1,
                source = new
                {
                    name = file,
                    path = string.Equals(Path.GetFileName(_sourceFilePath), file, StringComparison.OrdinalIgnoreCase)
                        ? _sourceFilePath
                        : file
                }
            });
        }

        if (frames.Count == 0 && _pausedLine > 0)
        {
            frames.Add(new
            {
                id = 1,
                name = _pausedMethod,
                line = _pausedLine,
                column = 1,
                source = new
                {
                    name = Path.GetFileName(_sourceFilePath),
                    path = _sourceFilePath
                }
            });
        }

        return frames;
    }

    private async Task<List<object>> HandleVariablesAsync(int varRef)
    {
        var items = new List<object>();
        var output = await SendCommandAsync("locals").ConfigureAwait(false);

        foreach (Match match in VarRegex().Matches(output))
        {
            string name = match.Groups["name"].Value.Trim();
            string val = match.Groups["val"].Value.Trim();

            if (name.Equals("main", StringComparison.OrdinalIgnoreCase) && val.Length == 0) continue;

            items.Add(new
            {
                name,
                value = val,
                type = "object",
                variablesReference = 0
            });
        }

        return items;
    }

    private async Task<(bool Success, object Body)> HandleEvaluateAsync(JsonElement? args)
    {
        string expr = args?.TryGetProperty("expression", out var e) == true ? e.GetString() ?? string.Empty : string.Empty;
        if (string.IsNullOrWhiteSpace(expr))
        {
            return (false, new { message = "Empty expression" });
        }

        var output = await SendCommandAsync($"print {expr}").ConfigureAwait(false);
        int eqIdx = output.IndexOf('=');
        string result = eqIdx >= 0 ? output[(eqIdx + 1)..].Trim() : output.Trim();

        return (true, new
        {
            result = string.IsNullOrEmpty(result) ? expr : result,
            type = "object",
            variablesReference = 0
        });
    }
}
