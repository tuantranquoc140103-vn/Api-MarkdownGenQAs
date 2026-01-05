using Microsoft.Extensions.Options;

public class LlmServiceFactory : ILlmServiceFactory
{
    private readonly ChunkOption _chunkOption;
    private readonly IServiceProvider _serviceProvider;
    public LlmServiceFactory(IOptions<ChunkOption> options, IServiceProvider serviceProvider)
    {
        _chunkOption = options.Value ?? throw new ArgumentNullException("ChunkOption is missing in appsettings.json");
        _serviceProvider = serviceProvider;
    }
    public LlmChatCompletionBase GetLlmServiceChoice()
    {
        (string providerName, string _) = LlmFactoryUtil.ParseProviderModel(_chunkOption.UseModelProviderForChoice);

        if(Enum.TryParse(providerName, out LlmProvider providerModel))
        {
            return _serviceProvider.GetRequiredKeyedService<LlmChatCompletionBase>(providerModel);
        }
        throw new InvalidOperationException($"Invalid get LlmChatCompletionBase provider name: {providerName}");
    }

    public LlmChatCompletionBase GetLlmServiceGenQAs()
    {
        (string providerName, string _) = LlmFactoryUtil.ParseProviderModel(_chunkOption.UseModelProviderForGenQAs);

        if(Enum.TryParse(providerName, out LlmProvider providerModel))
        {
            return _serviceProvider.GetRequiredKeyedService<LlmChatCompletionBase>(providerModel);
        }
        throw new InvalidOperationException($"Invalid get LlmChatCompletionBase provider name: {providerName}");
    }
}