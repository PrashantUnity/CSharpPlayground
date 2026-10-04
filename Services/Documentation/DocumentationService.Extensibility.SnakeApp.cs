using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Documentation;

public partial class DocumentationService
{
    private static DocCodeSnippet CreateSnakeMiniAppScriptSnippet()
    {
        return new DocCodeSnippet
        {
            Id = "ch10_snake_app_script",
            Title = "Playable Mini-App Script: Custom Snake Game",
            Description = "A complete, beautifully styled script matching SnakeGameWindow with glowing food, directional eyes, borderless draggable window, and persistent high scores.",
            Code = """
                // Run with Ctrl+Alt+R (Cmd+Alt+R) in any editor tab or notebook!
                using System;
                using System.Collections.Generic;
                using Avalonia;
                using Avalonia.Controls;
                using Avalonia.Input;
                using Avalonia.Layout;
                using Avalonia.Media;
                using Avalonia.Threading;
                using FrySharp.Sdk;

                Dispatcher.UIThread.Post(() =>
                {
                    const int GridCols = 20;
                    const int GridRows = 20;
                    const int CellSize = 15; // 300x300 board

                    int bestScore = App?.State.Get<int>("snake.best_score") ?? 0;
                    int score = 0;
                    bool isPlaying = false;
                    (int X, int Y) dir = (1, 0);
                    (int X, int Y) food = (4, 14);
                    var snake = new List<(int X, int Y)> { (10, 10), (9, 10), (8, 10) };

                    var win = new Window
                    {
                        Title = "Snake",
                        Width = 360,
                        Height = 440,
                        CanResize = false,
                        WindowDecorations = WindowDecorations.None,
                        Background = Brushes.Transparent,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen
                    };

                    var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(115) };
                    var canvas = new Canvas { Width = 300, Height = 300 };

                    var scoreText = new TextBlock
                    {
                        Text = "0",
                        Foreground = new SolidColorBrush(Color.Parse("#00E676")),
                        FontWeight = FontWeight.Bold,
                        FontSize = 14
                    };

                    var bestText = new TextBlock
                    {
                        Text = bestScore.ToString(),
                        Foreground = Brushes.White,
                        FontWeight = FontWeight.SemiBold,
                        FontSize = 14
                    };

                    var startBtn = new Button
                    {
                        Content = "Start",
                        Background = new SolidColorBrush(Color.Parse("#00E676")),
                        Foreground = Brushes.Black,
                        FontWeight = FontWeight.Bold,
                        CornerRadius = new CornerRadius(18),
                        Padding = new Thickness(18, 6),
                        BorderThickness = new Thickness(0)
                    };

                    Border CreateEye(double left, double top)
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

                    void DrawBoard()
                    {
                        canvas.Children.Clear();

                        // 1. Subtle grid lines
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
                                canvas.Children.Add(cell);
                            }
                        }

                        // 2. Glowing Food
                        var foodBorder = new Border
                        {
                            Width = 14,
                            Height = 14,
                            CornerRadius = new CornerRadius(7),
                            Background = new SolidColorBrush(Color.Parse("#FF334B")),
                            BoxShadow = new BoxShadows(new BoxShadow
                            {
                                Color = Color.FromArgb(160, 255, 51, 75),
                                Blur = 8,
                                Spread = 1
                            })
                        };
                        Canvas.SetLeft(foodBorder, food.X * CellSize + 0.5);
                        Canvas.SetTop(foodBorder, food.Y * CellSize + 0.5);
                        canvas.Children.Add(foodBorder);

                        // 3. Snake Segments & Direction-Aware Eyes
                        for (int i = 0; i < snake.Count; i++)
                        {
                            var seg = snake[i];
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
                                var eyeCanvas = new Canvas { Width = 14, Height = 14 };
                                double e1L = 8, e1T = 2, e2L = 8, e2T = 8;
                                if (dir == (-1, 0)) { e1L = 2; e1T = 2; e2L = 2; e2T = 8; }
                                else if (dir == (0, -1)) { e1L = 2; e1T = 2; e2L = 8; e2T = 2; }
                                else if (dir == (0, 1)) { e1L = 2; e1T = 8; e2L = 8; e2T = 8; }

                                eyeCanvas.Children.Add(CreateEye(e1L, e1T));
                                eyeCanvas.Children.Add(CreateEye(e2L, e2T));
                                segBorder.Child = eyeCanvas;
                            }

                            Canvas.SetLeft(segBorder, seg.X * CellSize + 0.5);
                            Canvas.SetTop(segBorder, seg.Y * CellSize + 0.5);
                            canvas.Children.Add(segBorder);
                        }
                    }

                    void ResetGame()
                    {
                        snake.Clear();
                        snake.Add((10, 10));
                        snake.Add((9, 10));
                        snake.Add((8, 10));
                        dir = (1, 0);
                        food = (4, 14);
                        score = 0;
                        scoreText.Text = "0";
                        isPlaying = false;
                        startBtn.Content = "Start";
                        DrawBoard();
                    }

                    void TogglePlay()
                    {
                        isPlaying = !isPlaying;
                        if (isPlaying) { startBtn.Content = "Pause"; timer.Start(); }
                        else { startBtn.Content = "Resume"; timer.Stop(); }
                    }

                    timer.Tick += (_, _) =>
                    {
                        var head = snake[0];
                        var next = (X: (head.X + dir.X + GridCols) % GridCols, Y: (head.Y + dir.Y + GridRows) % GridRows);

                        if (snake.Contains(next))
                        {
                            timer.Stop();
                            isPlaying = false;
                            startBtn.Content = "Start";
                            App?.UI.ShowInfo($"Game Over! Final Score: {score}");
                            ResetGame();
                            return;
                        }

                        snake.Insert(0, next);
                        if (next == food)
                        {
                            score += 10;
                            scoreText.Text = score.ToString();
                            if (score > bestScore)
                            {
                                bestScore = score;
                                bestText.Text = bestScore.ToString();
                                App?.State.Set("snake.best_score", bestScore);
                            }

                            var rand = new Random();
                            for (int i = 0; i < 100; i++)
                            {
                                var pt = (rand.Next(GridCols), rand.Next(GridRows));
                                if (!snake.Contains(pt)) { food = pt; break; }
                            }
                        }
                        else snake.RemoveAt(snake.Count - 1);

                        DrawBoard();
                    };

                    win.KeyDown += (_, e) =>
                    {
                        switch (e.Key)
                        {
                            case Key.Up or Key.W when dir.Y != 1: dir = (0, -1); break;
                            case Key.Down or Key.S when dir.Y != -1: dir = (0, 1); break;
                            case Key.Left or Key.A when dir.X != 1: dir = (-1, 0); break;
                            case Key.Right or Key.D when dir.X != -1: dir = (1, 0); break;
                            case Key.Space: TogglePlay(); break;
                        }
                    };

                    // --- 1. Header Bar ---
                    var headerGrid = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("Auto, *, Auto, Auto"),
                        Margin = new Thickness(0, 0, 0, 12),
                        Background = Brushes.Transparent
                    };
                    headerGrid.PointerPressed += (_, e) => win.BeginMoveDrag(e);

                    var titleStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
                    titleStack.Children.Add(new TextBlock { Text = "🐍", FontSize = 18, VerticalAlignment = VerticalAlignment.Center });
                    titleStack.Children.Add(new TextBlock { Text = "Snake", FontSize = 16, FontWeight = FontWeight.Bold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });
                    Grid.SetColumn(titleStack, 0);

                    var scoreStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center };
                    scoreStack.Children.Add(new TextBlock { Text = "Score", Foreground = new SolidColorBrush(Color.Parse("#94A3B8")), FontSize = 13 });
                    scoreStack.Children.Add(scoreText);
                    scoreStack.Children.Add(new TextBlock { Text = "  Best", Foreground = new SolidColorBrush(Color.Parse("#94A3B8")), FontSize = 13 });
                    scoreStack.Children.Add(bestText);
                    Grid.SetColumn(scoreStack, 1);

                    var btnMin = new Button { Content = "—", Width = 26, Height = 26, Background = Brushes.Transparent, Foreground = new SolidColorBrush(Color.Parse("#94A3B8")), BorderThickness = new Thickness(0), Padding = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
                    btnMin.Click += (_, _) => win.WindowState = WindowState.Minimized;
                    Grid.SetColumn(btnMin, 2);

                    var btnClose = new Button { Content = "✕", Width = 26, Height = 26, Background = Brushes.Transparent, Foreground = new SolidColorBrush(Color.Parse("#94A3B8")), BorderThickness = new Thickness(0), Padding = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
                    btnClose.Click += (_, _) => { timer.Stop(); win.Close(); };
                    Grid.SetColumn(btnClose, 3);

                    headerGrid.Children.Add(titleStack);
                    headerGrid.Children.Add(scoreStack);
                    headerGrid.Children.Add(btnMin);
                    headerGrid.Children.Add(btnClose);

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
                        VerticalAlignment = VerticalAlignment.Center,
                        Child = canvas
                    };

                    // --- 3. Footer Bar ---
                    var footerGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto, *"), Margin = new Thickness(0, 12, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                    startBtn.Click += (_, _) => TogglePlay();
                    Grid.SetColumn(startBtn, 0);

                    var hintLabel = new TextBlock { Text = "Arrows / WASD • Space", Foreground = new SolidColorBrush(Color.Parse("#64748B")), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                    Grid.SetColumn(hintLabel, 1);
                    footerGrid.Children.Add(startBtn);
                    footerGrid.Children.Add(hintLabel);

                    // --- Layout Assembly ---
                    var layout = new Grid { RowDefinitions = new RowDefinitions("Auto, *, Auto") };
                    Grid.SetRow(headerGrid, 0); layout.Children.Add(headerGrid);
                    Grid.SetRow(boardBorder, 1); layout.Children.Add(boardBorder);
                    Grid.SetRow(footerGrid, 2); layout.Children.Add(footerGrid);

                    win.Content = new Border
                    {
                        Background = new SolidColorBrush(Color.Parse("#0F141C")),
                        BorderBrush = new SolidColorBrush(Color.Parse("#1E293B")),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(16),
                        ClipToBounds = true,
                        Padding = new Thickness(14),
                        Child = layout
                    };

                    ResetGame();
                    win.Show();
                });
                """
        };
    }
}
