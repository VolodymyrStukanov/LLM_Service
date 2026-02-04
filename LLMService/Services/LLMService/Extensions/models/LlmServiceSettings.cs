
using LLMService.Services.LLMService.models;

namespace LLMService.Services.LLMService.Extensions.models
{
    public class LlmServiceSettings
    {
        public Dictionary<LlmProvider, LlmClientSettings> Providers { get; set; }
    }
}