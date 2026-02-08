using System.Text.Json;
using LLMService.Services.LLMService.LllHttpClients.Abstractions;

namespace LLMService.Services.LLMService.LllHttpClients
{
    public class GeminiHttpClient : BaseLlmHttpClient
    {
        public GeminiHttpClient(HttpClient httpClient, ILogger<GeminiHttpClient> logger)
            : base(httpClient, logger) { }

        protected override async Task<HttpResponseMessage> SendRequestAsync(string prompt, string model)
        {
            var requestBody = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } }
            };

            return await HttpClient.PostAsJsonAsync($"models/{model}:generateContent", requestBody);
        }

        protected override async Task<string> ParseResponseAsync(HttpResponseMessage response)
        {
            var resultJson = await response.Content.ReadFromJsonAsync<JsonElement>();
            
            if (!resultJson.TryGetProperty("candidates", out var candidates))
                throw new InvalidOperationException("Response missing 'candidates' property");

            if (candidates.GetArrayLength() == 0)
                throw new InvalidOperationException("Response candidates array is empty");

            if (!candidates[0].TryGetProperty("content", out var content))
                throw new InvalidOperationException("Response missing 'content' property");

            if (!content.TryGetProperty("parts", out var parts))
                throw new InvalidOperationException("Response missing 'parts' property");

            if (parts.GetArrayLength() == 0)
                throw new InvalidOperationException("Response parts array is empty");

            if (!parts[0].TryGetProperty("text", out var textElement))
                throw new InvalidOperationException("Response missing 'text' property");

            return textElement.GetString() ?? string.Empty;
        }

        protected override string GetProviderName() => "Gemini";
    }
}