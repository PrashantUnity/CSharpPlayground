namespace PdfEditorApp.Plugins.CSharpEditor.Models.AI;

/// <summary>
/// Supported AI providers for local and cloud LLM execution.
/// </summary>
public enum AiProviderKind
{
    /// <summary>Locally hosted Ollama instance (default: http://localhost:11434, supports custom ports like 11443).</summary>
    Ollama,

    /// <summary>Locally hosted LM Studio REST server (default: http://localhost:1234/v1).</summary>
    LMStudio,

    /// <summary>Any OpenAI-compatible server (vLLM, LocalAI, custom proxies).</summary>
    OpenAiCompatible,

    /// <summary>Official OpenAI API (cloud).</summary>
    OpenAI,

    /// <summary>Azure OpenAI endpoint (cloud).</summary>
    AzureOpenAI
}
