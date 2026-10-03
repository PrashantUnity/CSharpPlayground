using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.AI;

namespace PdfEditorApp.Plugins.CSharpEditor.Models.AI;

/// <summary>
/// A single message entry in the agent chat and composer feed.
/// </summary>
public partial class ChatMessageItem : ObservableObject
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public DateTime Timestamp { get; init; } = DateTime.Now;

    [ObservableProperty]
    private ChatRole _role = ChatRole.User;

    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private bool _isStreaming;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReasoning))]
    private string? _reasoningContent;

    [ObservableProperty]
    private bool _isReasoningExpanded = false;

    /// <summary>
    /// Autonomous plan steps executed by the agent for this turn.
    /// </summary>
    public ObservableCollection<AgentStepItem> Steps { get; } = new();

    /// <summary>
    /// Files modified during this turn for user inspection and diff review.
    /// </summary>
    public ObservableCollection<ModifiedFileItem> ModifiedFiles { get; } = new();

    public bool HasSteps => Steps.Count > 0;

    public bool HasModifiedFiles => ModifiedFiles.Count > 0;

    public bool HasReasoning => !string.IsNullOrWhiteSpace(ReasoningContent);

    public bool IsUser => Role == ChatRole.User;

    public bool IsAssistant => Role == ChatRole.Assistant;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public void ToggleReasoningExpanded()
    {
        IsReasoningExpanded = !IsReasoningExpanded;
    }
}
