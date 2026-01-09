
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;
using Microsoft.Extensions.Options;

public interface ILlmChatCompletion
{
    Task<string> ChatCompletionAsync(List<ChatMessageRequest> request);
    Task<string> ChatChoiceAsync(List<ChatMessageRequest> request, List<string> choices);
    Task<TModel> ChatGenQAsAsync<TModel>(List<ChatMessageRequest> messagesRequest) where TModel : class;
}

public abstract class LlmChatCompletionBase : ILlmChatCompletion
{
    private readonly ILlmClientFactory _llmClientFactory;
    protected readonly IJsonService _jsonService;
    private readonly MarkdownPipeline _pipeline;
    private ILogger _logger;

    protected LlmChatCompletionBase(IJsonService jsonService, ILlmClientFactory llmClientFactory,
                            IOptions<SystemPrompts> systemPrompts, MarkdownPipeline pipeline, ILogger logger)
    {
        _llmClientFactory = llmClientFactory;
        _jsonService = jsonService;
        _pipeline = pipeline;
        _logger = logger;
    }

    public abstract JsonObject CreateRequestJsonChema<TModel>(List<ChatMessageRequest> messagesRequest, LlmModelConfig model) where TModel : class;
    public abstract JsonObject CreateRequestChoice(List<ChatMessageRequest> messagesRequest, List<string> choices, LlmModelConfig model);

    public virtual string ProcessResponse(PipelineResponse response, bool haveThinking = false)
    {

        // Console.WriteLine($"Resonse: {response.Content}");
        string responseJson = response.Content.ToString();

        JsonObject resObj = JsonSerializer.Deserialize<JsonObject>(responseJson)!;

        string rawContent = resObj["choices"]?[0]?["message"]?["content"]?.GetValue<string>() ?? "";

        if (string.IsNullOrEmpty(rawContent))
        {
            throw new InvalidOperationException("API response content was empty or could not be deserialized.");
        }
        if (haveThinking)
        {
            rawContent = Regex.Replace(rawContent, @"<think>[\s\S]*?</think>", "", RegexOptions.IgnoreCase);
        }
        string cleanText = ClearText(rawContent);

        return cleanText;
    }

    public async Task<string> ChatChoiceAsync(List<ChatMessageRequest> request, List<string> choices)
    {
        try
        {
            (var _client, ChatProviderModel providerModelConfig) = _llmClientFactory.GetChatLlmProviderModelChoice();
            JsonObject requestJson = CreateRequestChoice(request, choices, providerModelConfig.modelConfig);
            string requestString = _jsonService.Serialize(requestJson);
            BinaryData data = new BinaryData(requestString);
            BinaryContent content = BinaryContent.Create(data);

            var res = await _client.CompleteChatAsync(content);
            PipelineResponse response = res.GetRawResponse();
            // _logger.LogDebug("ChatChoiceAsync response status: {StatusCode}", response.Status);

            string stringChoice = ProcessResponse(response, providerModelConfig.modelConfig.HaveThinking);
            stringChoice = LlmChatHelper.ParseChoice(stringChoice, choices);
            if (string.IsNullOrEmpty(stringChoice))
            {
                _logger.LogError("ChatChoiceAsync returned empty choice");
                throw new InvalidOperationException("API response content was empty for choice.");
            }
            _logger.LogInformation("ChatChoiceAsync completed successfully with choice: {Choice}", stringChoice);
            return stringChoice;
        }
        catch (ClientResultException ex)
        {
            if (ex.Status == 400)
            {
                _logger.LogError(ex, "ChatChoiceAsync failed - Model does not support choices. StatusCode: {StatusCode}", ex.Status);
                throw new ArgumentException($"Error Calling Nvidia API. StatusCode: {ex.Status}. Message: Model do not support choices", ex);
            }
            _logger.LogError(ex, "ChatChoiceAsync failed with API error. StatusCode: {StatusCode}", ex.Status);
            throw new HttpRequestException($"Error Calling Nvidia API. StatusCode: {ex.Status}. Message: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ChatChoiceAsync failed with unexpected error");
            throw new InvalidOperationException("Error ChatWithStructuredChoiceAsync: ", ex);
        }
    }

