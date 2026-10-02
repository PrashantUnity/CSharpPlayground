using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using FrySharp.Sdk;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Dialogs;

/// <summary>
/// Bridge implementation of IDialogApi for modal dialogs, input prompts, alerts, and file pickers.
/// </summary>
public class ExtensibilityDialogService : IDialogApi
{
    public Func<string, string, string, string?, Task<string?>>? PromptHandler { get; set; }
    public Func<string, string, Task<bool>>? ConfirmHandler { get; set; }
    public Func<string, string, Task>? AlertHandler { get; set; }
    public Func<string, IReadOnlyList<string>?, Task<string?>>? PickFileHandler { get; set; }
    public Func<string, Task<string?>>? PickFolderHandler { get; set; }
    public Func<string, object, double, double, Task>? CustomDialogHandler { get; set; }

    public async Task<string?> PromptAsync(string message, string title = "Prompt", string defaultValue = "", string? placeholder = null)
    {
        if (PromptHandler != null)
        {
            return await PromptHandler(message, title, defaultValue, placeholder);
        }

        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var tcs = new TaskCompletionSource<string?>();

            var tb = new TextBox
            {
                Text = defaultValue,
                PlaceholderText = placeholder ?? "Enter value...",
                Margin = new Thickness(0, 8, 0, 16)
            };

            var okBtn = new Button
            {
                Content = "OK",
                IsDefault = true,
                Classes = { "accent" },
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var cancelBtn = new Button
            {
                Content = "Cancel",
                IsCancel = true,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var win = new Window
            {
                Title = title,
                Width = 420,
                Height = 180,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            okBtn.Click += (_, _) =>
            {
                tcs.TrySetResult(tb.Text);
                win.Close();
            };

            cancelBtn.Click += (_, _) =>
            {
                tcs.TrySetResult(null);
                win.Close();
            };

            win.Closed += (_, _) => tcs.TrySetResult(null);

            var panel = new StackPanel
            {
                Margin = new Thickness(20),
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                    tb,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Children = { okBtn, cancelBtn }
                    }
                }
            };

            win.Content = panel;
            win.Show();
            tb.Focus();

            return await tcs.Task;
        });
    }

    public async Task<bool> ConfirmAsync(string message, string title = "Confirm")
    {
        if (ConfirmHandler != null)
        {
            return await ConfirmHandler(message, title);
        }

        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var tcs = new TaskCompletionSource<bool>();

            var win = new Window
            {
                Title = title,
                Width = 400,
                Height = 160,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            var yesBtn = new Button { Content = "Yes", Classes = { "accent" }, Margin = new Thickness(0, 0, 8, 0) };
            var noBtn = new Button { Content = "No", IsCancel = true };

            yesBtn.Click += (_, _) => { tcs.TrySetResult(true); win.Close(); };
            noBtn.Click += (_, _) => { tcs.TrySetResult(false); win.Close(); };
            win.Closed += (_, _) => tcs.TrySetResult(false);

            win.Content = new StackPanel
            {
                Margin = new Thickness(20),
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Children = { yesBtn, noBtn }
                    }
                }
            };

            win.Show();
            return await tcs.Task;
        });
    }

    public async Task AlertAsync(string message, string title = "Alert")
    {
        if (AlertHandler != null)
        {
            await AlertHandler(message, title);
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var tcs = new TaskCompletionSource<bool>();
            var win = new Window
            {
                Title = title,
                Width = 380,
                Height = 150,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            var okBtn = new Button { Content = "OK", IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
            okBtn.Click += (_, _) => { tcs.TrySetResult(true); win.Close(); };
            win.Closed += (_, _) => tcs.TrySetResult(true);

            win.Content = new StackPanel
            {
                Margin = new Thickness(20),
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) },
                    okBtn
                }
            };

            win.Show();
            await tcs.Task;
        });
    }

    public async Task<string?> PickFileAsync(string title = "Select File", IReadOnlyList<string>? extensions = null)
    {
        if (PickFileHandler != null)
        {
            return await PickFileHandler(title, extensions);
        }
        return null;
    }

    public async Task<string?> PickFolderAsync(string title = "Select Folder")
    {
        if (PickFolderHandler != null)
        {
            return await PickFolderHandler(title);
        }
        return null;
    }

    public async Task ShowCustomDialogAsync(string title, object content, double width = 500, double height = 400)
    {
        if (CustomDialogHandler != null)
        {
            await CustomDialogHandler(title, content, width, height);
            return;
        }

        if (content is Control control)
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var tcs = new TaskCompletionSource<bool>();
                var win = new Window
                {
                    Title = title,
                    Width = width,
                    Height = height,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Content = control
                };
                win.Closed += (_, _) => tcs.TrySetResult(true);
                win.Show();
                await tcs.Task;
            });
        }
    }
}
