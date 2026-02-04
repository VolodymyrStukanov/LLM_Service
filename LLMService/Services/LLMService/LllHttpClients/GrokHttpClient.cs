using System.Text.Json;
using LLMService.Services.LLMService.Interfaces;

namespace LLMService.Services.LLMService.LllHttpClients
{
    public class GrokHttpClient : ILlmHttpClient
    {
        private readonly HttpClient httpClient;
        public GrokHttpClient(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }
        public async Task<string> SendToLlm(string prompt, string model)
        {
            var response = await this.httpClient.PostAsJsonAsync("", new
            {
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