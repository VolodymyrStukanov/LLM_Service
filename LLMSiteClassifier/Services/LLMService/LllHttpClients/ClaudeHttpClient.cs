using System.Text.Json;
using LLMSiteClassifier.Services.LLMService.Interfaces;

namespace LLMSiteClassifier.Services.LLMService.LllHttpClients
{
    public class ClaudeHttpClient : ILlmHttpClient
    {
        private readonly HttpClient httpClient;
        private readonly string location = "";
        private readonly string model = "claude-3-5-haiku-20241022";
        private readonly int maxTokens = 1000;
        public ClaudeHttpClient(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }
        public async Task<string> SendToLlm(string prompt)
        {
            var response = await this.httpClient.PostAsJsonAsync(location, new
            {
                max_tokens = this.maxTokens,
                model = this.model,
                messages = new[] { new { role = "user", content = prompt } }
            });

            if (response.IsSuccessStatusCode)
            {
                var resultJson = await response.Content.ReadFromJsonAsync<JsonElement>();
                var text = resultJson
                    .GetProperty("content")[0]
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