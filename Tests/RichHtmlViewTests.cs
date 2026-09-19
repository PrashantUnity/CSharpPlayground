using System.Linq;
using Avalonia.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Views;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class RichHtmlViewTests
{
    private static StackPanel GetRootPanel(RichHtmlView view) =>
        view.FindControl<StackPanel>("RootPanel")!;

    [Fact]
    public void Construction_WithNoDataContext_DoesNotThrowAndRendersNothing()
    {
        var view = new RichHtmlView();
        Assert.Empty(GetRootPanel(view).Children);
    }

    [Fact]
    public void SetDataContext_NullOrWhitespace_RendersNothing()
    {
        var view = new RichHtmlView { DataContext = "   " };
        Assert.Empty(GetRootPanel(view).Children);
    }

    [Fact]
    public void SimpleParagraph_RendersOneBlock()
    {
        var view = new RichHtmlView { DataContext = "<p>Hello world</p>" };
        Assert.Single(GetRootPanel(view).Children);
    }

    [Fact]
    public void HeadingsAndParagraphs_RenderOneBlockEach()
    {
        var view = new RichHtmlView
        {
            DataContext = "<h1>Title</h1><p>First paragraph.</p><p>Second paragraph.</p>"
        };
        Assert.Equal(3, GetRootPanel(view).Children.Count);
    }

    [Fact]
    public void UnorderedList_RendersListPanelWithOneLinePerItem()
    {
        var view = new RichHtmlView
        {
            DataContext = "<ul><li>One</li><li>Two</li><li>Three</li></ul>"
        };
        var root = GetRootPanel(view);
        var listPanel = Assert.IsType<StackPanel>(Assert.Single(root.Children));
        Assert.Equal(3, listPanel.Children.Count);
    }

    [Fact]
    public void InlineFormatting_BoldItalicCodeAndLink_DoesNotThrow()
    {
        var view = new RichHtmlView
        {
            DataContext = "<p>Some <b>bold</b>, <i>italic</i>, <code>code</code> and a " +
                          "<a href=\"https://example.com\">link</a>.</p>"
        };
        Assert.Single(GetRootPanel(view).Children);
    }

    [Fact]
    public void LineBreak_InsideParagraph_DoesNotThrow()
    {
        var view = new RichHtmlView { DataContext = "<p>Line one<br/>Line two</p>" };
        Assert.Single(GetRootPanel(view).Children);
    }

    [Fact]
    public void LooseTextWithNoBlockTags_StillRendersAsParagraph()
    {
        var view = new RichHtmlView { DataContext = "Just plain text, no tags at all." };
        Assert.Single(GetRootPanel(view).Children);
    }

    [Fact]
    public void UnclosedAndMismatchedTags_DoNotThrow()
    {
        var view = new RichHtmlView
        {
            DataContext = "<p>Unclosed bold: <b>oops <i>nested</p><div>stray block tag</div>"
        };
        var exception = Record.Exception(() => GetRootPanel(view));
        Assert.Null(exception);
    }

    [Fact]
    public void UnknownTags_AreSwallowedNotLeakedAsText()
    {
        var view = new RichHtmlView
        {
            DataContext = "<p>Before <span class=\"x\">middle</span> after</p>"
        };
        var root = GetRootPanel(view);
        var tb = Assert.IsType<SelectableTextBlock>(Assert.Single(root.Children));
        var text = string.Concat(tb.Inlines!.Select(i => (i as Avalonia.Controls.Documents.Run)?.Text));
        Assert.DoesNotContain("span", text);
        Assert.Contains("middle", text);
    }

    [Fact]
    public void MalformedInput_NeverThrows_EvenIfParsingFails()
    {
        var pathological = "<h1>Unclosed<p><li>orphan li outside ul<b><i><code>" +
                            new string('<', 50) + new string('>', 5);
        var exception = Record.Exception(() => new RichHtmlView { DataContext = pathological });
        Assert.Null(exception);
    }

    [Fact]
    public void ReassigningDataContext_RebuildsFromScratch()
    {
        var view = new RichHtmlView { DataContext = "<p>First</p><p>Second</p>" };
        Assert.Equal(2, GetRootPanel(view).Children.Count);

        view.DataContext = "<p>Only one now</p>";
        Assert.Single(GetRootPanel(view).Children);

        view.DataContext = null;
        Assert.Empty(GetRootPanel(view).Children);
    }

    [Fact]
    public void IsFullHtmlDocument_DetectsDoctypeAndHtml5Pages()
    {
        Assert.True(RichHtmlView.IsFullHtmlDocument("<!DOCTYPE html><html><body>Hi</body></html>"));
        Assert.True(RichHtmlView.IsFullHtmlDocument("<canvas id=\"game\"></canvas>"));
        Assert.True(RichHtmlView.IsFullHtmlDocument("<script>console.log('hi');</script>"));
        Assert.True(RichHtmlView.IsFullHtmlDocument("<style>body { margin: 0; }</style>"));
        Assert.True(RichHtmlView.IsFullHtmlDocument("<svg width=\"100\" height=\"100\"></svg>"));

        Assert.False(RichHtmlView.IsFullHtmlDocument("<p>Just a simple paragraph</p>"));
        Assert.False(RichHtmlView.IsFullHtmlDocument("<h1>Title</h1><ul><li>Item</li></ul>"));
        Assert.False(RichHtmlView.IsFullHtmlDocument(null));
        Assert.False(RichHtmlView.IsFullHtmlDocument("   "));
    }

    [Fact]
    public void ExtractTitle_ExtractsDocumentTitleOrFallback()
    {
        var html = "<!DOCTYPE html><html><head><title>Minimal Snake</title></head></html>";
        Assert.Equal("Minimal Snake", RichHtmlView.ExtractTitle(html));

        Assert.Equal("Fallback", RichHtmlView.ExtractTitle("<p>No title tag</p>", "Fallback"));
    }

    [Fact]
    public void SanitizeHtmlForText_StripsStyleAndScriptBlocksCompletely()
    {
        var html = "<style>:root { color: red; }</style><p>Visible content</p><script>alert('hidden');</script>";
        var sanitized = RichHtmlView.SanitizeHtmlForText(html);

        Assert.DoesNotContain("color: red", sanitized);
        Assert.DoesNotContain("alert('hidden')", sanitized);
        Assert.Contains("Visible content", sanitized);
    }

    [Fact]
    public void FullHtml5SnakeGame_InitializesInRichHtmlViewWithoutThrowing()
    {
        var snakeHtml = """
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <title>Minimal Snake</title>
                <style>
                    :root {
                        --bg-body: #0b0f19;
                        --bg-container: #131927;
                        --bg-canvas: #0e121e;
                        --accent-green: #20c477;
                        --accent-green-hover: #1ab069;
                        --text-main: #ffffff;
                        --food-color: #ef4444;
                    }

                    body {
                        background-color: var(--bg-body);
                        color: var(--text-main);
                        font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
                        display: flex;
                        justify-content: center;
                        align-items: center;
                        height: 100vh;
                        margin: 0;
                        overflow: hidden;
                    }

                    .game-container {
                        background-color: var(--bg-container);
                        padding: 24px;
                        border-radius: 16px;
                        box-shadow: 0 10px 30px rgba(0, 0, 0, 0.5);
                        width: 400px;
                    }

                    .header {
                        display: flex;
                        justify-content: space-between;
                        align-items: center;
                        margin-bottom: 20px;
                        font-size: 1.1rem;
                        font-weight: 600;
                        color: #64748b;
                    }

                    #score {
                        color: var(--accent-green);
                    }

                    .canvas-wrapper {
                        position: relative;
                        width: 100%;
                        aspect-ratio: 1 / 1;
                        background-color: var(--bg-canvas);
                        border-radius: 12px;
                        overflow: hidden;
                    }

                    canvas {
                        display: block;
                        width: 100%;
                        height: 100%;
                    }

                    #gameOverScreen {
                        position: absolute;
                        top: 0;
                        left: 0;
                        width: 100%;
                        height: 100%;
                        background-color: var(--bg-canvas);
                        display: flex;
                        flex-direction: column;
                        justify-content: center;
                        align-items: center;
                        opacity: 0;
                        pointer-events: none;
                        transition: opacity 0.3s ease;
                    }

                    #gameOverScreen.active {
                        opacity: 1;
                        pointer-events: all;
                    }

                    h2 {
                        font-size: 2rem;
                        margin: 0 0 16px 0;
                    }

                    p {
                        font-size: 1.1rem;
                        margin: 0 0 32px 0;
                    }

                    #finalScore {
                        color: var(--accent-green);
                        font-weight: bold;
                    }

                    button {
                        background-color: var(--accent-green);
                        color: #0b0f19;
                        border: none;
                        padding: 12px 24px;
                        font-size: 1rem;
                        font-weight: 600;
                        border-radius: 8px;
                        cursor: pointer;
                        transition: background-color 0.2s ease, transform 0.1s ease;
                    }

                    button:hover {
                        background-color: var(--accent-green-hover);
                    }

                    button:active {
                        transform: scale(0.96);
                    }
                </style>
            </head>
            <body>

                <div class="game-container">
                    <div class="header">
                        <span>Score</span>
                        <span id="score">0</span>
                    </div>
                    
                    <div class="canvas-wrapper">
                        <canvas id="gameCanvas" width="400" height="400"></canvas>
                        
                        <div id="gameOverScreen">
                            <h2>Game Over</h2>
                            <p>Final Score: <span id="finalScore">0</span></p>
                            <button id="playAgainBtn">Play Again</button>
                        </div>
                    </div>
                </div>

                <script>
                    const canvas = document.getElementById("gameCanvas");
                    const ctx = canvas.getContext("2d");
                    const scoreElement = document.getElementById("score");
                    const finalScoreElement = document.getElementById("finalScore");
                    const gameOverScreen = document.getElementById("gameOverScreen");
                    const playAgainBtn = document.getElementById("playAgainBtn");

                    const gridSize = 20;
                    const tileCount = canvas.width / gridSize;

                    let snake = [];
                    let food = { x: 0, y: 0 };
                    let dx = 0;
                    let dy = 0;
                    let score = 0;
                    let gameLoop;
                    let isGameOver = false;

                    function initGame() {
                        snake = [
                            { x: 10, y: 10 },
                        ];
                        dx = 0;
                        dy = -1;
                        score = 0;
                        isGameOver = false;
                        scoreElement.innerText = score;
                        gameOverScreen.classList.remove("active");
                        placeFood();
                        
                        if(gameLoop) clearInterval(gameLoop);
                        gameLoop = setInterval(update, 100);
                    }

                    function update() {
                        if (isGameOver) return;
                        const head = { x: snake[0].x + dx, y: snake[0].y + dy };
                        if (head.x < 0 || head.x >= tileCount || head.y < 0 || head.y >= tileCount) {
                            endGame();
                            return;
                        }
                        for (let i = 0; i < snake.length; i++) {
                            if (head.x === snake[i].x && head.y === snake[i].y) {
                                endGame();
                                return;
                            }
                        }
                        snake.unshift(head);
                        if (head.x === food.x && head.y === food.y) {
                            score += 10;
                            scoreElement.innerText = score;
                            placeFood();
                        } else {
                            snake.pop();
                        }
                        draw();
                    }

                    function draw() {
                        ctx.fillStyle = "#0e121e";
                        ctx.fillRect(0, 0, canvas.width, canvas.height);
                        ctx.fillStyle = "#ef4444";
                        ctx.beginPath();
                        ctx.arc(food.x * gridSize + gridSize/2, food.y * gridSize + gridSize/2, gridSize/2.5, 0, Math.PI * 2);
                        ctx.fill();
                        ctx.fillStyle = "#20c477";
                        snake.forEach((part, index) => {
                            ctx.fillRect(part.x * gridSize, part.y * gridSize, gridSize - 1, gridSize - 1);
                        });
                    }

                    function placeFood() {
                        food.x = Math.floor(Math.random() * tileCount);
                        food.y = Math.floor(Math.random() * tileCount);
                        snake.forEach(part => {
                            if (part.x === food.x && part.y === food.y) {
                                placeFood(); 
                            }
                        });
                    }

                    function endGame() {
                        isGameOver = true;
                        clearInterval(gameLoop);
                        finalScoreElement.innerText = score;
                        gameOverScreen.classList.add("active");
                    }

                    playAgainBtn.addEventListener("click", initGame);
                    initGame();
                </script>
            </body>
            </html>
            """;

        var exception = Record.Exception(() => new RichHtmlView { DataContext = snakeHtml });
        Assert.Null(exception);

        var view = new RichHtmlView { DataContext = snakeHtml };
        var webContainer = view.FindControl<Border>("WebContainer");
        Assert.NotNull(webContainer);
        Assert.True(webContainer.IsVisible);

        var title = view.FindControl<TextBlock>("WebTitleText");
        Assert.NotNull(title);
        Assert.Equal("Minimal Snake", title.Text);
    }
}
