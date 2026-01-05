using System.Text.Json.Nodes;
using Markdig;
using Microsoft.Extensions.Options;

public class VllmService : LlmChatCompletionBase
{
    public VllmService(IJsonService jsonService, ILlmClientFactory llmClientFactory, IOptions<SystemPrompts> systemPrompts, MarkdownPipeline pipeline, ILogger<VllmService> logger) : base(jsonService, llmClientFactory, systemPrompts, pipeline, logger)
    {
    }

    public override JsonObject CreateRequestChoice(List<ChatMessageRequest> messagesRequest, List<string> choices, LlmModelConfig model)
    {
        throw new NotImplementedException();
    }

    public override JsonObject CreateRequestJsonChema<TModel>(List<ChatMessageRequest> messagesRequest, LlmModelConfig model)
    {
        throw new NotImplementedException();
    }
}