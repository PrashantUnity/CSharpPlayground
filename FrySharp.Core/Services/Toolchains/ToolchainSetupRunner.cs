using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

/// <summary>
/// Executes toolchain setup and package manager installation commands across operating systems,
/// streaming real-time stdout and stderr to the output log while registering processes for safe cleanup.
/// </summary>
public static class ToolchainSetupRunner
{
    public static async Task<bool> ExecuteAsync(
        string command,
        IHostEnvironment host,
        IProcessLauncher launcher,
        Action<string> output,
        CancellationToken ct = default)
    {
        var trimmed = command.Trim();
        output($"\n$ {trimmed}\n");

        ProcessStartSpec spec;
        if (host.IsWindows)
        {
            spec = new ProcessStartSpec
            {
                FileName = "cmd.exe",
                Arguments = ["/c", trimmed]
            };
        }
        else if (host.IsMacOS)
        {
            if (trimmed.StartsWith("xcode-select", StringComparison.OrdinalIgnoreCase))
            {
                spec = new ProcessStartSpec
                {
                    FileName = "/usr/bin/xcode-select",
                    Arguments = ["--install"]
                };
            }
            else
            {
                var shell = host.GetEnvironmentVariable("SHELL");
                if (string.IsNullOrWhiteSpace(shell) || !host.FileExists(shell))
                {
                    shell = host.FileExists("/bin/zsh") ? "/bin/zsh" : "/bin/bash";
                }
                spec = new ProcessStartSpec
                {
                    FileName = shell,
                    Arguments = ["-l", "-c", trimmed]
                };
            }
        }
        else
        {
            spec = new ProcessStartSpec
            {
                FileName = "/bin/bash",
                Arguments = ["-l", "-c", trimmed]
            };
        }

        try
        {
            using var process = launcher.Start(spec, output, output);
            using (ct.Register(process.Kill))
            {
                var exitCode = await process.Completion;
                if (exitCode == 0)
                {
                    output("\n✅ Command completed successfully.\n");
                    return true;
                }

                output($"\n❌ Command exited with code {exitCode}.\n");
                return false;
            }
        }
        catch (Exception ex)
        {
            output($"\n❌ Failed to start command: {ex.Message}\n");
            return false;
        }
    }
}
