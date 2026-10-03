using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using PdfEditorApp.Plugins.CSharpEditor.Models.AI;
using PdfEditorApp.Plugins.CSharpEditor.Services.AI.Core;
using PdfEditorApp.Plugins.CSharpEditor.Services.AI.Tools;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.AI.Agent;

/// <summary>
/// Coordinates the autonomous ReAct planning and execution loop for the Cursor-like AI coding agent.
/// Handles multi-step tool invocations, token streaming, Roslyn self-healing feedback, and session checkpointing.
/// </summary>
public class AiAgentExecutionCoordinator
{
    private readonly AiSettings _settings;
    private readonly AiAgentToolRegistry _toolRegistry;
    private readonly IChatClient _chatClient;
    private readonly List<ChatMessage> _chatHistory = new();
    private readonly List<ModifiedFileItem> _sessionModifiedFiles = new();

    public const int MaxAutonomousIterations = 15;

    public IReadOnlyList<ModifiedFileItem> SessionModifiedFiles => _sessionModifiedFiles;
    public IReadOnlyList<ChatMessage> ChatHistory => _chatHistory;

    public AiAgentExecutionCoordinator(
        AiSettings settings,
        AiAgentToolRegistry? toolRegistry = null,
        IChatClient? chatClient = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _toolRegistry = toolRegistry ?? new AiAgentToolRegistry(onFileModified: TrackModifiedFile);
        _chatClient = chatClient ?? AiChatClientFactory.CreateChatClient(_settings);

        InitializeSystemPrompt();
    }

    private void InitializeSystemPrompt()
    {
        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine(_settings.SystemPrompt);

        // Auto-inject workspace architectural rules and guidelines (AGENTS.md, GEMINI.md, CLAUDE.md)
        try
        {
            var projectRules = _toolRegistry.ReadProjectRules();
            if (!projectRules.StartsWith("No project rules found", StringComparison.OrdinalIgnoreCase))
            {
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("<project_rules>");
                promptBuilder.AppendLine(projectRules);
                promptBuilder.AppendLine("</project_rules>");
            }
        }
        catch
        {
            // Tolerate missing workspace or disk permissions
        }

        _chatHistory.Clear();
        _chatHistory.Add(new ChatMessage(ChatRole.System, promptBuilder.ToString()));
    }

