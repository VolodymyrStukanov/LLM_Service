using LLMService.Services.LLMService.LllHttpClients;
using LLMService.Services.LLMService.LllHttpClients.Abstractions;
using LLMService.Services.LLMService.models;

namespace LLMService.Services.LLMService.HttpClientFactory
{
    public class LlmHttpClientFactory : LlmHttpClientFactoryAbstract
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<LlmHttpClientFactory> _logger;
        private readonly ILoggerFactory _loggerFactory;

        public LlmHttpClientFactory(
            IHttpClientFactory httpClientFactory,
            ILogger<LlmHttpClientFactory> logger,
            ILoggerFactory loggerFactory)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        public override ILlmHttpClient CreateClient(LlmProvider provider)
        {
            var clientName = GetClientName(provider);
            
            try
            {
                var httpClient = _httpClientFactory.CreateClient(clientName);
                
                _logger.LogInformation("Creating LLM client for provider {Provider}", provider);

                return provider switch
                {
                    LlmProvider.Gemini => new GeminiHttpClient(
                        httpClient, 
                        _loggerFactory.CreateLogger<GeminiHttpClient>()),
                    
                    LlmProvider.Grok => new GrokHttpClient(
                        httpClient, 
                        _loggerFactory.CreateLogger<GrokHttpClient>()),
                    
                    LlmProvider.Claude => new ClaudeHttpClient(
                        httpClient, 
                        _loggerFactory.CreateLogger<ClaudeHttpClient>()),
                    
                    LlmProvider.Mistral => new MistralHttpClient(
                        httpClient, 
                        _loggerFactory.CreateLogger<MistralHttpClient>()),
                    
                    LlmProvider.OpenAI => new OpenAiHttpClient(
                        httpClient, 
                        _loggerFactory.CreateLogger<OpenAiHttpClient>()),
                    
                    _ => throw new NotSupportedException(
                        $"Provider {provider} is not supported. Supported providers are: {string.Join(", ", Enum.GetNames<LlmProvider>())}")
                };
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Failed to create HttpClient for provider {Provider}. " +
                    "Ensure the provider is properly configured in appsettings.json", provider);
                throw new NotSupportedException(
                    $"Provider {provider} is not properly configured. Check your appsettings.json", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating client for provider {Provider}", provider);
                throw;
            }
        }
    }
}