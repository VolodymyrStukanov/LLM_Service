using LLMService.Services.LLMService.Extensions.models;
using LLMService.Services.LLMService.HttpClientFactory;
using LLMService.Services.LLMService.models;
using Polly;

namespace LLMService.Services.LLMService.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddLlmHttpClients(
            this IServiceCollection services, 
            IConfiguration configuration)
        {
            services.Configure<LlmServiceSettings>(configuration.GetSection("LlmServiceSettings"));

            var settings = configuration.GetSection("LlmServiceSettings").Get<LlmServiceSettings>();

            if (settings?.Providers == null || settings.Providers.Count == 0)
            {
                throw new InvalidOperationException(
                    "LlmServiceSettings configuration is missing or contains no providers. " +
                    "Please ensure appsettings.json contains valid LlmServiceSettings with at least one provider.");
            }

            // Register HttpClients for each configured provider
            foreach (var provider in Enum.GetValues<LlmProvider>())
            {
                if (settings.Providers.TryGetValue(provider, out var config))
                {
                    ValidateProviderConfig(provider, config);
                    RegisterHttpClient(services, provider, config);
                }
            }

            services.AddSingleton<LlmHttpClientFactoryAbstract, LlmHttpClientFactory>();
            
            return services;
        }

        private static void ValidateProviderConfig(LlmProvider provider, LlmClientSettings config)
        {
            if (config == null)
                throw new InvalidOperationException($"Configuration for provider {provider} is null");

            if (string.IsNullOrWhiteSpace(config.BaseUrl))
                throw new InvalidOperationException($"BaseUrl for provider {provider} is not configured");

            if (!Uri.TryCreate(config.BaseUrl, UriKind.Absolute, out _))
                throw new InvalidOperationException($"BaseUrl '{config.BaseUrl}' for provider {provider} is not a valid URL");

            if (config.TimeoutSeconds <= 0)
                throw new InvalidOperationException($"TimeoutSeconds for provider {provider} must be greater than 0");

            if (config.MaxRetries < 0)
                throw new InvalidOperationException($"MaxRetries for provider {provider} cannot be negative");

            if (config.Headers == null)
                throw new InvalidOperationException($"Headers configuration for provider {provider} is null");
        }

        private static void RegisterHttpClient(
            IServiceCollection services, 
            LlmProvider provider, 
            LlmClientSettings config)
        {
            var clientName = LlmHttpClientFactoryAbstract.GetClientName(provider);

            services.AddHttpClient(clientName)
                .ConfigureHttpClient(client =>
                {
                    client.BaseAddress = new Uri(config.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
                    
                    foreach (var header in config.Headers)
                    {
                        if (string.IsNullOrWhiteSpace(header.Key))
                        {
                            throw new InvalidOperationException(
                                $"Header key cannot be empty for provider {provider}");
                        }
                        client.DefaultRequestHeaders.Add(header.Key, header.Value);
                    }
                    
                    client.DefaultRequestHeaders.Add("User-Agent", "LlmService/1.0");
                })
                .AddTransientHttpErrorPolicy(policyBuilder =>
                    policyBuilder.WaitAndRetryAsync(
                        config.MaxRetries,
                        retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                        onRetry: (outcome, timespan, retryCount, context) =>
                        {
                            // Log retry attempts
                            Console.WriteLine(
                                $"Retry {retryCount} for {provider} after {timespan.TotalSeconds}s due to: {outcome.Exception?.Message}");
                        }
                    ))
                .AddPolicyHandler(
                    Policy.TimeoutAsync<HttpResponseMessage>(
                        TimeSpan.FromSeconds(config.TimeoutSeconds)));
        }
    }
}