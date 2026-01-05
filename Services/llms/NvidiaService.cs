using System.Text.Json;
using System.Text.Json.Nodes;
using Markdig;
using Microsoft.Extensions.Options;


public class NvidiaService : LlmChatCompletionBase
{
    public NvidiaService(IJsonService jsonService, ILlmClientFactory llmClientFactory, IOptions<SystemPrompts> systemPrompts, MarkdownPipeline pipeline, ILogger<NvidiaService> logger) : 
    base(jsonService, llmClientFactory, systemPrompts, pipeline, logger)
    {
    }

    public override JsonObject CreateRequestJsonChema<TModel>(List<ChatMessageRequest> messagesRequest, LlmModelConfig model) where TModel : class
    {
        JsonObject requestBinary = new JsonObject()
            {
            ["model"] = model.ModelName,
                ["messages"] = _jsonService.SerializeToNode(messagesRequest),
                ["temperature"] = model.Temperature,
                ["max_tokens"] = model.MaxTokens,
                ["stream"] = false,
                ["nvext"] = new JsonObject()
                {
                    ["guided_json"] = _jsonService.CreatejsonChema<TModel>()
                }
            };

        return requestBinary;
    }

    public override JsonObject CreateRequestChoice(List<ChatMessageRequest> messagesRequest,List<string> choices, LlmModelConfig model)
    {
        JsonObject requestBinary = new JsonObject()
                {
                    ["model"] = model.ModelName,
                    ["messages"] = _jsonService.SerializeToNode(messagesRequest),
                    ["temperature"] = model.Temperature,
                    ["max_tokens"] = model.MaxTokens,
                    ["stream"] = false,
                    ["guided_choice"] = JsonSerializer.SerializeToNode(choices)
                };

        return requestBinary;
    }


}