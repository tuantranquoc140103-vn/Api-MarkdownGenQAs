
using OpenAI.Chat;

public interface ILlmClientFactory
{
    (ChatClient, ChatProviderModel) GetChatLlmProviderModelChoice();
    (ChatClient, ChatProviderModel) GetChatLlmProviderModelGenQAs();
}