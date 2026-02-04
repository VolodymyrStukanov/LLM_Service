using System.Text.Json;
using LLMService.Services.LLMService.Interfaces;

namespace LLMService.Services.LLMService.LllHttpClients
{
    public class GeminiHttpClient : ILlmHttpClient
    {
        private readonly HttpClient httpClient;
        public GeminiHttpClient(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }
        public async Task<string> SendToLlm(string prompt, string model)
        {
            var response = await this.httpClient.PostAsJsonAsync($"models/{model}", new
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