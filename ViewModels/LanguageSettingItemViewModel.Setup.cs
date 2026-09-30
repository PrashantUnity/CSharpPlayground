using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

/// <summary>
/// An actionable setup step for installing a missing compiler or language runtime.
/// Parses the descriptive text into an executable shell command or official download URL,
/// with one-click installation and clipboard copy capabilities.
/// </summary>
public sealed partial class ToolchainSetupStepItem : ObservableObject
{
    public required string RawText { get; init; }
    public required string Title { get; init; }
    public string? Command { get; init; }
    public string? Url { get; init; }
    public bool HasCommand => !string.IsNullOrWhiteSpace(Command);
    public bool HasUrl => !string.IsNullOrWhiteSpace(Url);
    public string? Subtitle => HasCommand ? Command : Url;
    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);
    public string? CopyableText => HasCommand ? Command : Url;
    public bool CanCopy => !string.IsNullOrWhiteSpace(CopyableText);
    public bool IsRunnable { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CopyButtonLabel))]
    private bool _isCopied;

    [ObservableProperty]
    private bool _isRunning;

    public string CopyButtonLabel => IsCopied ? "Copied! ✓" : "Copy";
}

public partial class LanguageSettingItemViewModel
{
    public bool HasDownloadUrl => !string.IsNullOrWhiteSpace(MissingDownloadUrl);

    public ObservableCollection<ToolchainSetupStepItem> SetupSteps { get; } = new();

    public bool HasQuickSetup => SetupSteps.Any(s => s.IsRunnable);

    public ToolchainSetupStepItem? PrimaryQuickSetup => SetupSteps.FirstOrDefault(s => s.IsRunnable);

    public static ToolchainSetupStepItem ParseSetupStep(string raw, IHostEnvironment? host)
    {
        var isWindows = host?.IsWindows ?? OperatingSystem.IsWindows();
        var isMac = host?.IsMacOS ?? OperatingSystem.IsMacOS();
        var isLinux = host?.IsLinux ?? OperatingSystem.IsLinux();

        var trimmed = raw.Trim();
        string title;
        string? command = null;
        string? url = null;

        var urlMatch = Regex.Match(trimmed, @"https?://[^\s]+");
        if (urlMatch.Success)
        {
            url = urlMatch.Value;
        }

        var colonIdx = urlMatch.Success ? trimmed[..urlMatch.Index].IndexOf(':') : trimmed.IndexOf(':');
        if (colonIdx > 0 && colonIdx < trimmed.Length - 1)
        {
            title = CleanTitle(trimmed[..colonIdx]);
            var rest = trimmed[(colonIdx + 1)..].Trim();
            if (urlMatch.Success && rest == urlMatch.Value)
            {
                // Pure URL step
            }
            else
            {
                command = rest;
            }
        }
        else if (urlMatch.Success && colonIdx < 0)
        {
            var textBeforeUrl = trimmed[..urlMatch.Index].Trim();
            title = CleanTitle(string.IsNullOrWhiteSpace(textBeforeUrl) ? (url ?? trimmed) : textBeforeUrl);
        }
        else if (trimmed.StartsWith("winget ", StringComparison.OrdinalIgnoreCase) ||
                 trimmed.StartsWith("brew ", StringComparison.OrdinalIgnoreCase) ||
                 trimmed.StartsWith("xcode-select ", StringComparison.OrdinalIgnoreCase) ||
                 trimmed.StartsWith("sudo apt", StringComparison.OrdinalIgnoreCase) ||
                 trimmed.StartsWith("apt-get ", StringComparison.OrdinalIgnoreCase) ||
                 trimmed.StartsWith("choco ", StringComparison.OrdinalIgnoreCase))
        {
            command = trimmed;
            title = CleanTitle(trimmed);
        }
        else
        {
            var cmdPattern = @"(winget\s+install\s+[\w\.\-]+|brew\s+install\s+[\w\.\-]+|xcode-select\s+--install|choco\s+install\s+[\w\.\-]+|sudo\s+apt(?:-get)?\s+install\s+[\w\.\-\s]+|dotnet\s+tool\s+install\s+[\w\.\-]+)";
            var match = Regex.Match(trimmed, cmdPattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                command = match.Value;
                var remaining = trimmed.Replace(match.Value, "", StringComparison.OrdinalIgnoreCase)
                                       .Replace("in terminal", "", StringComparison.OrdinalIgnoreCase)
                                       .Replace("run", "", StringComparison.OrdinalIgnoreCase).Trim();
                title = CleanTitle(string.IsNullOrWhiteSpace(remaining) ? command : remaining);
            }
            else
            {
                title = trimmed;
            }
        }

        var isRunnable = false;
        if (!string.IsNullOrWhiteSpace(command))
        {
            if (isWindows && (command.StartsWith("winget ", StringComparison.OrdinalIgnoreCase) ||
                              command.StartsWith("choco ", StringComparison.OrdinalIgnoreCase)))
            {
                isRunnable = true;
            }
            else if (isMac && (command.StartsWith("xcode-select", StringComparison.OrdinalIgnoreCase) ||
                               command.StartsWith("brew ", StringComparison.OrdinalIgnoreCase)))
            {
                isRunnable = true;
            }
            else if (isLinux && (command.StartsWith("sudo apt", StringComparison.OrdinalIgnoreCase) ||
                                 command.StartsWith("brew ", StringComparison.OrdinalIgnoreCase)))
            {
                isRunnable = true;
            }
        }

        return new ToolchainSetupStepItem
        {
            RawText = trimmed,
            Title = title,
            Command = command,
            Url = url,
            IsRunnable = isRunnable
        };
    }