    public async Task<TModel> ChatGenQAsAsync<TModel>(List<ChatMessageRequest> messagesRequest) where TModel : class
    {
        _logger.LogInformation("Starting ChatGenQAsAsync for type {ModelType}", typeof(TModel).Name);
        string cleanText = string.Empty;
        try
        {
            (var client, ChatProviderModel providerModelConfig) = _llmClientFactory.GetChatLlmProviderModelGenQAs();
            JsonObject requestJson = CreateRequestJsonChema<TModel>(messagesRequest, providerModelConfig.modelConfig);
            string requestString = _jsonService.Serialize(requestJson);

            BinaryData data = new BinaryData(requestString);
            BinaryContent content = BinaryContent.Create(data);

            if (client is null)
            {
                _logger.LogError("ChatGenQAsAsync failed - Client is null");
                throw new ArgumentNullException("Client is null. Please implement GetLlmClient()");
            }
            _logger.LogDebug("Sending ChatGenQAsAsync request to LLM with model: {ModelName}", providerModelConfig.modelConfig.ModelName);
            var res = await client.CompleteChatAsync(content);
            _logger.LogDebug("ChatGenQAsAsync response status: {StatusCode}", res.GetRawResponse().Status);
            PipelineResponse response = res.GetRawResponse();

            cleanText = ProcessResponse(response, providerModelConfig.modelConfig.HaveThinking);

            TModel tModelResult = JsonSerializer.Deserialize<TModel>(cleanText)!;
            

            return tModelResult;
        }
        catch (ClientResultException ex)
        {
            _logger.LogError(ex, "ChatGenQAsAsync failed with API error. StatusCode: {StatusCode}", ex.Status);
            throw new InvalidOperationException($"Error Calling Nvidia API. StatusCode: {ex.Status}", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogInformation("ChatGenQAsAsync - Model returned invalid JSON, attempting to parse with markdig");
            // Console.WriteLine($"Response: {cleanText}");
            _logger.LogDebug("Trying to process response by markdig...");

            List<Block> allBlock = MarkdownServiceHelper.GetAllBlock(cleanText, _pipeline);
            FencedCodeBlock? fencedCodeBlock = null;
            foreach (var block in allBlock)
            {
                if (block is FencedCodeBlock fenced)
                {
                    _logger.LogDebug("Found FencedCodeBlock with language: {Language}", fenced.Info);
                    fencedCodeBlock = fenced;
                    break;
                }
            }
            if (fencedCodeBlock is null)
            {
                _logger.LogError("ChatGenQAsAsync - No JSON FencedCodeBlock found in response. Response: {Response}", cleanText);
                throw new InvalidOperationException("Model do not return block FencedCodeBlock json", ex);
            }
            string language = fencedCodeBlock.Info ?? "";

            if (language == "json")
            {
                cleanText = string.Join("\n", fencedCodeBlock.Lines);
                cleanText = LlmChatHelper.EscapeNewlinesInsideJsonStrings(cleanText);
                cleanText = LlmChatHelper.CleanJsonWithWindowsPath(cleanText);
                _logger.LogInformation("ChatGenQAsAsync - Successfully extracted JSON from FencedCodeBlock");
            }
            try
            {
                TModel tModelResult = _jsonService.Deserialize<TModel>(cleanText);
                _logger.LogInformation("ChatGenQAsAsync - Successfully deserialized JSON after markdig processing");
                // Console.WriteLine($"Model result: {JsonSerializer.Serialize(tModelResult)}");
                return tModelResult;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "ChatGenQAsAsync - Failed to deserialize JSON after markdig processing. CleanText: {CleanText}", cleanText);
                throw new InvalidOperationException("Try process response by markdig failed", e);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ChatGenQAsAsync failed with unexpected error");
            throw new InvalidOperationException("Error ChatWithStructuredJsonSchema: ", ex);
        }
    }

    public async Task<string> ChatCompletionAsync(List<ChatMessageRequest> messagesRequest)
    {
        _logger.LogInformation("Starting ChatCompletionAsync");
        (var client, ChatProviderModel providerModelConfig) = _llmClientFactory.GetChatLlmProviderModelGenQAs();
        if (client is null)
        {
            _logger.LogError("ChatCompletionAsync failed - Client is null");
            throw new ArgumentNullException("Client is null. Please implement GetLlmClient()");
        }
        // var tmp = client.CompleteChat()
        var modelConfig = providerModelConfig.modelConfig;
        JsonObject requestJson = new JsonObject()
        {
            ["model"] = modelConfig.ModelName,
            ["messages"] = _jsonService.SerializeToNode(messagesRequest),
            ["temperature"] = modelConfig.Temperature,
            ["max_tokens"] = modelConfig.MaxTokens,
            ["stream"] = false
        };
        string requestString = _jsonService.Serialize(requestJson);
        BinaryData data = new BinaryData(requestString);
        BinaryContent content = BinaryContent.Create(data);
        _logger.LogDebug("Sending ChatCompletionAsync request to LLM with model: {ModelName}", modelConfig.ModelName);
        var res = await client.CompleteChatAsync(content);
        _logger.LogDebug("ChatCompletionAsync response status: {StatusCode}", res.GetRawResponse().Status);

        PipelineResponse response = res.GetRawResponse();

        var cleanText = ProcessResponse(response, true);
        _logger.LogInformation("ChatCompletionAsync completed successfully");
        return cleanText;
    }
    public string ClearText(string text)
    {
        return text.Replace("<|return|>", "").Trim();
    }

}

