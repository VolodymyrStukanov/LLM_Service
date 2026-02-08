namespace LLMService.Services.LLMService.LllHttpClients.Abstractions
{
    public interface ILlmHttpClient
    {
        public Task<string> SendToLlm(string prompt, string model);
    }
}