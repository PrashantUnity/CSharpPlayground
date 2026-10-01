using System;
using System.Threading;

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

public static class StudioLoadingExtensions
{
    public static IDisposable BeginLoading(this IStudioLoadingState state, string title, string subtitle = "")
    {
        state.LoadingTitle = title;
        state.LoadingSubtitle = subtitle;
        state.IsLoading = true;
        return new LoadingScope(state);
    }

    private sealed class LoadingScope : IDisposable
    {
        private readonly IStudioLoadingState _state;
        private int _disposed;

        public LoadingScope(IStudioLoadingState state) => _state = state;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _state.IsLoading = false;
            }
        }
    }
}
