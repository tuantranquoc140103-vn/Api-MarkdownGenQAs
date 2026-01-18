
using GenQAServer.Options;
using MarkdownGenQAs.Interfaces.Factory;
using MarkdownGenQAs.Models.Enum;
using Microsoft.Extensions.Options;
using Models.Llm;
using OpenAI.Chat;
using Utils;

namespace GenQAServer.Infrastructure.Factories;

public class LlmClientFactory : ILlmClientFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ChunkOption _chunkOption;
    private readonly LlmProviderOptions _llmProviderOptions;
    public LlmClientFactory(IOptions<ChunkOption> options, IOptions<LlmProviderOptions> llmProviderOptions, IServiceProvider serviceProvider)
    {
        _chunkOption = options.Value ?? throw new ArgumentNullException("ChunkOption is missing in appsettings.json");
        _llmProviderOptions = llmProviderOptions.Value ?? throw new ArgumentNullException("LlmProviderOptions is missing in appsettings.json");
        _serviceProvider = serviceProvider;
    }
    public (ChatClient, ChatProviderModel) GetChatLlmProviderModelChoice()
    {
        (string provider, string model) = LlmFactoryUtil.ParseProviderModel(_chunkOption.UseModelProviderForChoice);

        if(Enum.TryParse(provider, out LlmProvider providerModel))
        {
            var chatClient = _serviceProvider.GetRequiredKeyedService<ChatClient>(providerModel);
            var providerConfig = GetProvider(providerModel);
            var modelConfig = LlmFactoryUtil.GetModel(providerConfig, model);

            return (chatClient, new ChatProviderModel{provider = providerConfig, modelConfig = modelConfig});
            
        }
        throw new NotImplementedException();
    }

    public (ChatClient, ChatProviderModel) GetChatLlmProviderModelGenQAs()
    {
        (string provider, string model) = LlmFactoryUtil.ParseProviderModel(_chunkOption.UseModelProviderForGenQAs);

        if(Enum.TryParse(provider, out LlmProvider providerModel))
        {
            var chatClient = _serviceProvider.GetRequiredKeyedService<ChatClient>(providerModel);
            var providerConfig = GetProvider(providerModel);
            var modelConfig = LlmFactoryUtil.GetModel(providerConfig, model);

            return (chatClient, new ChatProviderModel{provider = providerConfig, modelConfig = modelConfig});
            
        }
        throw new NotImplementedException();
    }

    public ProviderConfig GetProvider(LlmProvider provider)
    {
        switch (provider)
        {
            case LlmProvider.Nvidia:
                return _llmProviderOptions.Nvidia;
            case LlmProvider.Vllm:
                return _llmProviderOptions.Vllm;
            default:
                throw new ArgumentException($"LlmProvider:{provider} is missing in appsettings.json");
        }
    }

}