using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models.AI;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.AI.Core;

/// <summary>
/// Status result from testing endpoint connectivity.
/// </summary>
public record EndpointHealthResult(bool IsOnline, TimeSpan Latency, string? ErrorMessage = null);

/// <summary>
/// Service that checks endpoint availability and queries installed local models.
/// </summary>
public class AiModelDiscoveryService
{
    private readonly HttpClient _httpClient;

    public AiModelDiscoveryService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    }

    /// <summary>
    /// Pings the endpoint using the provided AiSettings.
    /// </summary>
    public Task<EndpointHealthResult> PingEndpointAsync(AiSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return PingEndpointAsync(settings.EndpointUrl, settings.Provider, settings.ApiKey, cancellationToken);
    }

    /// <summary>
    /// Pings the endpoint to verify it is reachable and running.
    /// </summary>
    public async Task<EndpointHealthResult> PingEndpointAsync(
        string endpointUrl,
        AiProviderKind provider,
        string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var root = endpointUrl.TrimEnd('/');
            Uri targetUri;

            if (provider == AiProviderKind.Ollama)
            {
                // Ollama responds to GET /api/tags
                targetUri = new Uri(root + "/api/tags");
            }
            else
            {
                // LM Studio and OpenAI-compatible endpoints respond to /v1/models or /models
                targetUri = root.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
                    ? new Uri(root + "/models")
                    : new Uri(root + "/v1/models");
            }

            var request = new HttpRequestMessage(HttpMethod.Get, targetUri);
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            }

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                return new EndpointHealthResult(true, sw.Elapsed);
            }

            return new EndpointHealthResult(false, sw.Elapsed, $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new EndpointHealthResult(false, sw.Elapsed, ex.Message);
        }
    }

    /// <summary>
    /// Automatically probes common local endpoints (LM Studio on 1234, Ollama on 11434/11443)
    /// to detect which local LLM server is currently active.
    /// </summary>
    public async Task<AiSettings?> DetectRunningLocalServiceAsync(CancellationToken cancellationToken = default)
    {
        // 1. Check LM Studio (http://localhost:1234/v1)
        var lmConfig = new AiSettings
        {
            Provider = AiProviderKind.LMStudio,
            EndpointUrl = AiSettings.DefaultLmStudioEndpoint,
            ModelName = AiSettings.DefaultLmStudioModel
        };
        var lmHealth = await PingEndpointAsync(lmConfig, cancellationToken);
        if (lmHealth.IsOnline)
        {
            var models = await GetAvailableModelsAsync(lmConfig, cancellationToken);
            if (models.Count > 0)
            {
                lmConfig.ModelName = models[0];
            }
            return lmConfig;
        }

        // 2. Check Ollama default (http://localhost:11434)
        var ollamaConfig = new AiSettings
        {
            Provider = AiProviderKind.Ollama,
            EndpointUrl = AiSettings.DefaultOllamaEndpoint,
            ModelName = AiSettings.DefaultModelName
        };
        var ollamaHealth = await PingEndpointAsync(ollamaConfig, cancellationToken);
        if (ollamaHealth.IsOnline)
        {
            var models = await GetAvailableModelsAsync(ollamaConfig, cancellationToken);
            if (models.Count > 0)
            {
                ollamaConfig.ModelName = models[0];
            }
            return ollamaConfig;
        }

        // 3. Check custom Ollama port (http://localhost:11443)
        var customOllama = new AiSettings
        {
            Provider = AiProviderKind.Ollama,
            EndpointUrl = "http://localhost:11443",
            ModelName = AiSettings.DefaultModelName
        };
        var customHealth = await PingEndpointAsync(customOllama, cancellationToken);
        if (customHealth.IsOnline)
        {
            var models = await GetAvailableModelsAsync(customOllama, cancellationToken);
            if (models.Count > 0)
            {
                customOllama.ModelName = models[0];
            }
            return customOllama;
        }

        return null;
    }

    /// <summary>
    /// Discovers available models from local Ollama or OpenAI-compatible endpoint using AiSettings.
    /// </summary>
    public Task<IReadOnlyList<string>> GetAvailableModelsAsync(AiSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return GetAvailableModelsAsync(settings.EndpointUrl, settings.Provider, settings.ApiKey, cancellationToken);
    }

    /// <summary>
    /// Discovers available models from local Ollama or OpenAI-compatible endpoint.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetAvailableModelsAsync(
        string endpointUrl,
        AiProviderKind provider,
        string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        var models = new List<string>();

        try
        {
            var trimmedUrl = endpointUrl.TrimEnd('/');
            Uri targetUri;

            if (provider == AiProviderKind.Ollama)
            {
                targetUri = new Uri(trimmedUrl + "/api/tags");
            }
            else
            {
                targetUri = trimmedUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
                    ? new Uri(trimmedUrl + "/models")
                    : new Uri(trimmedUrl + "/v1/models");
            }

            var request = new HttpRequestMessage(HttpMethod.Get, targetUri);

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return models;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (provider == AiProviderKind.Ollama && root.TryGetProperty("models", out var modelsArray))
            {
                foreach (var element in modelsArray.EnumerateArray())
                {
                    if (element.TryGetProperty("name", out var nameProp))
                    {
                        var name = nameProp.GetString();
                        if (!string.IsNullOrEmpty(name))
                        {
                            models.Add(name);
                        }
                    }
                }
            }
            else if (root.TryGetProperty("data", out var dataArray))
            {
                foreach (var element in dataArray.EnumerateArray())
                {
                    if (element.TryGetProperty("id", out var idProp))
                    {
                        var id = idProp.GetString();
                        if (!string.IsNullOrEmpty(id))
                        {
                            models.Add(id);
                        }
                    }
                }
            }
        }
        catch
        {
            // Endpoint might be offline or non-standard format
        }

        return models;
    }
}
