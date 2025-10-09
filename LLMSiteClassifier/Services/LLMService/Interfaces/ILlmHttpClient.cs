namespace LLMSiteClassifier.Services.LLMService.Interfaces
{
    public interface ILlmHttpClient
    {
        public Task<string> SendToLlm(string prompt);
    }
}