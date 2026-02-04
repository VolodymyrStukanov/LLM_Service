using System.Text.Json;
using LLMService.Services.LLMService.Interfaces;

namespace LLMService.Services.LLMService.LllHttpClients
{
    public class MistralHttpClient : ILlmHttpClient
    {
        private readonly HttpClient httpClient;
        private readonly int maxTokens = 1000;
        public MistralHttpClient(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }
        public async Task<string> SendToLlm(string prompt, string model)
        {
            var response = await this.httpClient.PostAsJsonAsync("", new
            {
                max_tokens = this.maxTokens,
                model = model,
                messages = new[] { new { role = "user", content = prompt } }
            });

            if (response.IsSuccessStatusCode)
            {
                var resultJson = await response.Content.ReadFromJsonAsync<JsonElement>();
                var text = resultJson
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
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