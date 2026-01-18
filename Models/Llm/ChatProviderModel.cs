
using GenQAServer.Options;

namespace Models.Llm;

public class ChatProviderModel
{
    public required ProviderConfig provider { get; set; }
    public required LlmModelConfig modelConfig { get; set; }
}