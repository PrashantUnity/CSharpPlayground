using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using FryWorld.Sdk.Descriptors;
using FryWorld.Sdk.Models;
using FryWorld.Sdk.Plugins;
using FryWorld.Sdk.Services;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Views;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using CSharpCodeStudioViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio.CSharpCodeStudioViewModel;
using CSharpNotebookStudioViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Notebooks.CSharpNotebookStudioViewModel;
using CSharpStudioHostViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Common.CSharpStudioHostViewModel;

namespace PdfEditorApp.Plugins.CSharpEditor;

/// <summary>
/// FryPDF plugin entry point for FrySharp.
/// Mounts as a full-viewport Workspace Studio Navigation page, Status Bar widget,
/// Command Palette action, and Dynamic Ribbon action (No floating shell overlay).
/// </summary>
public class CSharpEditorPlugin : IFryPlugin
{
    public string Id => "com.fryworld.csharpstudio";
    public string Name => "FrySharp";
    public Version Version => new(2, 0, 5);
    public bool AutoOpenOverlay => false;
    public IReadOnlyList<Type> RequiredServices => Array.Empty<Type>();

    public IReadOnlyDictionary<string, PluginSettingDefinition>? SettingsSchema =>
        new Dictionary<string, PluginSettingDefinition>
        {
            ["DefaultTemplate"] = new()
            {
                Label = "Default Starter Script",
                Description = "Initial template loaded when creating a new script.",
                Type = PluginSettingType.Select,
                Options = new() { "Hello World Console", "PDF Document Automation", "LINQ Benchmark", "JSON Serializer" },
                DefaultValue = "Hello World Console"
            },
            ["EditorFontSize"] = new()
            {
                Label = "Editor Font Size (px)",
                Description = "Code typography size in points/pixels.",
                Type = PluginSettingType.Integer,
                DefaultValue = 13
            },
            ["ShowLineNumbers"] = new()
            {
                Label = "Show Line Numbers",
                Description = "Display gutter line numbers along the left margin.",
                Type = PluginSettingType.Boolean,
                DefaultValue = true
            },
            ["AutoCheckErrors"] = new()
            {
                Label = "Live Roslyn Diagnostics",
                Description = "Continuously analyze C# code in the background for syntax and semantic errors.",
                Type = PluginSettingType.Boolean,
                DefaultValue = true
            },
            ["ExecutionTimeoutSeconds"] = new()
            {
                Label = "Execution Timeout (seconds, 0 = no limit)",
                Description = "Maximum allowed run time before script execution is automatically stopped. 0 disables the automatic limit — Stop is always available regardless.",
                Type = PluginSettingType.Integer,
                DefaultValue = 0
            }
        };

