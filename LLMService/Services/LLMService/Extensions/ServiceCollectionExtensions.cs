using LLMService.Services.LLMService.Extensions.models;
using LLMService.Services.LLMService.HttpClientFactory;
using LLMService.Services.LLMService.models;
using Polly;

namespace LLMService.Services.LLMService.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddLlmHttpClients(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<LlmServiceSettings>(configuration.GetSection("LlmServiceSettings"));

            var settings = configuration.GetSection("LlmServiceSettings").Get<LlmServiceSettings>();

            // Register HttpClients
            foreach (var provider in Enum.GetValues<LlmProvider>())
            {
                if (settings?.Providers?.ContainsKey(provider) == true)
                {
                    var config = settings.Providers[provider];
                    var clientName = LlmHttpClientFactoryAbstract.GetClientName(provider);

                    services.AddHttpClient(clientName)
                        .ConfigureHttpClient(client =>
                        {
                            client.BaseAddress = new Uri(config.BaseUrl);
                            client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
                            foreach(var header in config.Headers)
                            {
                                client.DefaultRequestHeaders.Add(header.Key, header.Value);
                            }
                            client.DefaultRequestHeaders.Add("User-Agent", "LlmService/1.0");
                        })
                        .AddTransientHttpErrorPolicy(policyBuilder =>
                            policyBuilder.WaitAndRetryAsync(
                                config.MaxRetries,
                                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                            ))
                        .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(config.TimeoutSeconds)));
                }
            }

            services.AddSingleton<LlmHttpClientFactoryAbstract, LlmHttpClientFactory>();
            return services;
        }
    }
}