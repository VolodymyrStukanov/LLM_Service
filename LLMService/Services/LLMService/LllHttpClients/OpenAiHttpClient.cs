using System.Text.Json;
using LLMService.Services.LLMService.Interfaces;

namespace LLMService.Services.LLMService.LllHttpClients
{
    public class OpenAiHttpClient : ILlmHttpClient
    {
        private readonly HttpClient httpClient;
        private readonly string location = "";
        private readonly string model = "gpt-4.1-mini";
        private readonly int maxTokens = 1000;
        public OpenAiHttpClient(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }
        public async Task<string> SendToLlm(string prompt)
        {
            var response = await this.httpClient.PostAsJsonAsync(location, new
            {
                model = this.model,
                input = prompt
            });

            if (response.IsSuccessStatusCode)
            {
                var resultJson = await response.Content.ReadFromJsonAsync<JsonElement>();
                var text = resultJson
                    .GetProperty("output")[0]
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