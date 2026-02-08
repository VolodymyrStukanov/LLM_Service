namespace LLMService.Services.LLMService.Extensions.models
{
    public record LlmClientSettings(string BaseUrl, Dictionary<string, string> Headers, int TimeoutSeconds, int MaxRetries);
}