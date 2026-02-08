using System.Text.Json;
using LLMService.Services.LLMService.LllHttpClients.Abstractions;

namespace LLMService.Services.LLMService.LllHttpClients
{
    public class MistralHttpClient : BaseLlmHttpClient
    {
        private readonly int _maxTokens = 1000;

        public MistralHttpClient(HttpClient httpClient, ILogger<MistralHttpClient> logger)
            : base(httpClient, logger) { }

        protected override async Task<HttpResponseMessage> SendRequestAsync(string prompt, string model)
        {
            var requestBody = new
            {
                max_tokens = _maxTokens,
                model = model,
                messages = new[] { new { role = "user", content = prompt } }
            };

            return await HttpClient.PostAsJsonAsync("", requestBody);
        }

        protected override async Task<string> ParseResponseAsync(HttpResponseMessage response)
        {
            var resultJson = await response.Content.ReadFromJsonAsync<JsonElement>();
            
            if (!resultJson.TryGetProperty("choices", out var choices))
                throw new InvalidOperationException("Response missing 'choices' property");

            if (choices.GetArrayLength() == 0)
                throw new InvalidOperationException("Response choices array is empty");

            if (!choices[0].TryGetProperty("message", out var message))
                throw new InvalidOperationException("Response missing 'message' property");

            if (!message.TryGetProperty("content", out var contentElement))
                throw new InvalidOperationException("Response missing 'content' property");

            return contentElement.GetString() ?? string.Empty;
        }

        protected override string GetProviderName() => "Mistral";
    }
}