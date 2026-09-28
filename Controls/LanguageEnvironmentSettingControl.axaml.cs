using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls;

public partial class LanguageEnvironmentSettingControl : UserControl
{
    public LanguageEnvironmentSettingControl()
    {
        InitializeComponent();
    }

    private void OnCustomModeRadioClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is LanguageSettingItemViewModel vm)
        {
            vm.IsAutoDetect = false;
        }
    }

    private async void OnBrowseExecutableClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } storageProvider) return;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Executable / Interpreter",
            AllowMultiple = false
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } filePath)
        {
            if (DataContext is LanguageSettingItemViewModel vm)
            {
                vm.CustomPath = filePath;
                vm.IsAutoDetect = false;
            }
        }
    }

    private async void OnRunActionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ToolchainAction action } &&
            DataContext is LanguageSettingItemViewModel vm &&
            vm.Provider != null)
        {
            vm.IsRunningAction = true;
            vm.ActionStatusMessage = $"Executing {action.Label}…";
            vm.ActionOutputLog = $"--- Starting {action.Label} ---\n";

            void Append(string text)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    vm.ActionOutputLog += text;
                });
            }

            try
            {
                var query = new ToolchainQuery(null, null);
                var result = await Task.Run(() => vm.Provider.RunActionAsync(action.Id, query, Append));
                vm.ActionStatusMessage = result.Success ? result.Message : $"⚠️ {result.Message}";
                vm.ActionOutputLog += $"\n[Finished: {result.Message}]\n";
            }
            catch (Exception ex)
            {
                vm.ActionStatusMessage = $"Action failed: {ex.Message}";
                ActionOutputLogAppend(vm, $"\n[Error: {ex.Message}]\n");
            }
            finally
            {
                vm.IsRunningAction = false;
            }
        }
    }

    private static void ActionOutputLogAppend(LanguageSettingItemViewModel vm, string text)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            vm.ActionOutputLog += text;
        });
    }

    private void OnDownloadInstallerClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is LanguageSettingItemViewModel vm && !string.IsNullOrWhiteSpace(vm.MissingDownloadUrl))
        {
            PdfEditorApp.Plugins.CSharpEditor.Services.BrowserLauncher.Open(vm.MissingDownloadUrl);
        }
    }

    private void OnOpenStepUrlClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ToolchainSetupStepItem { Url: { Length: > 0 } url } })
        {
            PdfEditorApp.Plugins.CSharpEditor.Services.BrowserLauncher.Open(url);
        }
    }

    private async void OnCopyStepCommandClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ToolchainSetupStepItem step } && DataContext is LanguageSettingItemViewModel vm)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            await vm.CopyCommandToClipboardAsync(step, topLevel?.Clipboard);
        }
    }

    private async void OnRunStepCommandClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ToolchainSetupStepItem step } && DataContext is LanguageSettingItemViewModel vm)
        {
            await vm.RunSetupStepAsync(step);
        }
    }

    private async void OnPrimaryQuickSetupClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is LanguageSettingItemViewModel vm)
        {
            await vm.RunQuickSetupAsync();
        }
    }

    private async void OnCheckAgainClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is LanguageSettingItemViewModel vm && vm.ParentSettings != null)
        {
            await vm.ParentSettings.RefreshLanguageToolchainAsync(vm);
        }
    }
}

