using System.Text.Json;
using LLMSiteClassifier.Services.LLMService.Interfaces;

namespace LLMSiteClassifier.Services.LLMService.LllHttpClients
{
    public class GeminiHttpClient : ILlmHttpClient
    {
        private readonly HttpClient httpClient;
        private readonly string location = "models/gemini-2.5-flash:generateContent";
        public GeminiHttpClient(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }
        public async Task<string> SendToLlm(string prompt)
        {
            var response = await this.httpClient.PostAsJsonAsync(location, new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } }
            });

            if (response.IsSuccessStatusCode)
            {
                var resultJson = await response.Content.ReadFromJsonAsync<JsonElement>();
                var text = resultJson
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .ToString();
                return text ?? "Empty result";
            }
            else
            {
                return "Error";
            }
        }

    }
}