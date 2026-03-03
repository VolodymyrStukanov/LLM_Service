using LLMService.Services.LLMService.models;

namespace LLMService.Services.LLMService.LllHttpClients.Abstractions
{
    public interface ILlmHttpClient
    {
        public Task<string> SendToLlm(string prompt, string model, List<FileAttachment>? attachments);
    }
}