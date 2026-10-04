using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models.AI;
using PdfEditorApp.Plugins.CSharpEditor.Services.AI.Agent;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.AI;

public partial class AiComposerViewModel
{
    [ObservableProperty]
    private string _endpointUrl = AiSettings.DefaultOllamaEndpoint;

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private bool? _isEndpointOnline;

    [ObservableProperty]
    private string _endpointStatusText = "Unchecked";

    [ObservableProperty]
    private bool _isCheckingEndpoint;

    [ObservableProperty]
    private bool _isConnectionDrawerOpen;

    private void InitializeConnectionState()
    {
        EndpointUrl = _settings.EndpointUrl ?? AiSettings.DefaultOllamaEndpoint;
        ApiKey = _settings.ApiKey ?? string.Empty;
        _ = PingEndpointAsync();
    }

    partial void OnEndpointUrlChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            _settings.EndpointUrl = value.Trim();
            if (_coordinator != null)
            {
                _coordinator = new AiAgentExecutionCoordinator(_settings, _toolRegistry);
            }
            _ = PingEndpointAsync();
        }
    }

    partial void OnApiKeyChanged(string value)
    {
        _settings.ApiKey = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (_coordinator != null)
        {
            _coordinator = new AiAgentExecutionCoordinator(_settings, _toolRegistry);
        }
    }

    [RelayCommand]
    public void ToggleConnectionDrawer()
    {
        IsConnectionDrawerOpen = !IsConnectionDrawerOpen;
    }

    [RelayCommand]
    public async Task PingEndpointAsync()
    {
        if (IsCheckingEndpoint) return;
        IsCheckingEndpoint = true;
        EndpointStatusText = "Checking...";

        try
        {
            var result = await _discoveryService.PingEndpointAsync(_settings.EndpointUrl, _settings.Provider, _settings.ApiKey);
            IsEndpointOnline = result.IsOnline;
            EndpointStatusText = result.IsOnline
                ? $"Online ({result.Latency.TotalMilliseconds:F0}ms)"
                : (result.ErrorMessage ?? "Offline");

            if (result.IsOnline)
            {
                await RefreshModelsAsync();
            }
        }
        catch (Exception ex)
        {
            IsEndpointOnline = false;
            EndpointStatusText = ex.Message;
        }
        finally
        {
            IsCheckingEndpoint = false;
        }
    }

    [RelayCommand]
    public async Task AutoDetectLocalServiceAsync()
    {
        if (IsCheckingEndpoint) return;
        IsCheckingEndpoint = true;
        EndpointStatusText = "Auto-detecting local LLMs (1234, 11434, 11443)...";

        try
        {
            var detected = await _discoveryService.DetectRunningLocalServiceAsync();
            if (detected != null)
            {
                _settings.Provider = detected.Provider;
                _settings.EndpointUrl = detected.EndpointUrl;
                _settings.ModelName = detected.ModelName;
                EndpointUrl = detected.EndpointUrl;

                SelectedModel = detected.ModelName;
                IsEndpointOnline = true;
                EndpointStatusText = $"Detected {detected.Provider} ({detected.ModelName})";

                if (_coordinator != null)
                {
                    _coordinator = new AiAgentExecutionCoordinator(_settings, _toolRegistry);
                }

                await RefreshModelsAsync();
            }
            else
            {
                IsEndpointOnline = false;
                EndpointStatusText = "No running LLM found on localhost:1234 or 11434";
            }
        }
        catch (Exception ex)
        {
            IsEndpointOnline = false;
            EndpointStatusText = $"Detection failed: {ex.Message}";
        }
        finally
        {
            IsCheckingEndpoint = false;
        }
    }
}
