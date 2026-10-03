using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using PdfEditorApp.Plugins.CSharpEditor.Models.AI;
using PdfEditorApp.Plugins.CSharpEditor.Services.AI.Agent;
using PdfEditorApp.Plugins.CSharpEditor.Services.AI.Tools;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class AiAgentCoordinatorTests : IDisposable
{
    private readonly string _testDir;

    public AiAgentCoordinatorTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "FryAiCoordTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, true); } catch { }
        }
    }

    [Fact]
    public async Task ExecuteTaskAsync_StreamsTokensAndCompletesTurn()
    {
        var mockClient = new MockStreamingChatClient(new[] { "Thinking... ", "Creating new file... ", "Done!" });
        var settings = new AiSettings();
        var coordinator = new AiAgentExecutionCoordinator(settings, chatClient: mockClient);

        var tokenChunks = new List<string>();
        var result = await coordinator.ExecuteTaskAsync(
            userPrompt: "Create a hello world script",
            activeContext: "Active file: None",
            onTokenChunk: chunk => tokenChunks.Add(chunk));

        Assert.NotNull(result);
        Assert.False(result.IsStreaming);
        Assert.Equal("Thinking... Creating new file... Done!", result.Content);
        Assert.Equal(3, tokenChunks.Count);
        Assert.Single(result.Steps);
        Assert.Equal(AgentStepStatus.Completed, result.Steps[0].Status);
    }

    [Fact]
    public void RollbackSession_RestoresOriginalFiles()
    {
        var testFile = Path.Combine(_testDir, "RollbackTarget.cs");
        File.WriteAllText(testFile, "original line 1\noriginal line 2");

        var settings = new AiSettings();
        var toolRegistry = new AiAgentToolRegistry();
        var coordinator = new AiAgentExecutionCoordinator(settings, toolRegistry: toolRegistry);

        // Simulate modifying the file via tool registry
        toolRegistry.ModifyFile(testFile, "original line 2", "modified line 2");
        Assert.Contains("modified line 2", File.ReadAllText(testFile));

        // Note: coordinator tracks modified files via callback
        var mockModified = new ModifiedFileItem
        {
            FilePath = testFile,
            OriginalContent = "original line 1\noriginal line 2",
            ModifiedContent = "original line 1\nmodified line 2"
        };

        // Trigger rollback
        typeof(AiAgentExecutionCoordinator)
            .GetField("_sessionModifiedFiles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .SetValue(coordinator, new List<ModifiedFileItem> { mockModified });

        coordinator.RollbackSession();

        Assert.Equal("original line 1\noriginal line 2", File.ReadAllText(testFile));
    }

    private class MockStreamingChatClient : IChatClient
    {
        private readonly IEnumerable<string> _chunks;

        public MockStreamingChatClient(IEnumerable<string> chunks)
        {
            _chunks = chunks;
        }

        public ChatClientMetadata Metadata => new("MockClient");

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, string.Concat(_chunks))));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var chunk in _chunks)
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, chunk);
                await Task.Yield();
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
