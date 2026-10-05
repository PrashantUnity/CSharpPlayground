using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PdfEditorApp.Plugins.CSharpEditor.Runner;

public partial class AboutWindow : Window
{
    public Action? OpenDocumentationAction { get; set; }

    public AboutWindow()
    {
        InitializeComponent();

        var arch = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        var os = RuntimeInformation.OSDescription;
        var dotnetVer = Environment.Version.ToString();

        var runtimeBlock = this.FindControl<TextBlock>("RuntimeText");
        if (runtimeBlock != null)
        {
            runtimeBlock.Text = $".NET {dotnetVer} ({arch})";
        }

        var platformBlock = this.FindControl<TextBlock>("PlatformText");
        if (platformBlock != null)
        {
            platformBlock.Text = os;
        }
    }

    private void OnDocumentationClick(object? sender, RoutedEventArgs e)
    {
        if (OpenDocumentationAction != null)
        {
            OpenDocumentationAction();
            Close();
            return;
        }

        try
        {
            OpenUrl("https://github.com/CodeFryDev");
        }
        catch
        {
        }
    }

    private void OnGitHubClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            OpenUrl("https://github.com/CodeFryDev");
        }
        catch
        {
        }
    }

    private void OnSponsorClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            OpenUrl("https://github.com/sponsors/PrashantUnity");
        }
        catch
        {
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private static void OpenUrl(string url)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", url);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        else
        {
            Process.Start("xdg-open", url);
        }
    }
}
