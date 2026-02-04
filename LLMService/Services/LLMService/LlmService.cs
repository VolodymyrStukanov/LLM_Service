using System.Collections.Concurrent;
using LLMService.Services.LLMService.HttpClientFactory;
using LLMService.Services.LLMService.Interfaces;
using LLMService.Services.LLMService.models;

namespace LLMService.Services.LLMService
{
    public class LlmService
    {
        private readonly LlmHttpClientFactoryAbstract clientFactory;
        private readonly ConcurrentDictionary<int, ILlmHttpClient> providers = new();

        private readonly Lock _lockObj = new();

        public LlmService(LlmHttpClientFactoryAbstract llmHttpClientFactory)
        {
            this.clientFactory = llmHttpClientFactory;
        }

        public async Task<string> GetCompletionAsync(LlmProvider provider, string model, string prompt)
        {
            lock (_lockObj)
            {
                if(!providers.TryGetValue((int)provider, out var _))
                    providers.TryAdd((int)provider, clientFactory.CreateClient(provider));                
                else providers[(int)provider] ??= clientFactory.CreateClient(provider);
            }
            
            var client = providers[(int)provider];
            var response = await client.SendToLlm(prompt, model);
            return response;
            // string jsonContent = Regex.Match(response, @"```json\s*([\s\S]*?)\s*```").Groups[1].Value;
            // return jsonContent;
        }
    }
}