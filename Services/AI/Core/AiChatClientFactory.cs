using System;
using System.ClientModel;
using System.Net.Http;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using PdfEditorApp.Plugins.CSharpEditor.Models.AI;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.AI.Core;

/// <summary>
/// Factory that instantiates configured <see cref="IChatClient"/> instances from <see cref="AiSettings"/>.
/// </summary>
public static class AiChatClientFactory
{
    /// <summary>
    /// Creates an <see cref="IChatClient"/> configured according to the given <paramref name="settings"/>.
    /// </summary>
    public static IChatClient CreateChatClient(AiSettings settings, HttpClient? httpClient = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var endpoint = NormalizeEndpoint(settings.EndpointUrl, settings.Provider);
        var apiKey = string.IsNullOrWhiteSpace(settings.ApiKey) ? "ollama" : settings.ApiKey;
        var model = string.IsNullOrWhiteSpace(settings.ModelName) ? AiSettings.DefaultModelName : settings.ModelName;

        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = endpoint
        };

        if (httpClient != null)
        {
            // If custom transport is required, clientOptions can be customized here
        }

        var openAiClient = new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions);
        var chatClient = openAiClient.GetChatClient(model);

        // Convert the OpenAI ChatClient to Microsoft.Extensions.AI IChatClient
        IChatClient meaiClient = chatClient.AsIChatClient();

        // Build middleware pipeline with automatic function invocation support
        var builder = new ChatClientBuilder(meaiClient)
            .UseFunctionInvocation();

        return builder.Build();
    }

    /// <summary>
    /// Normalizes endpoint URLs, ensuring appropriate /v1 suffixes for local Ollama and custom endpoints.
    /// </summary>
    public static Uri NormalizeEndpoint(string url, AiProviderKind provider)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            url = provider == AiProviderKind.LMStudio
                ? AiSettings.DefaultLmStudioEndpoint
                : AiSettings.DefaultOllamaEndpoint;
        }

        url = url.TrimEnd('/');

        if (provider == AiProviderKind.Ollama)
        {
            // Ollama's OpenAI-compatible completions live under /v1
            if (!url.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            {
                url += "/v1";
            }
        }
        else if (provider is AiProviderKind.OpenAiCompatible or AiProviderKind.LMStudio)
        {
            if (!url.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) &&
                !url.Contains("/v1/", StringComparison.OrdinalIgnoreCase))
            {
                url += "/v1";
            }
        }

        return new Uri(url);
    }
}
