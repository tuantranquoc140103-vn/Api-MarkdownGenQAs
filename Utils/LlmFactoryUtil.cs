public static class LlmFactoryUtil
{
    public static (string, string) ParseProviderModel(string providerModel)
    {
        string[] parse = providerModel.Split("__");
        string provierName = parse[0];
        string model = parse[1];
        return (provierName, model);
    }


    public static LlmModelConfig GetModel(ProviderConfig provider, string modelName)
    {
        LlmModelConfig? modelConfig = provider.Models.FirstOrDefault(x => x.ModelName == modelName);

        if (modelConfig is null)
        {
            throw new ArgumentException($"Model:{modelName} is missing in appsettings.json");
        }
        return modelConfig;
    }
}