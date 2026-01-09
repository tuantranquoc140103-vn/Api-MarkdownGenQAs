using Microsoft.Extensions.Options;


public class GenQAsService : IGenQAsService
{
    private readonly IOptions<SystemPrompts> _systemPrompts;
    private readonly LlmChatCompletionBase _llmChatGenQAs;
    private readonly IJsonService _jsonService;
    private readonly ILogger<GenQAsService> _logger;
    private readonly string BASE_DIR = AppContext.BaseDirectory;
    private const int MAX_RETRY = 3;

    public GenQAsService(
        IOptions<SystemPrompts> systemPrompts,
        ILlmServiceFactory llmServiceFactory,
        IJsonService jsonService,
        ILogger<GenQAsService> logger)
    {
        _systemPrompts = systemPrompts;
        _llmChatGenQAs = llmServiceFactory.GetLlmServiceGenQAs();
        _jsonService = jsonService;
        _logger = logger;
    }

    public async Task<List<ChunkQA>> GenQAsTextAsync(ChunkInfo chunkInfo, string summaryDocument, string nameFile)
    {
        string path = Path.Combine(BASE_DIR, _systemPrompts.Value.GenQAsText.PathTemplatePrompt);
        string templatePrompt = File.ReadAllText(path);
        string jsonSchema = _jsonService.Serialize(_jsonService.CreatejsonChema<List<TextQA>>());
        string prompt = string.Format(templatePrompt, jsonSchema, nameFile, summaryDocument, chunkInfo.Content);


        List<ChatMessageRequest> messagesRequest;
        for (int i = 0; i < MAX_RETRY; i++)
        {
            try
            {
                messagesRequest = new List<ChatMessageRequest>()
                {
                    new ChatMessageRequest{Role = ChatRole.System, Content = _systemPrompts.Value.GenQAsText.SystemPrompt},
                    new ChatMessageRequest{Role = ChatRole.User, Content = prompt}
                };

                // var documentSummaryPackage = await _llmChatGenQAs.ChatGenQAsAsync<List<SummaryQA>>(messagesRequest);
                var documentSummaryPackage = await _llmChatGenQAs.ChatGenQAsAsync<List<ChunkQA>>(messagesRequest);
                return documentSummaryPackage;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "GenQAsTextAsync error for file {FileName}. Try {RetryCount}/{MaxRetry}", nameFile, i + 1, MAX_RETRY);
            }
        }

        return new List<ChunkQA>();
    }

    public async Task<List<ChunkQA>> GenQAsSumaryAsync(string dataSource, string nameFile)
    {
        List<ChatMessageRequest> messagesRequest;
        string path = Path.Combine(BASE_DIR, _systemPrompts.Value.GenQAsSummary.PathTemplatePrompt);
        string templatePromptGenQAsSummary = File.ReadAllText(path);
        string jsonSchema = _jsonService.Serialize(_jsonService.CreatejsonChema<List<SummaryQA>>());
        string promptGenQAsSummary = string.Format(templatePromptGenQAsSummary, jsonSchema, nameFile, dataSource);
        for (int i = 0; i < MAX_RETRY; i++)
        {
            try
            {


                messagesRequest = new List<ChatMessageRequest>()
                {
                    new ChatMessageRequest{Role = ChatRole.System, Content = _systemPrompts.Value.GenQAsSummary.SystemPrompt},
                    new ChatMessageRequest{Role = ChatRole.User, Content = promptGenQAsSummary}
                };

                // var documentSummaryPackage = await _llmChatGenQAs.ChatGenQAsAsync<List<SummaryQA>>(messagesRequest);
                var documentSummaryPackage = await _llmChatGenQAs.ChatGenQAsAsync<List<ChunkQA>>(messagesRequest);
                _logger.LogInformation("GenQAsSumaryAsync success for file {FileName}", nameFile);
                return documentSummaryPackage;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "GenQAsSumaryAsync error for file {FileName}. Try {RetryCount}/{MaxRetry}", nameFile, i + 1, MAX_RETRY);
            }
        }

        return new List<ChunkQA>();
    }

    public async Task<string> GenSummaryDocumentAsync(string dataSource, string nameFile)
    {
        string path = Path.Combine(BASE_DIR, _systemPrompts.Value.GenSummaryDocument.PathTemplatePrompt);
        string templatePromptGenSummary = await File.ReadAllTextAsync(path);
        string prompt = string.Format(templatePromptGenSummary, nameFile, dataSource);
        List<ChatMessageRequest> messagesRequest;

        for (int i = 0; i < MAX_RETRY; i++)
        {
            try
            {
                messagesRequest = new List<ChatMessageRequest>()
                {
                    new ChatMessageRequest{Role = ChatRole.System, Content = _systemPrompts.Value.GenSummaryDocument.SystemPrompt},
                    new ChatMessageRequest{Role = ChatRole.User, Content = prompt}
                };

                var result = await _llmChatGenQAs.ChatCompletionAsync(messagesRequest);
                return result;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "GenSummaryDocumentAsync error for file {FileName}. Try {RetryCount}/{MaxRetry}", nameFile, i + 1, MAX_RETRY);
                // await Task.Delay(1000 * (i + 1));
            }
        }

        return string.Empty;
    }

    public async Task<List<ChunkQA>> GenQAsTableAsync(ChunkInfo chunkInfo, string summaryDocument, string nameFile)
    {
        List<ChatMessageRequest> messagesRequest;
        try
        {
            string path = Path.Combine(BASE_DIR, _systemPrompts.Value.GenQAsTable.PathTemplatePrompt);
            string templatePrompt = await File.ReadAllTextAsync(path);
            string jsonSchema = _jsonService.Serialize(_jsonService.CreatejsonChema<List<TableQA>>());

            var prompt = string.Format(templatePrompt, nameFile, jsonSchema, summaryDocument, chunkInfo.Title, chunkInfo.TittleHirarchy, chunkInfo.Content);

            messagesRequest = new List<ChatMessageRequest>()
            {
                new ChatMessageRequest{Role = ChatRole.System, Content = _systemPrompts.Value.GenQAsTable.SystemPrompt},
                new ChatMessageRequest{Role = ChatRole.User, Content = prompt}
            };
        }
        catch (Exception e)
        {
            throw new Exception($"GenQAsChunkTableAsync error. Try {MAX_RETRY}/{MAX_RETRY}", e);
        }
        for (int i = 0; i < MAX_RETRY; i++)
        {
            try
            {

                return await _llmChatGenQAs.ChatGenQAsAsync<List<ChunkQA>>(messagesRequest);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "GenQAsTableAsync error for file {FileName}. Try {RetryCount}/{MaxRetry}", nameFile, i + 1, MAX_RETRY);
            }
        }

        // throw new Exception($"GenQAsChunkTableAsync error. Try {MAX_RETRY}/{MAX_RETRY}");
        _logger.LogError("GenQAsTableAsync error for file {FileName}. Try {RetryCount}/{MaxRetry}", nameFile, MAX_RETRY, MAX_RETRY);
        return new List<ChunkQA>();
    }
}

