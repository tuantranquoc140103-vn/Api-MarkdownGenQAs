

public interface ILlmServiceFactory
{
    LlmChatCompletionBase GetLlmServiceGenQAs();
    LlmChatCompletionBase GetLlmServiceChoice();
}