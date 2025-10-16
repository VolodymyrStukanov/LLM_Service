using LLMSiteClassifier.Services.LLMService.Interfaces;
using LLMSiteClassifier.Services.LLMService.LllHttpClients;
using LLMSiteClassifier.Services.LLMService.models;

namespace LLMSiteClassifier.Services.LLMService.HttpClientFactory
{
    public class LlmHttpClientFactory : LlmHttpClientFactoryAbstract
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public LlmHttpClientFactory(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public override ILlmHttpClient CreateClient(LlmProvider provider)
        {
            var clientName = GetClientName(provider);
            var client = _httpClientFactory.CreateClient(clientName);
            
            return provider switch
            {
                LlmProvider.Gemini => new GeminiHttpClient(client),
                LlmProvider.Grok => new GrokHttpClient(client),
                LlmProvider.Claude => new ClaudeHttpClient(client),
                LlmProvider.Mistral => new MistralHttpClient(client),
                LlmProvider.OpenAI => new OpenAiHttpClient(client),
                _ => throw new NotSupportedException($"Provider {provider} not supported")
            };
        }
    }
}