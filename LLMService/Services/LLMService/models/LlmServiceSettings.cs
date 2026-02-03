
namespace LLMService.Services.LLMService.models
{
    public class LlmServiceSettings
    {
        public Dictionary<LlmProvider, LlmClientSettings> Providers { get; set; }
    }
}