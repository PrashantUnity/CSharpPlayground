using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

public sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut)
{
    public bool Succeeded => !TimedOut && ExitCode == 0;
}

/// <summary>
/// The machine the studio runs on, as toolchain discovery sees it. Tests substitute a fake one, so discovery rules can be
/// checked for every OS without the programs installed.
/// </summary>
public interface IHostEnvironment
{
    bool IsWindows { get; }
    bool IsMacOS { get; }
    bool IsLinux { get; }
    string HomeDirectory { get; }

    string? GetEnvironmentVariable(string name);
    bool FileExists(string path);
    bool DirectoryExists(string path);

    /// <summary>The folders directly inside <paramref name="path"/>, or none when it's missing or unreadable.</summary>
    IReadOnlyList<string> GetDirectories(string path);

    /// <summary>
    /// PATH as the user's login shell sets it, or null when unknown. Apps started from the Finder get a minimal PATH
    /// without Homebrew and friends, so this finds the same programs the user's terminal does.
    /// </summary>
    Task<string?> GetLoginShellPathAsync(CancellationToken ct = default);

    /// <summary>Runs a short command and captures its output. Never throws for a missing program (exit code -1).</summary>
    Task<CommandResult> RunAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken ct = default);
}

public sealed class HostEnvironment : IHostEnvironment
{
    private static readonly TimeSpan LoginShellTimeout = TimeSpan.FromSeconds(3);
    private readonly object _gate = new();
    private Task<string?>? _loginShellPath;

    public bool IsWindows => OperatingSystem.IsWindows();
    public bool IsMacOS => OperatingSystem.IsMacOS();
    public bool IsLinux => OperatingSystem.IsLinux();
    public string HomeDirectory => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public string? GetEnvironmentVariable(string name) => Environment.GetEnvironmentVariable(name);

    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public IReadOnlyList<string> GetDirectories(string path)
    {
        try
        {
            return Directory.Exists(path) ? Directory.GetDirectories(path) : Array.Empty<string>();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    public Task<string?> GetLoginShellPathAsync(CancellationToken ct = default)
    {
        if (IsWindows) return Task.FromResult<string?>(null);
        lock (_gate)
        {
            // Asked once per run: a login shell takes a moment to start.
            return _loginShellPath ??= ReadLoginShellPathAsync();
        }
    }

    private async Task<string?> ReadLoginShellPathAsync()
    {
        var shell = GetEnvironmentVariable("SHELL");
        if (string.IsNullOrWhiteSpace(shell) || !File.Exists(shell))
        {
            shell = File.Exists("/bin/zsh") ? "/bin/zsh" : "/bin/bash";
        }

        // -l (login) reads the profile files where Homebrew, pyenv and similar tools put themselves on PATH. Not -i: an
        // interactive shell prints prompts and banners. The last line is PATH, whatever the profile printed before it.
        var result = await RunAsync(shell, ["-l", "-c", "/usr/bin/printenv PATH"], LoginShellTimeout);
        if (!result.Succeeded) return null;
        var lastLine = result.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault();
        return string.IsNullOrWhiteSpace(lastLine) ? null : lastLine;
    }

    public async Task<CommandResult> RunAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken ct = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start()) return new CommandResult(-1, string.Empty, $"Couldn't start {fileName}.", false);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return new CommandResult(-1, string.Empty, ex.Message, false);
        }

        // Nothing is typed into a probe: a program waiting for input sees end-of-file instead of hanging.
        try { process.StandardInput.Close(); } catch (IOException) { }

        var stdout = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } catch (Win32Exception) { }
            return new CommandResult(-1, await Settle(stdout), await Settle(stderr), TimedOut: true);
        }

        return new CommandResult(process.ExitCode, await Settle(stdout), await Settle(stderr), false);
    }

    // Output of a killed process may never finish reading (a grandchild can hold the pipe): give up after a moment.
    private static async Task<string> Settle(Task<string> read)
    {
        var done = await Task.WhenAny(read, Task.Delay(TimeSpan.FromSeconds(1)));
        return done == read ? await read : string.Empty;
    }
}
