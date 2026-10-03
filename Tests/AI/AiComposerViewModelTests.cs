using System;
using System.IO;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Models.AI;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.AI;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Common;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class AiComposerViewModelTests
{
    [Fact]
    public void AiComposerViewModel_Defaults_AreCorrect()
    {
        var vm = new AiComposerViewModel();

        Assert.False(vm.IsVisible);
        Assert.False(vm.IsMinimized);
        Assert.True(vm.IsAgentMode);
        Assert.False(vm.IsGenerating);
        Assert.Equal("Ready", vm.StatusText);
        Assert.Equal(AiSettings.DefaultModelName, vm.SelectedModel);
        Assert.Contains(AiSettings.DefaultModelName, vm.AvailableModels);
    }

    [Fact]
    public void ToggleFloating_TogglesVisibilityCorrectly()
    {
        var vm = new AiComposerViewModel();

        Assert.False(vm.IsVisible);
        vm.ToggleFloating();
        Assert.True(vm.IsVisible);
        vm.ToggleFloating();
        Assert.False(vm.IsVisible);

        // When minimized, toggling floating should unminimize and make visible
        vm.IsMinimized = true;
        vm.ToggleFloating();
        Assert.False(vm.IsMinimized);
        Assert.True(vm.IsVisible);
    }

    [Fact]
    public void ToggleMinimize_TogglesMinimizedState()
    {
        var vm = new AiComposerViewModel();

        Assert.False(vm.IsMinimized);
        vm.ToggleMinimize();
        Assert.True(vm.IsMinimized);
        vm.ToggleMinimize();
        Assert.False(vm.IsMinimized);
    }

    [Fact]
    public void AcceptAllChanges_MarksModifiedFilesAcceptedAndClearsSession()
    {
        var vm = new AiComposerViewModel();
        var item1 = new ModifiedFileItem { FilePath = "File1.cs" };
        var item2 = new ModifiedFileItem { FilePath = "File2.cs" };

        vm.SessionModifiedFiles.Add(item1);
        vm.SessionModifiedFiles.Add(item2);
        Assert.True(vm.HasModifiedFiles);

        vm.AcceptAllChanges();

        Assert.True(item1.IsAccepted);
        Assert.True(item2.IsAccepted);
        Assert.Empty(vm.SessionModifiedFiles);
        Assert.False(vm.HasModifiedFiles);
    }

    [Fact]
    public void ClearChat_EmptiesMessagesAndModifiedFiles()
    {
        var vm = new AiComposerViewModel();
        vm.Messages.Add(new ChatMessageItem { Content = "Hello" });
        vm.SessionModifiedFiles.Add(new ModifiedFileItem { FilePath = "Temp.cs" });

        vm.ClearChat();

        Assert.Empty(vm.Messages);
        Assert.Empty(vm.SessionModifiedFiles);
        Assert.False(vm.HasModifiedFiles);
    }

    [Fact]
    public void CodeStudioViewModel_ToggleAiComposerCommand_InvokesAction()
    {
        bool toggled = false;
        var codeVm = new CSharpCodeStudioViewModel(
            new ScriptDocumentItem(),
            new Services.Storage.LocalScriptStorageService(),
            new Services.Roslyn.RoslynCompilerService(),
            new Services.Execution.ScriptExecutionEngine(),
            () => { })
        {
            ToggleAiComposerAction = () => toggled = true
        };

        codeVm.ToggleAiComposerCommand.Execute(null);

        Assert.True(toggled);
    }

    [Fact]
    public void StudioHostViewModel_ToggleAiComposerCommand_TogglesFloatingComposer()
    {
        var host = new CSharpStudioHostViewModel();

        Assert.False(host.AiComposer.IsVisible);
        host.ToggleAiComposerCommand.Execute(null);
        Assert.True(host.AiComposer.IsVisible);
        host.ToggleAiComposerCommand.Execute(null);
        Assert.False(host.AiComposer.IsVisible);
    }

    [Fact]
    public void ModifiedFileItem_CalculateLineMetrics_GeneratesDiffPreview()
    {
        var item = new ModifiedFileItem
        {
            FilePath = "Math.cs",
            OriginalContent = "int a = 1;\nint b = 2;\n",
            ModifiedContent = "int a = 1;\nint b = 3;\nint c = 4;\n"
        };

        item.CalculateLineMetrics();

        Assert.True(item.LinesAdded > 0);
        Assert.True(item.LinesDeleted > 0);
        Assert.Contains("+ int b = 3;", item.DiffPreviewText);
        Assert.Contains("- int b = 2;", item.DiffPreviewText);
    }

    [Fact]
    public async Task SwitchToLmStudio_And_SwitchToOllama_UpdateSettingsAndModel()
    {
        var vm = new AiComposerViewModel();

        await vm.SwitchToLmStudioAsync();
        Assert.Equal(AiProviderKind.LMStudio, vm.Settings.Provider);
        Assert.Equal(AiSettings.DefaultLmStudioEndpoint, vm.Settings.EndpointUrl);
        Assert.Equal(AiSettings.DefaultLmStudioModel, vm.Settings.ModelName);
        Assert.Equal("google/gemma-4-e2b", vm.SelectedModel);
        Assert.Contains("LM Studio", vm.StatusText);

        await vm.SwitchToOllamaAsync();
        Assert.Equal(AiProviderKind.Ollama, vm.Settings.Provider);
        Assert.Equal(AiSettings.DefaultOllamaEndpoint, vm.Settings.EndpointUrl);
        Assert.Equal(AiSettings.DefaultModelName, vm.Settings.ModelName);
        Assert.Contains("Ollama", vm.StatusText);
    }

    [Fact]
    public void PromptCustomization_TogglesEditorAndAppliesPresets()
    {
        var vm = new AiComposerViewModel();

        Assert.False(vm.IsPromptEditorOpen);
        Assert.Equal("Agent", vm.ActivePromptPresetName);
        Assert.Equal(AiSettings.DefaultSystemPrompt, vm.CustomSystemPrompt);
        Assert.True(vm.CustomPromptLength > 50);

        vm.TogglePromptEditor();
        Assert.True(vm.IsPromptEditorOpen);

        // Switch to Concise
        vm.SetComposerConcisePrompt();
        Assert.Equal("Concise", vm.ActivePromptPresetName);
        Assert.Equal(AiSettings.ConciseSystemPrompt, vm.Settings.SystemPrompt);
        Assert.Equal(AiSettings.ConciseSystemPrompt, vm.CustomSystemPrompt);
        Assert.Contains("Concise", vm.StatusText);

        // Switch to Reviewer
        vm.SetComposerReviewerPrompt();
        Assert.Equal("Reviewer", vm.ActivePromptPresetName);
        Assert.Equal(AiSettings.ReviewerSystemPrompt, vm.Settings.SystemPrompt);

        // Switch to TDD
        vm.SetComposerTddPrompt();
        Assert.Equal("TDD", vm.ActivePromptPresetName);
        Assert.Equal(AiSettings.TddArchitectSystemPrompt, vm.Settings.SystemPrompt);

        // Custom prompt text
        vm.CustomSystemPrompt = "You are an expert Roslyn compiler hacker who only outputs pure AST code.";
        Assert.Equal("Custom", vm.ActivePromptPresetName);
        vm.ApplyCustomPrompt();
        Assert.Equal("You are an expert Roslyn compiler hacker who only outputs pure AST code.", vm.Settings.SystemPrompt);
        Assert.Contains("Custom", vm.StatusText);

        // Reset prompt
        vm.ResetComposerPrompt();
        Assert.Equal("Agent", vm.ActivePromptPresetName);
        Assert.Equal(AiSettings.DefaultSystemPrompt, vm.Settings.SystemPrompt);
    }

    [Fact]
    public void PromptCustomization_PersistsToSettingsStore_AndSyncsBack()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"studio_settings_{Guid.NewGuid():N}.json");
        try
        {
            var store = new PdfEditorApp.Plugins.CSharpEditor.Services.Settings.StudioSettingsStore(tempFile);
            var vm = new AiComposerViewModel(settingsStore: store);

            Assert.Equal("Agent", vm.ActivePromptPresetName);

            // Change prompt to Concise and apply
            vm.SetComposerConcisePrompt();
            Assert.Equal(AiSettings.ConciseSystemPrompt, vm.Settings.SystemPrompt);

            // Verify store now has Concise prompt
            var reloaded = store.GetSettings();
            Assert.NotNull(reloaded.Ai);
            Assert.Equal(AiSettings.ConciseSystemPrompt, reloaded.Ai.SystemPrompt);

            // Change store directly and verify vm syncs
            var updated = store.GetSettings();
            updated.Ai.SystemPrompt = AiSettings.TddArchitectSystemPrompt;
            store.SaveSettings(updated);

            Assert.Equal(AiSettings.TddArchitectSystemPrompt, vm.CustomSystemPrompt);
            Assert.Equal("TDD", vm.ActivePromptPresetName);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
