using System.Collections.Concurrent;
using LLMService.Services.LLMService.HttpClientFactory;
using LLMService.Services.LLMService.LllHttpClients.Abstractions;
using LLMService.Services.LLMService.models;

namespace LLMService.Services.LLMService
{
    public class LlmService
    {
        private readonly LlmHttpClientFactoryAbstract _clientFactory;
        private readonly ILogger<LlmService> _logger;
        private readonly ConcurrentDictionary<LlmProvider, ILlmHttpClient> _providers = new();

        public LlmService(LlmHttpClientFactoryAbstract llmHttpClientFactory, ILogger<LlmService> logger)
        {
            this._clientFactory = llmHttpClientFactory;
            _logger = logger;
        }

        public async Task<string> Send(LlmProvider provider, string model, string prompt)
        {
            var client = _providers.GetOrAdd(provider, p =>
            {
                _logger.LogDebug("Creating new HTTP client for provider {Provider}", p);
                return _clientFactory.CreateClient(p);
            });
            var response = await client.SendToLlm(prompt, model);
            return response;
        }

        public void ClearCache()
        {
            _logger.LogInformation("Clearing provider cache");
            _providers.Clear();
        }
    }
}