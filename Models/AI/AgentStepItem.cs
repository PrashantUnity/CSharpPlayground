using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PdfEditorApp.Plugins.CSharpEditor.Models.AI;

/// <summary>
/// Status of an individual agent thought or tool execution step.
/// </summary>
public enum AgentStepStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    SelfHealing
}

/// <summary>
/// Represents a discrete step in the agent's autonomous ReAct execution plan.
/// </summary>
public partial class AgentStepItem : ObservableObject
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string? _detail;

    [ObservableProperty]
    private AgentStepStatus _status = AgentStepStatus.Pending;

    [ObservableProperty]
    private string? _toolName;

    [ObservableProperty]
    private string? _argumentsJson;

    [ObservableProperty]
    private string? _outputJson;

    [ObservableProperty]
    private TimeSpan? _duration;

    [ObservableProperty]
    private bool _isExpanded;

    public string StatusIconKind => Status switch
    {
        AgentStepStatus.Pending => "ClockOutline",
        AgentStepStatus.Running => "ProgressClock",
        AgentStepStatus.Completed => "CheckCircleOutline",
        AgentStepStatus.Failed => "CloseCircleOutline",
        AgentStepStatus.SelfHealing => "AutoFix",
        _ => "InformationOutline"
    };

    public string StatusColorHex => Status switch
    {
        AgentStepStatus.Pending => "#808080",
        AgentStepStatus.Running => "#38BDF8",
        AgentStepStatus.Completed => "#22C55E",
        AgentStepStatus.Failed => "#EF4444",
        AgentStepStatus.SelfHealing => "#F59E0B",
        _ => "#808080"
    };
}