    public Task ApplyAsync(IFryPluginContext ctx, CancellationToken ct = default)
    {
        // Scripts compile against the desktop API (Display, charts, Avalonia), so fix that before anything compiles.
        PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn.DesktopScriptHost.Configure();

        CSharpStudioHostViewModel? activeHost = null;

        // Opens the studio page in whichever shell hosts the plugin: FryWorld, FryPDF or the standalone runner.
        void NavigateToStudio()
        {
            if (ctx.TryGetService<IShellService>(out var shell)) shell.NavigateTo("com.fryworld.csharpstudio");
        }

        var navReg = ctx.RegisterNavigationItem(new NavigationItemDescriptor
        {
            Id = "com.fryworld.csharpstudio",
            Title = "FrySharp",
            Category = "Overview",
            IconKind = "CodeBraces",
            AccentColor = "#75D59A",
            Order = 38,
            ViewFactory = () =>
            {
                ctx.TryGetService<ISettingsManager>(out var settingsStore);
                var hostVm = new CSharpStudioHostViewModel(ctx, settingsStore);
                activeHost = hostVm;
                return new CSharpStudioHostView { DataContext = hostVm };
            }
        });

        var cmdReg = ctx.RegisterCommand(new CommandPaletteDescriptor
        {
            Id = "cmd.studio.csharpeditor",
            Title = "Open FrySharp",
            Subtitle = "Launch in-app C# development studio with project manager, editor, and Roslyn runner",
            Category = "Developer Tools",
            IconKind = "CodeBraces",
            ShortcutKey = OperatingSystem.IsMacOS() ? "Cmd+Alt+E" : "Ctrl+Alt+E",
            Command = new RelayCommand(NavigateToStudio)
        });

        var customizationManager = new PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.CustomizationManager();
        PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.StudioAppContext.Instance.CustomizationManager = customizationManager;

        var extensionManager = new PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Extensions.ExtensionManager();
        PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.StudioAppContext.Instance.ExtensionManager = extensionManager;

        // The look the user left the studio with (theme, density, colours), before their customization script runs, so
        // the script still has the last word.
        PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.AppearanceRestorer.RestoreFrom(
            null, PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.StudioAppContext.Instance.ThemeEngine);

        _ = Task.Run(async () =>
        {
            var ws = PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.StudioAppContext.Instance.WorkspaceService.RootPath;
            await customizationManager.InitializeAsync(enableHotReload: true, ct);
            await extensionManager.DiscoverAndLoadFromDefaultLocationsAsync(workspacePath: ws, enableHotReload: true, ct);
        }, ct);

        var reloadCustomizationsCmd = ctx.RegisterCommand(new CommandPaletteDescriptor
        {
            Id = "cmd.studio.reload_customizations",
            Title = "Reload Customizations & Themes",
            Subtitle = "Recompile and apply ~/.frysharp/init.csx and active themes in real time",
            Category = "Developer Tools",
            IconKind = "Refresh",
            ShortcutKey = OperatingSystem.IsMacOS() ? "Cmd+Shift+R" : "Ctrl+Shift+R",
            Command = new RelayCommand(() => { _ = customizationManager.ReloadAsync(); })
        });

        var openInitScriptCmd = ctx.RegisterCommand(new CommandPaletteDescriptor
        {
            Id = "cmd.studio.open_init_script",
            Title = "Open Customization Script (init.csx)",
            Subtitle = "Edit in-app startup script, colors, themes, commands, and hooks",
            Category = "Developer Tools",
            IconKind = "Tune",
            Command = new RelayCommand(() =>
            {
                NavigateToStudio();
                _ = customizationManager.Storage.EnsureInitScriptExistsAsync().ContinueWith(async t =>
                {
                    if (activeHost?.CodeStudioViewModel != null)
                    {
                        var item = new ScriptDocumentItem
                        {
                            Id = "init.csx",
                            Title = "init.csx",
                            Code = await t,
                            LanguageId = "csharp",
                            SourceFilePath = customizationManager.Storage.GlobalInitScriptPath
                        };
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            activeHost.NavigateToCodeStudio(item);
                        });
                    }
                });
            })
        });

        var statusReg = ctx.RegisterStatusBarWidget(new StatusBarWidgetDescriptor
        {
            Id = "frypdf.status.csharpeditor",
            AlignRight = true,
            Order = 14,
            Tooltip = "FrySharp & Automation Hub",
            CustomControlFactory = () => new Button
            {
                Content = "{ } FrySharp",
                Classes = { "m3-tonal-btn" },
                Command = new RelayCommand(NavigateToStudio)
            }
        });

        var ribbonActionReg = ctx.RegisterRibbonTab(new RibbonTabDescriptor
        {
            Id = "plugins",
            Header = "Plugins",
            Actions = new List<RibbonActionDescriptor>
            {
                new RibbonActionDescriptor
                {
                    Id = "frypdf.ribbon.action.csharpeditor",
                    GroupId = "tools",
                    Label = "FrySharp",
                    Tooltip = "Open full-featured C# development studio and script automation workspace",
                    IconKind = "CodeBraces",
                    Order = 30,
                    Command = new RelayCommand(NavigateToStudio)
                }
            }
        });

        var shortcuts = new List<IDisposable>
        {
            ctx.RegisterShortcut(new ShortcutDescriptor
            {
                Id = "csharp.studio.launch",
                Description = "Open in-app C# development studio and script automation workspace.",
                KeyGesture = OperatingSystem.IsMacOS() ? "Cmd+Alt+E" : "Ctrl+Alt+E",
                Command = new RelayCommand(NavigateToStudio)
            }),

            ctx.RegisterShortcut(new ShortcutDescriptor
            {
                Id = "csharp.editor.run",
                Description = "Execute active C# script or debug at current breakpoint.",
                KeyGesture = "F5",
                Command = new RelayCommand(() =>
                {
                    if (activeHost?.CurrentPage is not CSharpCodeStudioViewModel codeVm) return;
                    if (codeVm.IsPaused)
                    {
                        codeVm.ContinueDebug();
                    }
                    else if (!codeVm.IsExecuting && !codeVm.IsDebugging)
                    {
                        codeVm.DebugCodeCommand.Execute(null);
                    }
                })
            }),

            ctx.RegisterShortcut(new ShortcutDescriptor
            {
                Id = "csharp.editor.stop",
                Description = "Halt currently running C# background script, or interrupt the active notebook cell.",
                KeyGesture = "Shift+F5",
                Command = new RelayCommand(() =>
                {
                    switch (activeHost?.CurrentPage)
                    {
                        case CSharpCodeStudioViewModel { IsDebugging: true } codeVm:
                            codeVm.StopDebug();
                            break;
                        case CSharpCodeStudioViewModel codeVm:
                            codeVm.StopCommand.Execute(null);
                            break;
                        case CSharpNotebookStudioViewModel nbVm:
                            nbVm.InterruptExecutionCommand.Execute(null);
                            break;
                    }
                })
            }),

            ctx.RegisterShortcut(new ShortcutDescriptor
            {
                Id = "csharp.editor.save",
                Description = "Save active C# script or notebook to local workspace storage.",
                KeyGesture = OperatingSystem.IsMacOS() ? "Cmd+S" : "Ctrl+S",
                Command = new RelayCommand(() =>
                {
                    switch (activeHost?.CurrentPage)
                    {
                        case CSharpCodeStudioViewModel codeVm:
                            codeVm.SaveCommand.Execute(null);
                            break;
                        case CSharpNotebookStudioViewModel nbVm:
                            nbVm.SaveCommand.Execute(null);
                            break;
                    }
                })
            }),

            ctx.RegisterShortcut(new ShortcutDescriptor
            {
                Id = "csharp.editor.new",
                Description = "Create a new notebook when Notebook Studio is active, otherwise a new automation script.",
                KeyGesture = OperatingSystem.IsMacOS() ? "Cmd+N" : "Ctrl+N",
                Command = new RelayCommand(() =>
                {
                    if (activeHost?.CurrentPage is CSharpNotebookStudioViewModel nbVm)
                    {
                        nbVm.NewNotebookTabCommand.Execute(null);
                    }
                    else
                    {
                        var newScriptTask = activeHost?.ManagerViewModel.CreateNewScriptAsync(null);
                    }
                })
            }),

            ctx.RegisterShortcut(new ShortcutDescriptor
            {
                Id = "csharp.editor.show_references",
                Description = "Switch to References tab and manage NuGet package dependencies.",
                KeyGesture = OperatingSystem.IsMacOS() ? "Cmd+Shift+P" : "Ctrl+Shift+P",
                Command = new RelayCommand(() =>
                {
                    if (activeHost?.CurrentPage is CSharpCodeStudioViewModel codeVm)
                    {
                        codeVm.SelectedLeftTabIndex = codeVm.SelectedLeftTabIndex == 1 ? 0 : 1;
                    }
                })
            }),

            ctx.RegisterShortcut(new ShortcutDescriptor
            {
                Id = "csharp.notebook.run_cell",
                Description = "Execute current notebook code cell and advance cursor to next cell.",
                KeyGesture = "Shift+Enter",
                Command = new RelayCommand(() =>
                {
                    if (activeHost?.CurrentPage is CSharpNotebookStudioViewModel nbVm)
                    {
                        nbVm.RunCellAndSelectNextCommand.Execute(null);
                    }
                })
            }),

            ctx.RegisterShortcut(new ShortcutDescriptor
            {
                Id = "csharp.notebook.run_cell_stay",
                Description = "Execute current notebook cell without moving cursor focus.",
                KeyGesture = OperatingSystem.IsMacOS() ? "Cmd+Enter" : "Ctrl+Enter",
                Command = new RelayCommand(() =>
                {
                    if (activeHost?.CurrentPage is CSharpNotebookStudioViewModel { ActiveCell: not null } nbVm)
                    {
                        nbVm.RunSingleCellCommand.Execute(nbVm.ActiveCell);
                    }
                })
            }),

            ctx.RegisterShortcut(new ShortcutDescriptor
            {
                Id = "csharp.notebook.run_all",
                Description = "Restart notebook kernel and execute all cells in sequence.",
                KeyGesture = OperatingSystem.IsMacOS() ? "Cmd+Shift+Enter" : "Ctrl+Shift+Enter",
                Command = new RelayCommand(() =>
                {
                    if (activeHost?.CurrentPage is CSharpNotebookStudioViewModel nbVm)
                    {
                        nbVm.RunAllCellsCommand.Execute(null);
                    }
                })
            }),

            ctx.RegisterShortcut(new ShortcutDescriptor
            {
                Id = "csharp.notebook.insert_cell_below",
                Description = "Insert a new C# code cell below current cell.",
                KeyGesture = OperatingSystem.IsMacOS() ? "Cmd+Shift+B" : "Ctrl+Shift+B",
                Command = new RelayCommand(() =>
                {
                    if (activeHost?.CurrentPage is CSharpNotebookStudioViewModel nbVm)
                    {
                        nbVm.AddCodeCellCommand.Execute(nbVm.ActiveCell);
                    }
                })
            }),

            ctx.RegisterShortcut(new ShortcutDescriptor
            {
                Id = "csharp.notebook.insert_cell_above",
                Description = "Insert a new C# code cell above current cell.",
                KeyGesture = OperatingSystem.IsMacOS() ? "Cmd+Shift+A" : "Ctrl+Shift+A",
                Command = new RelayCommand(() =>
                {
                    if (activeHost?.CurrentPage is CSharpNotebookStudioViewModel nbVm)
                    {
                        nbVm.AddCellAboveCommand.Execute(null);
                    }
                })
            }),

            ctx.RegisterShortcut(new ShortcutDescriptor
            {
                Id = "csharp.notebook.delete_cell",
                Description = "Remove the selected notebook cell from document.",
                KeyGesture = OperatingSystem.IsMacOS() ? "Cmd+Shift+D" : "Ctrl+Shift+D",
                Command = new RelayCommand(() =>
                {
                    if (activeHost?.CurrentPage is CSharpNotebookStudioViewModel nbVm)
                    {
                        nbVm.DeleteActiveCellCommand.Execute(null);
                    }
                })
            }),

            ctx.RegisterShortcut(new ShortcutDescriptor
            {
                Id = "csharp.editor.zoom_in",
                Description = "Increase code canvas and terminal font size.",
                KeyGesture = OperatingSystem.IsMacOS() ? "Cmd+=" : "Ctrl+=",
                Command = new RelayCommand(() =>
                {
                    if (activeHost?.CurrentPage is CSharpCodeStudioViewModel codeVm) codeVm.ZoomInCommand.Execute(null);
                    else if (activeHost?.CurrentPage is CSharpNotebookStudioViewModel nbVm) nbVm.ZoomInCommand.Execute(null);
                })
            }),

            ctx.RegisterShortcut(new ShortcutDescriptor
            {
                Id = "csharp.editor.zoom_out",
                Description = "Decrease code canvas and terminal font size.",
                KeyGesture = OperatingSystem.IsMacOS() ? "Cmd+-" : "Ctrl+-",
                Command = new RelayCommand(() =>
                {
                    if (activeHost?.CurrentPage is CSharpCodeStudioViewModel codeVm) codeVm.ZoomOutCommand.Execute(null);
                    else if (activeHost?.CurrentPage is CSharpNotebookStudioViewModel nbVm) nbVm.ZoomOutCommand.Execute(null);
                })
            }),

            ctx.RegisterShortcut(new ShortcutDescriptor
            {
                Id = "csharp.editor.zoom_reset",
                Description = "Reset typography to default 100% (13px).",
                KeyGesture = OperatingSystem.IsMacOS() ? "Cmd+0" : "Ctrl+0",
                Command = new RelayCommand(() =>
                {
                    if (activeHost?.CurrentPage is CSharpCodeStudioViewModel codeVm) codeVm.ResetZoomCommand.Execute(null);
                    else if (activeHost?.CurrentPage is CSharpNotebookStudioViewModel nbVm) nbVm.ResetZoomCommand.Execute(null);
                })
            }),

            ctx.RegisterShortcut(new ShortcutDescriptor
            {
                Id = "csharp.studio.reload_customizations",
                Description = "Recompile and apply in-app customization scripts and theme tokens.",
                KeyGesture = OperatingSystem.IsMacOS() ? "Cmd+Shift+R" : "Ctrl+Shift+R",
                Command = new RelayCommand(() => { _ = customizationManager.ReloadAsync(); })
            })
        };

        // Programs the studio starts (Python runs and notebook kernels) end with it: when the app exits, and when the plugin
        // is unloaded.
        var killProcessesOnExit = ProcessRegistry.KillAllOnExit();

        ctx.RegisterEffect(() =>
        {
            killProcessesOnExit.Dispose();
            ProcessRegistry.KillAll();
            navReg.Dispose();
            cmdReg.Dispose();
            reloadCustomizationsCmd.Dispose();
            openInitScriptCmd.Dispose();
            customizationManager.Dispose();
            extensionManager.Dispose();
            statusReg.Dispose();
            ribbonActionReg.Dispose();
            foreach (var s in shortcuts) s.Dispose();
        });

        return Task.CompletedTask;
    }
}
