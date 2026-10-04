using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FrySharp.Sdk;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Terminal;

/// <summary>
/// Bridge implementation of ITerminalApi for terminal output and external command execution.
/// </summary>
public class ExtensibilityTerminalService : ITerminalApi
{
    public Action<string>? OutputWriter { get; set; }
    public Action? ClearHandler { get; set; }

    public void Write(string text)
    {
        OutputWriter?.Invoke(text);
    }

    public void WriteLine(string line = "")
    {
        Write(line + Environment.NewLine);
    }

    public void Clear()
    {
        ClearHandler?.Invoke();
    }

    public async Task<CommandRunResult> RunCommandAsync(
        string command,
        string arguments = "",
        string? workingDirectory = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        var sw = Stopwatch.StartNew();
        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        WriteLine($"$ {command} {arguments}".TrimEnd());

        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            WorkingDirectory = !string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory)
                ? workingDirectory
                : Directory.GetCurrentDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stdoutBuilder.AppendLine(e.Data);
                WriteLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stderrBuilder.AppendLine(e.Data);
                WriteLine(e.Data);
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(ct);
            sw.Stop();

            return new CommandRunResult
            {
                ExitCode = process.ExitCode,
                Output = stdoutBuilder.ToString(),
                Error = stderrBuilder.ToString(),
                Duration = sw.Elapsed
            };
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            sw.Stop();
            WriteLine("⚠️ Process execution cancelled.");
            return new CommandRunResult
            {
                ExitCode = -1,
                Output = stdoutBuilder.ToString(),
                Error = "Operation cancelled",
                Duration = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            WriteLine($"✗ Process error: {ex.Message}");
            return new CommandRunResult
            {
                ExitCode = -1,
                Output = stdoutBuilder.ToString(),
                Error = ex.Message,
                Duration = sw.Elapsed
            };
        }
    }
}