    private static string CleanTitle(string text)
    {
        var cleaned = text.Trim();
        if (cleaned.StartsWith("or ", StringComparison.OrdinalIgnoreCase)) cleaned = cleaned[3..].Trim();
        if (cleaned.StartsWith("With ", StringComparison.OrdinalIgnoreCase)) cleaned = cleaned[5..].Trim();
        if (cleaned.StartsWith("Install ", StringComparison.OrdinalIgnoreCase)) cleaned = cleaned[8..].Trim();
        if (cleaned.EndsWith(" from", StringComparison.OrdinalIgnoreCase)) cleaned = cleaned[..^5].Trim();
        if (cleaned.EndsWith(" at", StringComparison.OrdinalIgnoreCase)) cleaned = cleaned[..^3].Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? text : cleaned;
    }

    [RelayCommand]
    public void DownloadInstaller()
    {
        if (!string.IsNullOrWhiteSpace(MissingDownloadUrl))
        {
            BrowserLauncher.Open(MissingDownloadUrl);
        }
    }

    [RelayCommand]
    public void OpenStepUrl(ToolchainSetupStepItem? step)
    {
        if (step?.Url is { } url && !string.IsNullOrWhiteSpace(url))
        {
            BrowserLauncher.Open(url);
        }
    }

    public async Task CopyCommandToClipboardAsync(ToolchainSetupStepItem? step, Avalonia.Input.Platform.IClipboard? clipboard)
    {
        if (step == null) return;
        var textToCopy = step.CopyableText;
        if (string.IsNullOrWhiteSpace(textToCopy)) return;

        if (clipboard != null)
        {
            await clipboard.SetTextAsync(textToCopy);
        }

        step.IsCopied = true;
        _ = Task.Run(async () =>
        {
            await Task.Delay(2500);
            Avalonia.Threading.Dispatcher.UIThread.Post(() => step.IsCopied = false);
        });
    }

    [RelayCommand]
    public async Task RunQuickSetupAsync()
    {
        if (PrimaryQuickSetup != null)
        {
            await RunSetupStepAsync(PrimaryQuickSetup);
        }
        else if (HasDownloadUrl)
        {
            DownloadInstaller();
        }
    }

    public async Task RunSetupStepAsync(ToolchainSetupStepItem step)
    {
        if (string.IsNullOrWhiteSpace(step.Command)) return;

        step.IsRunning = true;
        IsRunningAction = true;
        ActionStatusMessage = $"Running: {step.Command}…";
        ActionOutputLog = $"--- Starting setup: {step.Command} ---\n";

        void Append(string text)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                ActionOutputLog += text;
            });
        }

        try
        {
            var services = ParentSettings?.LanguageServices;
            var launcher = services?.Processes;
            var host = services?.Host;

            if (launcher != null && host != null)
            {
                var success = await ToolchainSetupRunner.ExecuteAsync(step.Command, host, launcher, Append);
                ActionStatusMessage = success ? $"✅ Setup finished: {step.Title}" : $"⚠️ Setup completed with warnings/errors.";
                ActionOutputLog += success ? "\n[Finished successfully]\n" : "\n[Finished with warnings/errors]\n";
            }
            else
            {
                ActionStatusMessage = "Process launcher not available.";
                ActionOutputLog += "\n[Error: Process launcher not available]\n";
            }

            if (ParentSettings != null)
            {
                await ParentSettings.RefreshLanguageToolchainAsync(this);
            }
        }
        catch (Exception ex)
        {
            ActionStatusMessage = $"Setup failed: {ex.Message}";
            ActionOutputLog += $"\n[Error: {ex.Message}]\n";
        }
        finally
        {
            step.IsRunning = false;
            IsRunningAction = false;
        }
    }
}
