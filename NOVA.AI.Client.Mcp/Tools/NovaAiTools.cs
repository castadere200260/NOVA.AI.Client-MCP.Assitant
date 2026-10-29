using System.ComponentModel;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Y10.AI.Client.Abstractions;
using Y10.AI.Client.Configuration;
using Y10.AI.Client.Models;

namespace NOVA.AI.Client.Mcp.Tools;

/// <summary>MCP tools that expose Y10.AI.Client to MCP-compatible AI assistants and clients.</summary>
[McpServerToolType]
public sealed class NovaAiTools(IAiClientFactory clientFactory, IOptions<AiClientOptions> options)
{
    [McpServerTool(Name = "ask_chatgpt")]
    [Description("Send a prompt to ChatGPT using the configured OpenAI API. Requires AiClient:OpenAI:ApiKey. Optional systemPrompt and model can be supplied.")]
    public Task<string> AskChatGptAsync(
        [Description("The user prompt or question.")] string message,
        [Description("Optional system instructions for the assistant.")] string? systemPrompt = null,
        [Description("Optional OpenAI model override, for example gpt-4o-mini.")] string? model = null,
        CancellationToken cancellationToken = default)
        => AskAsync("ChatGPT", message, systemPrompt, model, cancellationToken);

    [McpServerTool(Name = "ask_ollama")]
    [Description("Send a prompt to a local or remote Ollama model. Ollama must be running and the model must be pulled.")]
    public Task<string> AskOllamaAsync(
        [Description("The user prompt or question.")] string message,
        [Description("Optional system instructions for the assistant.")] string? systemPrompt = null,
        [Description("Optional Ollama model override, for example llama3.2.")] string? model = null,
        CancellationToken cancellationToken = default)
        => AskAsync("Ollama", message, systemPrompt, model, cancellationToken);

    [McpServerTool(Name = "ask_ai")]
    [Description("Send a prompt to the configured default AI provider (AiClient:DefaultProvider), or override it with ChatGPT or Ollama.")]
    public Task<string> AskAiAsync(
        [Description("The user prompt or question.")] string message,
        [Description("Optional provider: ChatGPT or Ollama. Defaults to AiClient:DefaultProvider.")] string? provider = null,
        [Description("Optional system instructions for the assistant.")] string? systemPrompt = null,
        [Description("Optional model override.")] string? model = null,
        CancellationToken cancellationToken = default)
        => AskAsync(provider ?? options.Value.DefaultProvider, message, systemPrompt, model, cancellationToken);

    private async Task<string> AskAsync(
        string provider, string message, string? systemPrompt, string? model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "Error: message is required.";

        try
        {
            var client = clientFactory.GetClient(provider);
            var result = await client.ChatAsync(new AiChatRequest
            {
                Message = message,
                SystemPrompt = systemPrompt,
                Model = model,
                Temperature = 0.2
            }, cancellationToken).ConfigureAwait(false);

            return $"Provider: {result.Provider}\nModel: {result.Model}\n\n{result.Content}";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return "The AI request was cancelled.";
        }
        catch (Exception ex)
        {
            // Return a concise diagnostic to the MCP caller without exposing secrets.
            return $"AI request failed for provider '{provider}': {ex.Message}";
        }
    }
}
