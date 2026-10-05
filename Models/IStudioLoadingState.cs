using System;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PdfEditorApp.Plugins.CSharpEditor.Models;

/// <summary>
/// State interface for surfaces that can display a visual loading popup / progress overlay.
/// </summary>
public interface IStudioLoadingState
{
    bool IsLoading { get; set; }
    string LoadingTitle { get; set; }
    string LoadingSubtitle { get; set; }
}

/// <summary>
/// Global, centralized loading manager for the entire studio application.
/// Provides thread-safe, ref-counted loading state so operations across any view or service
/// drive the single, global root loading overlay smoothly.
/// </summary>
public sealed class StudioLoadingManager : ObservableObject, IStudioLoadingState
{
    public static StudioLoadingManager Instance { get; } = new();

    private int _loadingCount;
    private bool _isLoading;
    private string _loadingTitle = "Loading...";
    private string _loadingSubtitle = string.Empty;

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string LoadingTitle
    {
        get => _loadingTitle;
        set => SetProperty(ref _loadingTitle, value);
    }

    public string LoadingSubtitle
    {
        get => _loadingSubtitle;
        set => SetProperty(ref _loadingSubtitle, value);
    }

    public IDisposable Begin(string title, string subtitle = "")
    {
        LoadingTitle = title;
        LoadingSubtitle = subtitle;
        Interlocked.Increment(ref _loadingCount);
        IsLoading = true;
        return new GlobalLoadingScope(this);
    }

    internal void End()
    {
        if (Interlocked.Decrement(ref _loadingCount) <= 0)
        {
            _loadingCount = 0;
            IsLoading = false;
        }
    }

    /// <summary>
    /// Resets the loading manager to idle (used primarily by unit tests).
    /// </summary>
    public void Reset()
    {
        _loadingCount = 0;
        IsLoading = false;
        LoadingTitle = "Loading...";
        LoadingSubtitle = string.Empty;
    }

    private sealed class GlobalLoadingScope : IDisposable
    {
        private readonly StudioLoadingManager _manager;
        private int _disposed;

        public GlobalLoadingScope(StudioLoadingManager manager) => _manager = manager;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _manager.End();
            }
        }
    }
}

public static class StudioLoadingExtensions
{
    public static IDisposable BeginLoading(this IStudioLoadingState state, string title, string subtitle = "")
    {
        state.LoadingTitle = title;
        state.LoadingSubtitle = subtitle;
        state.IsLoading = true;

        var globalScope = StudioLoadingManager.Instance.Begin(title, subtitle);
        return new CombinedLoadingScope(state, globalScope);
    }

    private sealed class CombinedLoadingScope : IDisposable
    {
        private readonly IStudioLoadingState _state;
        private readonly IDisposable _globalScope;
        private int _disposed;

        public CombinedLoadingScope(IStudioLoadingState state, IDisposable globalScope)
        {
            _state = state;
            _globalScope = globalScope;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _state.IsLoading = false;
                _globalScope.Dispose();
            }
        }
    }
}