    private void TrackModifiedFile(ModifiedFileItem item)
    {
        var existing = _sessionModifiedFiles.FirstOrDefault(f => f.FilePath.Equals(item.FilePath, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            _sessionModifiedFiles.Remove(existing);
        }
        _sessionModifiedFiles.Add(item);
    }

    /// <summary>
    /// Executes an autonomous agent task in response to a user prompt.
    /// Streams tokens, captures reasoning thoughts, and reports tool execution steps and modified files back to the UI.
    /// </summary>
    public async Task<ChatMessageItem> ExecuteTaskAsync(
        string userPrompt,
        string? activeContext = null,
        Action<string>? onTokenChunk = null,
        Action<string>? onReasoningChunk = null,
        Action<AgentStepItem>? onStepUpdate = null,
        CancellationToken cancellationToken = default)
    {
        var assistantMessage = new ChatMessageItem
        {
            Role = ChatRole.Assistant,
            IsStreaming = true
        };

        // Hook up tool registry progress updates to caller
        _toolRegistry.OnStepUpdate = onStepUpdate;

        // Format user message with optional context chips
        var fullPromptBuilder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(activeContext))
        {
            fullPromptBuilder.AppendLine("<context>");
            fullPromptBuilder.AppendLine(activeContext);
            fullPromptBuilder.AppendLine("</context>");
            fullPromptBuilder.AppendLine();
        }
        fullPromptBuilder.Append(userPrompt);

        var userMessage = fullPromptBuilder.ToString();
        _chatHistory.Add(new ChatMessage(ChatRole.User, userMessage));

        var tools = _toolRegistry.BuildToolList();
        var chatOptions = new ChatOptions
        {
            Temperature = _settings.Temperature,
            MaxOutputTokens = _settings.MaxOutputTokens,
            Tools = tools
        };

        var responseText = new StringBuilder();
        var reasoningBuilder = new StringBuilder();
        bool inThinkTag = false;

        try
        {
            // Initial planning step
            var planningStep = new AgentStepItem
            {
                Title = "Formulating Plan & Exploring Context",
                Status = AgentStepStatus.Running
            };
            assistantMessage.Steps.Add(planningStep);
            onStepUpdate?.Invoke(planningStep);

            // Stream response using Microsoft.Extensions.AI
            // The FunctionInvokingChatClient middleware handles tool calls automatically.
            await foreach (var update in _chatClient.GetStreamingResponseAsync(_chatHistory, chatOptions, cancellationToken))
            {
                // 1. Check for provider-native reasoning_content (e.g. LM Studio / OpenAI delta)
                string? nativeReasoning = null;
                if (update.AdditionalProperties != null)
                {
                    if (update.AdditionalProperties.TryGetValue("reasoning_content", out var rc) && rc != null)
                    {
                        nativeReasoning = rc.ToString();
                    }
                    else if (update.AdditionalProperties.TryGetValue("reasoning", out var r) && r != null)
                    {
                        nativeReasoning = r.ToString();
                    }
                }

                if (!string.IsNullOrEmpty(nativeReasoning))
                {
                    reasoningBuilder.Append(nativeReasoning);
                    assistantMessage.ReasoningContent = reasoningBuilder.ToString();
                    onReasoningChunk?.Invoke(nativeReasoning);
                }

                // 2. Check update.Text for <think> ... </think> or standard response text
                if (update.Text != null)
                {
                    var text = update.Text;
                    if (inThinkTag)
                    {
                        int closeIdx = text.IndexOf("</think>", StringComparison.OrdinalIgnoreCase);
                        if (closeIdx >= 0)
                        {
                            var thinkPart = text[..closeIdx];
                            var regularPart = text[(closeIdx + 8)..];
                            inThinkTag = false;

                            if (!string.IsNullOrEmpty(thinkPart))
                            {
                                reasoningBuilder.Append(thinkPart);
                                assistantMessage.ReasoningContent = reasoningBuilder.ToString();
                                onReasoningChunk?.Invoke(thinkPart);
                            }

                            if (!string.IsNullOrEmpty(regularPart))
                            {
                                responseText.Append(regularPart);
                                assistantMessage.Content = responseText.ToString();
                                onTokenChunk?.Invoke(regularPart);
                            }
                        }
                        else
                        {
                            reasoningBuilder.Append(text);
                            assistantMessage.ReasoningContent = reasoningBuilder.ToString();
                            onReasoningChunk?.Invoke(text);
                        }
                    }
                    else
                    {
                        int openIdx = text.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
                        if (openIdx >= 0)
                        {
                            var regularPart = text[..openIdx];
                            var remainder = text[(openIdx + 7)..];
                            inThinkTag = true;

                            if (!string.IsNullOrEmpty(regularPart))
                            {
                                responseText.Append(regularPart);
                                assistantMessage.Content = responseText.ToString();
                                onTokenChunk?.Invoke(regularPart);
                            }

                            int closeIdx = remainder.IndexOf("</think>", StringComparison.OrdinalIgnoreCase);
                            if (closeIdx >= 0)
                            {
                                var thinkPart = remainder[..closeIdx];
                                var trailingRegular = remainder[(closeIdx + 8)..];
                                inThinkTag = false;

                                if (!string.IsNullOrEmpty(thinkPart))
                                {
                                    reasoningBuilder.Append(thinkPart);
                                    assistantMessage.ReasoningContent = reasoningBuilder.ToString();
                                    onReasoningChunk?.Invoke(thinkPart);
                                }

                                if (!string.IsNullOrEmpty(trailingRegular))
                                {
                                    responseText.Append(trailingRegular);
                                    assistantMessage.Content = responseText.ToString();
                                    onTokenChunk?.Invoke(trailingRegular);
                                }
                            }
                            else if (!string.IsNullOrEmpty(remainder))
                            {
                                reasoningBuilder.Append(remainder);
                                assistantMessage.ReasoningContent = reasoningBuilder.ToString();
                                onReasoningChunk?.Invoke(remainder);
                            }
                        }
                        else
                        {
                            responseText.Append(text);
                            assistantMessage.Content = responseText.ToString();
                            onTokenChunk?.Invoke(text);
                        }
                    }
                }
            }

            planningStep.Status = AgentStepStatus.Completed;
            onStepUpdate?.Invoke(planningStep);

            if (reasoningBuilder.Length > 0)
            {
                assistantMessage.ReasoningContent = reasoningBuilder.ToString().Trim();
            }

            // Record assistant turn in history
            _chatHistory.Add(new ChatMessage(ChatRole.Assistant, responseText.ToString()));

            // Associate any files modified during this turn
            foreach (var mod in _sessionModifiedFiles)
            {
                if (!assistantMessage.ModifiedFiles.Any(m => m.FilePath == mod.FilePath))
                {
                    assistantMessage.ModifiedFiles.Add(mod);
                }
            }
        }
        catch (OperationCanceledException)
        {
            assistantMessage.ErrorMessage = "Task was stopped by user.";
        }
        catch (Exception ex)
        {
            assistantMessage.ErrorMessage = ex.Message;
        }
        finally
        {
            assistantMessage.IsStreaming = false;
        }

        return assistantMessage;
    }

    /// <summary>
    /// Resets the conversation and rolls back any uncommitted file changes.
    /// </summary>
    public void RollbackSession()
    {
        foreach (var file in _sessionModifiedFiles)
        {
            if (!string.IsNullOrEmpty(file.OriginalContent) && File.Exists(file.FilePath))
            {
                try { File.WriteAllText(file.FilePath, file.OriginalContent); } catch { }
            }
        }
        _sessionModifiedFiles.Clear();
        InitializeSystemPrompt();
    }
}
