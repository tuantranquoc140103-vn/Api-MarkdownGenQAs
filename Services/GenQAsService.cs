
using Microsoft.Extensions.Options;

public class GenQAsService : IGenQAsService
{
    private readonly SystemPrompts _systemPrompts;
    private readonly LlmChatCompletionBase _llmChatGenQAs;
    private readonly IJsonService _jsonService;
    private readonly string BASE_DIR = AppContext.BaseDirectory;
    private const int MAX_RETRY = 3;
    public GenQAsService(IOptions<SystemPrompts> systemPrompts, ILlmServiceFactory llmServiceFactory, IJsonService jsonService)
    {
        _systemPrompts = systemPrompts.Value;
        _llmChatGenQAs = llmServiceFactory.GetLlmServiceGenQAs();
        _jsonService = jsonService;
    }

    public async Task<List<TextQA>> GenQAsTextAsync(ChunkInfo chunkInfo, string summaryDocument, string nameFile)
    {
        string path = Path.Combine(BASE_DIR, _systemPrompts.GenQAsText.PathTemplatePrompt);
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
                    new ChatMessageRequest{Role = ChatRole.System, Content = _systemPrompts.GenQAsText.SystemPrompt},
                    new ChatMessageRequest{Role = ChatRole.User, Content = prompt}
                };

                // var documentSummaryPackage = await _llmChatGenQAs.ChatGenQAsAsync<List<SummaryQA>>(messagesRequest);
                var documentSummaryPackage = await _llmChatGenQAs.ChatGenQAsAsync<List<TextQA>>(messagesRequest);
                // Console.WriteLine(documentSummaryPackage);
                return documentSummaryPackage;
            }
            catch (Exception e)
            {
                Console.WriteLine($"GenQAsSummaryAsync error. Try {i + 1}/{MAX_RETRY}");
                Console.WriteLine($"Log error: {e.Message}");
            }
        }

        return new List<TextQA>();
    }

    public async Task<List<SummaryQA>> GenQAsSumaryAsync(string dataSource, string nameFile)
    {
        List<ChatMessageRequest> messagesRequest;
        string path = Path.Combine(BASE_DIR, _systemPrompts.GenQAsSummary.PathTemplatePrompt);
        string templatePromptGenQAsSummary = File.ReadAllText(path);
        string jsonSchema = _jsonService.Serialize(_jsonService.CreatejsonChema<List<SummaryQA>>());
        string promptGenQAsSummary = string.Format(templatePromptGenQAsSummary, jsonSchema, nameFile, dataSource);
        for (int i = 0; i < MAX_RETRY; i++)
        {
            try
            {
                

                messagesRequest = new List<ChatMessageRequest>()
                {
                    new ChatMessageRequest{Role = ChatRole.System, Content = _systemPrompts.GenQAsSummary.SystemPrompt},
                    new ChatMessageRequest{Role = ChatRole.User, Content = promptGenQAsSummary}
                };

                // var documentSummaryPackage = await _llmChatGenQAs.ChatGenQAsAsync<List<SummaryQA>>(messagesRequest);
                var documentSummaryPackage = await _llmChatGenQAs.ChatCompletionAsync(messagesRequest);
                Console.WriteLine(documentSummaryPackage);
                // return documentSummaryPackage;
                return new List<SummaryQA>();
            }
            catch (Exception e)
            {
                Console.WriteLine($"GenQAsSummaryAsync error. Try {i + 1}/{MAX_RETRY}");
                Console.WriteLine($"Log error: {e.Message}");
            }
        }

        return new List<SummaryQA>();
    }

    public async Task<string> GenSummaryDocumentAsync(string dataSource, string nameFile)
    {
        string path = Path.Combine(BASE_DIR, _systemPrompts.GenSummaryDocument.PathTemplatePrompt);
        string templatePromptGenSummary = await File.ReadAllTextAsync(path);
        string prompt = string.Format(templatePromptGenSummary, nameFile, dataSource);
        List<ChatMessageRequest> messagesRequest;

        for (int i = 0; i < MAX_RETRY; i++)
        {
            try
            {
                messagesRequest = new List<ChatMessageRequest>()
                {
                    new ChatMessageRequest{Role = ChatRole.System, Content = _systemPrompts.GenSummaryDocument.SystemPrompt},
                    new ChatMessageRequest{Role = ChatRole.User, Content = prompt}
                };

                var result = await _llmChatGenQAs.ChatCompletionAsync(messagesRequest);
                return result;
            }
            catch (Exception e)
            {
                Console.WriteLine($"GenQAsSummaryAsync error. Try {i + 1}/{MAX_RETRY}");
                Console.WriteLine($"Log error: {e.Message}");
                // await Task.Delay(1000 * (i + 1));
            }
        }

        return string.Empty;
    }

    public async Task<List<TableQA>> GenQAsTableAsync(ChunkInfo chunkInfo, string summaryDocument, string nameFile)
    {
        List<ChatMessageRequest> messagesRequest;
        try
        {
            string path = Path.Combine(BASE_DIR, _systemPrompts.GenQAsTable.PathTemplatePrompt);
            string templatePrompt = await File.ReadAllTextAsync(path);
            string jsonSchema = _jsonService.Serialize(_jsonService.CreatejsonChema<List<TableQA>>());

            var prompt = string.Format(templatePrompt, nameFile, jsonSchema, summaryDocument, chunkInfo.Title, chunkInfo.TittleHirarchy, chunkInfo.Content);

            messagesRequest = new List<ChatMessageRequest>()
            {
                new ChatMessageRequest{Role = ChatRole.System, Content = _systemPrompts.GenQAsTable.SystemPrompt},
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

                return await _llmChatGenQAs.ChatGenQAsAsync<List<TableQA>>(messagesRequest);
            }
            catch (Exception e)
            {
                Console.WriteLine($"GenQAsChunkTableAsync error. Try {i + 1}/{MAX_RETRY}");
                Console.WriteLine($"Log error: {e.Message}");
            }
        }

        throw new Exception($"GenQAsChunkTableAsync error. Try {MAX_RETRY}/{MAX_RETRY}");
    }
}

