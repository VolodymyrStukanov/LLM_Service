using System.Text.Json;
using LLMService.Services.LLMService.LllHttpClients.Abstractions;

namespace LLMService.Services.LLMService.LllHttpClients
{
    public class ClaudeHttpClient : BaseLlmHttpClient
    {
        private readonly int _maxTokens = 1000;

        public ClaudeHttpClient(HttpClient httpClient, ILogger<ClaudeHttpClient> logger)
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
            
            if (!resultJson.TryGetProperty("content", out var content))
                throw new InvalidOperationException("Response missing 'content' property");

            if (content.GetArrayLength() == 0)
                throw new InvalidOperationException("Response content array is empty");

            if (!content[0].TryGetProperty("text", out var textElement))
                throw new InvalidOperationException("Response missing 'text' property");

            return textElement.GetString() ?? string.Empty;
        }

        protected override string GetProviderName() => "Claude";
    }
}