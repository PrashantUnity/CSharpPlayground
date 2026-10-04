using System;
using System.Threading.Tasks;

namespace FrySharp.Sdk;

/// <summary>
/// Placement and layout mode for the AI Assistant / Composer.
/// </summary>
public enum AiDockMode
{
    /// <summary>
    /// Free-floating, draggable canvas overlay on top of the studio workspace (default).
    /// </summary>
    FloatingOverlay = 0,

    /// <summary>
    /// Docked into the Primary Side Bar (Zone 2) alongside Explorer / Search.
    /// </summary>
    DockedSideBar = 1,

    /// <summary>
    /// Docked into the Bottom Tool Deck (Zone 4) alongside Problems / Output / Terminal.
    /// </summary>
    DockedBottomDeck = 2,

    /// <summary>
    /// Extracted / torn out into a standalone native desktop OS window (multi-monitor, floating).
    /// </summary>
    ExtractedWindow = 3
}

/// <summary>
/// Visual styling options for the AI Assistant interface.
/// </summary>
public record AiStyleOptions
{
    public string? AccentColorHex { get; init; }
    public string? BackgroundHex { get; init; }
    public double? Opacity { get; init; }
    public double? CornerRadius { get; init; }
    public double? Width { get; init; }
    public double? Height { get; init; }
    public double? PositionX { get; init; }
    public double? PositionY { get; init; }
}

/// <summary>
/// Window options when extracting the AI Assistant into a standalone desktop window.
/// </summary>
public record AiWindowOptions
{
    public string? Title { get; init; } = "Fry AI Studio Assistant";
    public double? Width { get; init; } = 560;
    public double? Height { get; init; } = 800;
    public bool Topmost { get; init; } = false;
    public bool CenterScreen { get; init; } = true;
}

/// <summary>
/// Execution policies and behavioral guardrails for the AI agent.
/// </summary>
public record AiPolicyOptions
{
    public bool? AutoAcceptDiffs { get; init; }
    public int? MaxSteps { get; init; }
    public bool? EnableBashExecution { get; init; }
    public double? Temperature { get; init; }
}

/// <summary>
/// Target UI area for visual studio capture.
/// </summary>
public enum CaptureTarget
{
    /// <summary>The active code/notebook editor canvas.</summary>
    ActiveEditor = 0,

    /// <summary>The active workspace area (KeepAlivePageHost), excluding floating AI overlays.</summary>
    WorkspaceArea = 1,

    /// <summary>The bottom panel / tool deck (Problems, Output, Terminal, Results).</summary>
    BottomPanel = 2,

    /// <summary>The full host studio window.</summary>
    FullWindow = 3
}

/// <summary>
/// Public API for controlling the AI Assistant's UI styling, layout docking,
/// native window extraction (screen tearing), behavioral personas, and agent policies.
/// </summary>
public interface IAiApi
{
    /// <summary>Current docking mode of the AI Assistant.</summary>
    AiDockMode DockMode { get; }

    /// <summary>Whether the AI Assistant is currently extracted into a standalone desktop window.</summary>
    bool IsExtracted { get; }

    /// <summary>Whether the AI Assistant UI is currently visible.</summary>
    bool IsVisible { get; set; }

    /// <summary>Whether the AI Assistant is minimized to a floating pill / capsule.</summary>
    bool IsMinimized { get; set; }

    /// <summary>The name of the currently active system prompt persona preset.</summary>
    string ActivePersona { get; }

    /// <summary>The name of the currently active LLM model.</summary>
    string ActiveModel { get; }

    /// <summary>The current opacity of the assistant window (0.1 - 1.0).</summary>
    double Opacity { get; }

    /// <summary>Whether OS-level screen capture protection (e.g. NSWindowSharingNone on macOS, WDA_EXCLUDEFROMCAPTURE on Windows) is active.</summary>
    bool IsProtectedFromCapture { get; set; }

    /// <summary>Current styling parameters of the assistant.</summary>
    AiStyleOptions CurrentStyle { get; }

    /// <summary>Current behavioral and execution policy parameters.</summary>
    AiPolicyOptions CurrentPolicy { get; }

    /// <summary>Docks the assistant into the specified layout zone.</summary>
    void DockTo(AiDockMode mode);

    /// <summary>Extracts ("tears out") the assistant into a standalone native OS window.</summary>
    void ExtractToWindow(AiWindowOptions? options = null);

    /// <summary>Re-docks the extracted assistant back into the main studio host.</summary>
    void ReDock(AiDockMode targetMode = AiDockMode.FloatingOverlay);

    /// <summary>Applies visual styling parameters (colors, opacity, dimensions, corner radius).</summary>
    void SetStyle(AiStyleOptions style);

    /// <summary>Sets the assistant window opacity (0.1 to 1.0).</summary>
    void SetOpacity(double opacity);

    /// <summary>Sets the dimensions of the assistant window.</summary>
    void SetDimensions(double width, double height);

    /// <summary>Sets the screen position of the floating overlay.</summary>
    void SetPosition(double x, double y);

    /// <summary>Switches the system prompt persona (e.g. "Agent", "Concise", "Reviewer", "TDD").</summary>
    void SetPersona(string presetName);

    /// <summary>Sets custom system prompt instructions.</summary>
    void SetCustomPrompt(string systemPrompt);

    /// <summary>Switches the active AI model.</summary>
    void SetModel(string modelName);

    /// <summary>Configures agent execution policies (auto-accept diffs, step limits, etc.).</summary>
    void ConfigurePolicy(AiPolicyOptions policy);

    /// <summary>Enables or disables OS-level screen capture protection (Zoom/Teams/OBS exclusion).</summary>
    void SetCaptureProtection(bool enabled);

    /// <summary>
    /// Captures a screenshot of the specified IDE workspace area, automatically
    /// excluding the AI Assistant's own window/overlay from the capture to prevent self-occlusion.
    /// </summary>
    Task<byte[]> CaptureWorkspaceScreenshotAsync(CaptureTarget target = CaptureTarget.WorkspaceArea, bool excludeSelf = true);

    /// <summary>
    /// Captures a screenshot of the specified IDE workspace area to a PNG file,
    /// automatically excluding the AI Assistant's own window/overlay.
    /// </summary>
    Task<string> CaptureWorkspaceScreenshotToFileAsync(string? outputPath = null, CaptureTarget target = CaptureTarget.WorkspaceArea, bool excludeSelf = true);

    /// <summary>Programmatically sends a message or prompt to the assistant.</summary>
    Task SendMessageAsync(string prompt);

    /// <summary>Clears the current conversation history.</summary>
    void ClearChat();

    /// <summary>Event raised whenever the assistant's docking mode changes.</summary>
    event Action<AiDockMode>? DockModeChanged;

    /// <summary>Event raised whenever the assistant's styling options change.</summary>
    event Action<AiStyleOptions>? StyleChanged;

    /// <summary>Event raised whenever screen capture protection state changes.</summary>
    event Action<bool>? CaptureProtectionChanged;
}
