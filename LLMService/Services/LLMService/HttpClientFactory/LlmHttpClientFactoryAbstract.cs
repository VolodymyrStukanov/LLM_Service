using LLMService.Services.LLMService.Interfaces;
using LLMService.Services.LLMService.models;

namespace LLMService.Services.LLMService.HttpClientFactory
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