using LLMSiteClassifier.Services.LLMService.HttpClientFactory;
using LLMSiteClassifier.Services.LLMService.models;

namespace LLMSiteClassifier.Services.LLMService
{
    public class LlmService
    {
        private readonly LlmHttpClientFactoryAbstract clientFactory;

        public LlmService(LlmHttpClientFactoryAbstract llmHttpClientFactory)
        {
            this.clientFactory = llmHttpClientFactory;
        }

        public async Task<string> GetCompletionAsync(LlmProvider provider, string prompt)
        {
            var client = clientFactory.CreateClient(provider);
            var response = await client.SendToLlm(prompt);
            return response;
        }
    }
}