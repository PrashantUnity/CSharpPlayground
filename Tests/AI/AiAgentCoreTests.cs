using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using PdfEditorApp.Plugins.CSharpEditor.Models.AI;
using PdfEditorApp.Plugins.CSharpEditor.Services.AI.Core;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class AiAgentCoreTests
{
    [Fact]
    public void AiSettings_Defaults_AreCorrect()
    {
        var settings = new AiSettings();

        Assert.Equal(AiProviderKind.Ollama, settings.Provider);
        Assert.Equal(AiSettings.DefaultOllamaEndpoint, settings.EndpointUrl);
        Assert.Equal(AiSettings.DefaultModelName, settings.ModelName);
        Assert.Equal(0.2f, settings.Temperature);
        Assert.Equal(4096, settings.MaxOutputTokens);
        Assert.False(settings.AutoApproveEdits);
        Assert.Contains("Fry AI", settings.SystemPrompt);
    }

    [Fact]
    public void AiSettings_Clone_CreatesIndependentCopy()
    {
        var original = new AiSettings
        {
            EndpointUrl = "http://localhost:11443",
            ModelName = "llama3.2:3b",
            AutoApproveEdits = true,
            Temperature = 0.5f
        };

        var clone = original.Clone();

        Assert.Equal(original.EndpointUrl, clone.EndpointUrl);
        Assert.Equal(original.ModelName, clone.ModelName);
        Assert.Equal(original.AutoApproveEdits, clone.AutoApproveEdits);
        Assert.Equal(original.Temperature, clone.Temperature);

        clone.EndpointUrl = "http://localhost:1234";
        Assert.NotEqual(original.EndpointUrl, clone.EndpointUrl);
    }

    [Theory]
    [InlineData("http://localhost:11434", AiProviderKind.Ollama, "http://localhost:11434/v1")]
    [InlineData("http://localhost:11434/", AiProviderKind.Ollama, "http://localhost:11434/v1")]
    [InlineData("http://localhost:11434/v1", AiProviderKind.Ollama, "http://localhost:11434/v1")]
    [InlineData("http://localhost:11443", AiProviderKind.Ollama, "http://localhost:11443/v1")]
    [InlineData("http://localhost:1234", AiProviderKind.LMStudio, "http://localhost:1234/v1")]
    [InlineData("http://localhost:1234/v1", AiProviderKind.LMStudio, "http://localhost:1234/v1")]
    [InlineData("http://localhost:1234", AiProviderKind.OpenAiCompatible, "http://localhost:1234/v1")]
    [InlineData("https://api.openai.com/v1", AiProviderKind.OpenAI, "https://api.openai.com/v1")]
    public void NormalizeEndpoint_FormatsUrlsCorrectly(string input, AiProviderKind provider, string expected)
    {
        var uri = AiChatClientFactory.NormalizeEndpoint(input, provider);
        Assert.Equal(new Uri(expected), uri);
    }

    [Fact]
    public void AiChatClientFactory_CreateChatClient_ReturnsConfiguredClient()
    {
        var settings = new AiSettings
        {
            EndpointUrl = "http://localhost:11434",
            ModelName = "qwen2.5-coder:7b"
        };

        var client = AiChatClientFactory.CreateChatClient(settings);

        Assert.NotNull(client);
        Assert.IsAssignableFrom<IChatClient>(client);
    }

    [Fact]
    public void ModifiedFileItem_CalculateLineMetrics_ComputesAddedAndDeleted()
    {
        var item = new ModifiedFileItem
        {
            FilePath = "/path/to/Script.cs",
            OriginalContent = "line 1\nline 2\nline 3",
            ModifiedContent = "line 1\nline 2 (modified)\nline 3\nline 4"
        };

        item.CalculateLineMetrics();

        Assert.Equal(2, item.LinesAdded); // "line 2 (modified)" and "line 4"
        Assert.Equal(1, item.LinesDeleted); // "line 2"
        Assert.Equal("+2 -1", item.SummaryText);
    }

    [Fact]
    public void ChatMessageItem_TracksStepsAndModifiedFiles()
    {
        var message = new ChatMessageItem
        {
            Role = ChatRole.Assistant,
            Content = "I have updated the code."
        };

        var step = new AgentStepItem
        {
            Title = "Read Script.cs",
            Status = AgentStepStatus.Completed,
            ToolName = "read_file"
        };

        var file = new ModifiedFileItem
        {
            FilePath = "Script.cs",
            LinesAdded = 5,
            LinesDeleted = 2
        };

        message.Steps.Add(step);
        message.ModifiedFiles.Add(file);

        Assert.True(message.HasSteps);
        Assert.True(message.HasModifiedFiles);
        Assert.True(message.IsAssistant);
        Assert.Equal("CheckCircleOutline", step.StatusIconKind);
        Assert.Equal("#22C55E", step.StatusColorHex);
    }

    [Fact]
    public async Task AiModelDiscoveryService_ParsesOllamaTagsResponse()
    {
        var jsonResponse = """
            {
              "models": [
                { "name": "qwen2.5-coder:7b", "modified_at": "2026-10-01" },
                { "name": "llama3.2:latest", "modified_at": "2026-09-20" }
              ]
            }
            """;

        var handler = new MockHttpMessageHandler(jsonResponse);
        using var client = new HttpClient(handler);
        var discovery = new AiModelDiscoveryService(client);

        var models = await discovery.GetAvailableModelsAsync("http://localhost:11434", AiProviderKind.Ollama);

        Assert.Equal(2, models.Count);
        Assert.Contains("qwen2.5-coder:7b", models);
        Assert.Contains("llama3.2:latest", models);
    }

    [Fact]
    public async Task AiModelDiscoveryService_PingEndpoint_ReturnsSuccessWhenOnline()
    {
        var handler = new MockHttpMessageHandler("""{"status":"ok"}""");
        using var client = new HttpClient(handler);
        var discovery = new AiModelDiscoveryService(client);

        var result = await discovery.PingEndpointAsync("http://localhost:11434", AiProviderKind.Ollama);

        Assert.True(result.IsOnline);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ChatMessageItem_ReasoningContent_TogglesAndNotifiesCorrectly()
    {
        var item = new ChatMessageItem();
        Assert.False(item.HasReasoning);
        Assert.False(item.IsReasoningExpanded);

        bool hasReasoningChanged = false;
        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(item.HasReasoning))
            {
                hasReasoningChanged = true;
            }
        };

        item.ReasoningContent = "First, let's explore the file system...";
        Assert.True(item.HasReasoning);
        Assert.True(hasReasoningChanged);

        item.ToggleReasoningExpanded();
        Assert.True(item.IsReasoningExpanded);

        item.ToggleReasoningExpanded();
        Assert.False(item.IsReasoningExpanded);
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseContent;
        private readonly HttpStatusCode _statusCode;

        public MockHttpMessageHandler(string responseContent, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _responseContent = responseContent;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseContent)
            };
            return Task.FromResult(response);
        }
    }
}
