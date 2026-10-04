namespace PdfEditorApp.Plugins.CSharpEditor.Models.AI;

/// <summary>
/// User configuration settings for AI model connectivity, endpoints, and agent behavior.
/// </summary>
public class AiSettings
{
    public const string DefaultOllamaEndpoint = "http://localhost:11434";
    public const string DefaultLmStudioEndpoint = "http://localhost:1234/v1";
    public const string DefaultModelName = "qwen2.5-coder:7b";
    public const string DefaultLmStudioModel = "google/gemma-4-e2b";

    public static readonly string DefaultSystemPrompt = """
        You are Fry AI, an expert C# and .NET 10 coding assistant and pair programmer built into FrySharp.
        The human user is the developer. Never address the user as "Fry AI".
        You assist the developer with C#, .NET 10 document automation, Roslyn scripting, polyglot notebooks, shell tasks, and studio customization.

        Guidelines:
        1. When asked to modify code, add features, or fix bugs, be direct and produce clean, modern C# (C# 13 / .NET 10).
        2. When modifying the active document or a mentioned file, provide the complete updated code or replacement in a ```csharp code block so it can be applied directly to the editor.
        3. Avoid repetitive greetings. Directly answer the user's question or execute the requested task.
        4. You have tools to inspect files, edit files, compile with Roslyn, and run shell commands. Leverage them when in Agent mode.
        5. You can dynamically customize the running studio itself using studio APIs (App.UI, App.Theme, App.Commands, App.Editor).
        """;

    public static readonly string ConciseSystemPrompt = """
        You are Fry AI, a concise, high-efficiency pair programmer built into FrySharp.
        You assist developers with C#, .NET 10, Roslyn scripts, and shell automation.

        Guidelines:
        1. Keep explanations to an absolute minimum. Prioritize code snippets, diffs, and direct answers over conversational prose.
        2. Never repeat the user's prompt or provide unsolicited tutorials.
        3. When modifying code, make minimal, surgical changes that preserve existing style and conventions.
        4. Always write clean, idiomatic C# 13 (.NET 10).
        """;

    public static readonly string ReviewerSystemPrompt = """
        You are Fry AI Code Reviewer, an expert software auditor and security analyst for .NET 10 and C#.

        Guidelines:
        1. Analyze code rigorously for performance bottlenecks, unnecessary heap allocations, memory leaks, and thread safety issues.
        2. Inspect boundary conditions, nullability handling (C# 8+ nullable reference types), and exception handling.
        3. Enforce idiomatic C# patterns, Clean Architecture, and component reusability.
        4. Clearly point out issues with severity levels (Critical, Warning, Suggestion) and provide concise remediation examples.
        """;

    public static readonly string TddArchitectSystemPrompt = """
        You are Fry AI TDD Architect, a test-driven development specialist built into FrySharp.

        Guidelines:
        1. Prioritize unit testing, testability, and deterministic behavior above all else.
        2. When asked to implement or fix functionality, design the xUnit test cases first, covering edge cases, null arguments, and error states.
        3. Use FluentAssertions and Moq where applicable to create readable, maintainable assertions.
        4. Ensure 100% backward compatibility and 0 compiler warnings.
        """;

    /// <summary>
    /// Identifies the preset name matching the given prompt text, or returns "Custom".
    /// </summary>
    public static string GetPromptPresetName(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt) || prompt.Trim() == DefaultSystemPrompt.Trim())
            return "Agent";
        if (prompt.Trim() == ConciseSystemPrompt.Trim())
            return "Concise";
        if (prompt.Trim() == ReviewerSystemPrompt.Trim())
            return "Reviewer";
        if (prompt.Trim() == TddArchitectSystemPrompt.Trim())
            return "TDD";
        return "Custom";
    }

    /// <summary>Active provider kind (Ollama, LMStudio, OpenAiCompatible, OpenAI, AzureOpenAI).</summary>
    public AiProviderKind Provider { get; set; } = AiProviderKind.Ollama;

    /// <summary>The base endpoint URL (e.g. http://localhost:11434, http://localhost:1234/v1).</summary>
    public string EndpointUrl { get; set; } = DefaultOllamaEndpoint;

    /// <summary>The target model identifier (e.g. google/gemma-4-e2b, qwen2.5-coder:7b, llama3.2, deepseek-r1).</summary>
    public string ModelName { get; set; } = DefaultModelName;

    /// <summary>Optional API key (only required for authenticated cloud or remote proxies).</summary>
    public string? ApiKey { get; set; }

    /// <summary>Model temperature for response sampling (0.0 = deterministic, 0.7 = creative).</summary>
    public float Temperature { get; set; } = 0.2f;

    /// <summary>Maximum tokens to generate per response.</summary>
    public int MaxOutputTokens { get; set; } = 4096;

    /// <summary>
    /// When true, captures and displays internal chain-of-thought or reasoning content from models (like Gemma 4, DeepSeek R1).
    /// </summary>
    public bool EnableReasoning { get; set; } = true;

    /// <summary>
    /// When true, the agent applies file edits directly to the workspace without waiting for manual confirmation.
    /// When false, edits are held in a diff review card for user approval.
    /// </summary>
    public bool AutoApproveEdits { get; set; } = false;

    /// <summary>Custom system instructions guiding agent behavior and persona.</summary>
    public string SystemPrompt { get; set; } = DefaultSystemPrompt;

    /// <summary>Creates a clone of current settings.</summary>
    public AiSettings Clone() => new()
    {
        Provider = Provider,
        EndpointUrl = EndpointUrl,
        ModelName = ModelName,
        ApiKey = ApiKey,
        Temperature = Temperature,
        MaxOutputTokens = MaxOutputTokens,
        EnableReasoning = EnableReasoning,
        AutoApproveEdits = AutoApproveEdits,
        SystemPrompt = SystemPrompt
    };
}
