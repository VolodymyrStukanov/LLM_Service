using LLMSiteClassifier.Services.LLMService.Interfaces;
using LLMSiteClassifier.Services.LLMService.models;

namespace LLMSiteClassifier.Services.LLMService.HttpClientFactory
{
    public abstract class LlmHttpClientFactoryAbstract
    {
        public abstract ILlmHttpClient CreateClient(LlmProvider provider);
        public static string GetClientName(LlmProvider provider)
        {
            return $"LlmClient_{provider}";
        }
    }
}