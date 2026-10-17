using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Studio;

public partial class StudioDiffViewerControl : UserControl
{
    private bool _isSyncingScroll;

    public StudioDiffViewerControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        var left = this.FindControl<ScrollViewer>("LeftScrollViewer");
        var right = this.FindControl<ScrollViewer>("RightScrollViewer");

        if (left != null && right != null)
        {
            left.ScrollChanged += (s, ev) =>
            {
                if (_isSyncingScroll) return;
                _isSyncingScroll = true;
                try
                {
                    right.Offset = new Vector(right.Offset.X, left.Offset.Y);
                }
                finally
                {
                    _isSyncingScroll = false;
                }
            };

            right.ScrollChanged += (s, ev) =>
            {
                if (_isSyncingScroll) return;
                _isSyncingScroll = true;
                try
                {
                    left.Offset = new Vector(left.Offset.X, right.Offset.Y);
                }
                finally
                {
                    _isSyncingScroll = false;
                }
            };
        }
    }
}
