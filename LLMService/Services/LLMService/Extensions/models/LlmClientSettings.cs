
namespace LLMService.Services.LLMService.Extensions.models
{
    public class LlmClientSettings
    {        
        public string BaseUrl { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public int TimeoutSeconds { get; set; } = 30;
        public int MaxRetries { get; set; } = 3;
    }
}