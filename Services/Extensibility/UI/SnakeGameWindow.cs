using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using FrySharp.Sdk;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.UI;

/// <summary>
/// A sleek, floating Snake Game window built as an extensibility showcase for FrySharp.
/// Demonstrates custom Avalonia floating windows, keybindings, DispatcherTimer, and persistent state.
/// </summary>
public class SnakeGameWindow : Window
{
    private const int GridCols = 20;
    private const int GridRows = 20;
    private const int CellSize = 15; // 300x300 board

    private readonly Canvas _boardCanvas;
    private readonly TextBlock _scoreText;
    private readonly TextBlock _bestText;
    private readonly Button _startBtn;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(115) };

    private readonly List<(int X, int Y)> _snake = new();
    private (int X, int Y) _direction = (1, 0);
    private (int X, int Y) _food = (4, 14);
    private int _score = 0;
    private int _bestScore = 0;
    private bool _isPlaying = false;
    private readonly IStudioApp? _app;

    public SnakeGameWindow(IStudioApp? app = null)
    {
        _app = app;
        _bestScore = _app?.State.Get<int>("snake.best_score") ?? 0;

        Title = "Snake";
        Width = 360;
        Height = 440;
        CanResize = false;
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        Background = Brushes.Transparent;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        // Root Window Container
        var rootBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#0F141C")),
            BorderBrush = new SolidColorBrush(Color.Parse("#1E293B")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            ClipToBounds = true,
            Padding = new Thickness(14)
        };

        var mainLayout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto, *, Auto")
        };

        // --- 1. Header Bar ---
        var headerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto, *, Auto, Auto"),
            Margin = new Thickness(0, 0, 0, 12),
            Background = Brushes.Transparent
        };
        headerGrid.PointerPressed += (s, e) => BeginMoveDrag(e);

        var titleStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 8
        };
        titleStack.Children.Add(new TextBlock { Text = "🐍", FontSize = 18, VerticalAlignment = VerticalAlignment.Center });
        titleStack.Children.Add(new TextBlock
        {
            Text = "Snake",
            FontSize = 16,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetColumn(titleStack, 0);
        headerGrid.Children.Add(titleStack);

        var scoreStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 16, 0),
            Spacing = 4
        };
        scoreStack.Children.Add(new TextBlock { Text = "Score", Foreground = new SolidColorBrush(Color.Parse("#94A3B8")), FontSize = 13 });
        _scoreText = new TextBlock
        {
            Text = "0",
            Foreground = new SolidColorBrush(Color.Parse("#00E676")),
            FontWeight = FontWeight.Bold,
            FontSize = 14
        };
        scoreStack.Children.Add(_scoreText);
        scoreStack.Children.Add(new TextBlock { Text = "  Best", Foreground = new SolidColorBrush(Color.Parse("#94A3B8")), FontSize = 13 });
        _bestText = new TextBlock
        {
            Text = _bestScore.ToString(),
            Foreground = Brushes.White,
            FontWeight = FontWeight.SemiBold,
            FontSize = 14
        };
        scoreStack.Children.Add(_bestText);
        Grid.SetColumn(scoreStack, 1);
        headerGrid.Children.Add(scoreStack);

        var btnMin = new Button
        {
            Content = "—",
            Width = 26,
            Height = 26,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.Parse("#94A3B8")),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        btnMin.Click += (_, _) => WindowState = WindowState.Minimized;
        Grid.SetColumn(btnMin, 2);
        headerGrid.Children.Add(btnMin);

        var btnClose = new Button
        {
            Content = "✕",
            Width = 26,
            Height = 26,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.Parse("#94A3B8")),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        btnClose.Click += (_, _) => { _timer.Stop(); Close(); };
        Grid.SetColumn(btnClose, 3);
        headerGrid.Children.Add(btnClose);

        Grid.SetRow(headerGrid, 0);
        mainLayout.Children.Add(headerGrid);

        // --- 2. Board Grid & Canvas ---
        var boardBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#0A0D14")),
            BorderBrush = new SolidColorBrush(Color.Parse("#1A2333")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            ClipToBounds = true,
            Width = 300,
            Height = 300,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        _boardCanvas = new Canvas
        {
            Width = 300,
            Height = 300
        };
        boardBorder.Child = _boardCanvas;
        Grid.SetRow(boardBorder, 1);
        mainLayout.Children.Add(boardBorder);

        // --- 3. Footer Bar ---
        var footerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto, *"),
            Margin = new Thickness(0, 12, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        _startBtn = new Button
        {
            Content = "Start",
            Background = new SolidColorBrush(Color.Parse("#00E676")),
            Foreground = Brushes.Black,
            FontWeight = FontWeight.Bold,
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(18, 6),
            BorderThickness = new Thickness(0)
        };
        _startBtn.Click += (_, _) => TogglePlay();
        Grid.SetColumn(_startBtn, 0);
        footerGrid.Children.Add(_startBtn);

        var hintLabel = new TextBlock
        {
            Text = "Arrows / WASD • Space",
            Foreground = new SolidColorBrush(Color.Parse("#64748B")),
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(hintLabel, 1);
        footerGrid.Children.Add(hintLabel);

        Grid.SetRow(footerGrid, 2);
        mainLayout.Children.Add(footerGrid);

        rootBorder.Child = mainLayout;
        Content = rootBorder;

        // Initialize Game State
        ResetGame();

        // Timer
        _timer.Tick += (_, _) => GameStep();

        // Keyboard Handling
        KeyDown += OnWindowKeyDown;
    }

    private void ResetGame()
    {
        _snake.Clear();
        // Initial 3 segments matching the user mockup
        _snake.Add((10, 10)); // Head
        _snake.Add((9, 10));  // Body
        _snake.Add((8, 10));  // Tail
        _direction = (1, 0);
        _food = (4, 14);
        _score = 0;
        _scoreText.Text = "0";
        _isPlaying = false;
        _startBtn.Content = "Start";
        DrawBoard();
    }

    private void TogglePlay()
    {
        _isPlaying = !_isPlaying;
        if (_isPlaying)
        {
            _startBtn.Content = "Pause";
            _timer.Start();
        }
        else
        {
            _startBtn.Content = "Resume";
            _timer.Stop();
        }
    }

    private void GameStep()
    {
        var head = _snake[0];
        var next = (X: head.X + _direction.X, Y: head.Y + _direction.Y);

        // Wrap around walls
        if (next.X < 0) next.X = GridCols - 1;
        else if (next.X >= GridCols) next.X = 0;
        if (next.Y < 0) next.Y = GridRows - 1;
        else if (next.Y >= GridRows) next.Y = 0;

        // Self collision check
        if (_snake.Contains(next))
        {
            _timer.Stop();
            _isPlaying = false;
            _startBtn.Content = "Start";
            _app?.UI.ShowInfo($"Game Over! Final Score: {_score}");
            ResetGame();
            return;
        }

        _snake.Insert(0, next);

        if (next == _food)
        {
            _score += 10;
            _scoreText.Text = _score.ToString();
            if (_score > _bestScore)
            {
                _bestScore = _score;
                _bestText.Text = _bestScore.ToString();
                _app?.State.Set("snake.best_score", _bestScore);
            }
            SpawnFood();
        }
        else
        {
            _snake.RemoveAt(_snake.Count - 1);
        }

        DrawBoard();
    }

    private void SpawnFood()
    {
        var rand = new Random();
        for (int i = 0; i < 100; i++)
        {
            var pt = (rand.Next(GridCols), rand.Next(GridRows));
            if (!_snake.Contains(pt))
            {
                _food = pt;
                break;
            }
        }
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up or Key.W when _direction.Y != 1:
                _direction = (0, -1);
                break;
            case Key.Down or Key.S when _direction.Y != -1:
                _direction = (0, 1);
                break;
            case Key.Left or Key.A when _direction.X != 1:
                _direction = (-1, 0);
                break;
            case Key.Right or Key.D when _direction.X != -1:
                _direction = (1, 0);
                break;
            case Key.Space:
                TogglePlay();
                break;
        }
    }

    private void DrawBoard()
    {
        _boardCanvas.Children.Clear();

        // 1. Draw subtle grid pattern
        for (int r = 0; r < GridRows; r++)
        {
            for (int c = 0; c < GridCols; c++)
            {
                var cell = new Border
                {
                    Width = CellSize,
                    Height = CellSize,
                    BorderBrush = new SolidColorBrush(Color.Parse("#161F2E")),
                    BorderThickness = new Thickness(0.5)
                };
                Canvas.SetLeft(cell, c * CellSize);
                Canvas.SetTop(cell, r * CellSize);
                _boardCanvas.Children.Add(cell);
            }
        }

        // 2. Draw Food
        var foodBorder = new Border
        {
            Width = 14,
            Height = 14,
            CornerRadius = new CornerRadius(7),
            Background = new SolidColorBrush(Color.Parse("#FF334B")),
            BoxShadow = new BoxShadows(new BoxShadow { Color = Color.FromArgb(160, 255, 51, 75), Blur = 8, Spread = 1 })
        };
        Canvas.SetLeft(foodBorder, _food.X * CellSize + 0.5);
        Canvas.SetTop(foodBorder, _food.Y * CellSize + 0.5);
        _boardCanvas.Children.Add(foodBorder);

        // 3. Draw Snake
        for (int i = 0; i < _snake.Count; i++)
        {
            var seg = _snake[i];
            bool isHead = i == 0;

            var segBorder = new Border
            {
                Width = 14,
                Height = 14,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.Parse("#00E676"))
            };

            if (isHead)
            {
                // Head has cute white eyes with black pupils
                var eyeCanvas = new Canvas { Width = 14, Height = 14 };

                // Position eyes based on direction
                double eye1Left = 7, eye1Top = 2, eye2Left = 7, eye2Top = 8;
                if (_direction == (1, 0)) // Right
                {
                    eye1Left = 8; eye1Top = 2; eye2Left = 8; eye2Top = 8;
                }
                else if (_direction == (-1, 0)) // Left
                {
                    eye1Left = 2; eye1Top = 2; eye2Left = 2; eye2Top = 8;
                }
                else if (_direction == (0, -1)) // Up
                {
                    eye1Left = 2; eye1Top = 2; eye2Left = 8; eye2Top = 2;
                }
                else if (_direction == (0, 1)) // Down
                {
                    eye1Left = 2; eye1Top = 8; eye2Left = 8; eye2Top = 8;
                }

                eyeCanvas.Children.Add(CreateEye(eye1Left, eye1Top));
                eyeCanvas.Children.Add(CreateEye(eye2Left, eye2Top));
                segBorder.Child = eyeCanvas;
            }

            Canvas.SetLeft(segBorder, seg.X * CellSize + 0.5);
            Canvas.SetTop(segBorder, seg.Y * CellSize + 0.5);
            _boardCanvas.Children.Add(segBorder);
        }
    }

    private static Border CreateEye(double left, double top)
    {
        var eye = new Border
        {
            Width = 4.5,
            Height = 4.5,
            CornerRadius = new CornerRadius(2.25),
            Background = Brushes.White,
            ClipToBounds = true
        };
        var pupil = new Border
        {
            Width = 2,
            Height = 2,
            CornerRadius = new CornerRadius(1),
            Background = Brushes.Black,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        eye.Child = pupil;
        Canvas.SetLeft(eye, left);
        Canvas.SetTop(eye, top);
        return eye;
    }
}
