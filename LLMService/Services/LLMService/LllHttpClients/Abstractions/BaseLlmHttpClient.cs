using System.Text.Json;

namespace LLMService.Services.LLMService.LllHttpClients.Abstractions
{
    public abstract class BaseLlmHttpClient : ILlmHttpClient
    {
        protected readonly HttpClient HttpClient;
        protected readonly ILogger Logger;

        protected BaseLlmHttpClient(HttpClient httpClient, ILogger logger)
        {
            HttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string> SendToLlm(string prompt, string model)
        {
            ValidateInput(prompt, model);

            HttpResponseMessage? response = null;
            try
            {
                Logger.LogDebug("Sending request to {Provider} with model {Model}", GetProviderName(), model);

                response = await SendRequestAsync(prompt, model);

                if (!response.IsSuccessStatusCode)
                {
                    await HandleErrorResponseAsync(response);
                }

                var text = await ParseResponseAsync(response);
                
                if (string.IsNullOrWhiteSpace(text))
                {
                    Logger.LogWarning("{Provider} returned empty text", GetProviderName());
                    return "Empty result";
                }

                Logger.LogDebug("Successfully received response from {Provider}", GetProviderName());
                return text;
            }
            catch (JsonException ex)
            {
                await LogJsonParsingErrorAsync(response, ex);
                throw new InvalidOperationException($"Failed to parse {GetProviderName()} response", ex);
            }
        }

        private async Task LogJsonParsingErrorAsync(HttpResponseMessage? response, JsonException ex)
        {
            if (response == null)
            {
                Logger.LogError(ex, "Failed to parse {Provider} response - response was null", GetProviderName());
                return;
            }

            try
            {
                var rawJson = await response.Content.ReadAsStringAsync();
                
                Logger.LogError(ex, 
                    "Failed to parse {Provider} response. Status: {StatusCode}, Raw JSON: {RawJson}", 
                    GetProviderName(), 
                    response.StatusCode,
                    rawJson);
            }
            catch (Exception readEx)
            {
                Logger.LogError(ex, 
                    "Failed to parse {Provider} response and couldn't read response content: {ReadError}", 
                    GetProviderName(), 
                    readEx.Message);
            }
        }

        protected virtual void ValidateInput(string prompt, string model)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                throw new ArgumentException("Prompt cannot be null or empty", nameof(prompt));
            }

            if (string.IsNullOrWhiteSpace(model))
            {
                throw new ArgumentException("Model cannot be null or empty", nameof(model));
            }
        }

        protected async Task HandleErrorResponseAsync(HttpResponseMessage response)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Logger.LogError("{Provider} returned error status {StatusCode}: {Error}", 
                GetProviderName(), response.StatusCode, errorContent);
            
            throw new HttpRequestException(
                $"{GetProviderName()} request failed with status {response.StatusCode}: {errorContent}");
        }

        /// <summary>
        /// Sends the HTTP request to the LLM provider
        /// </summary>
        protected abstract Task<HttpResponseMessage> SendRequestAsync(string prompt, string model);

        /// <summary>
        /// Parses the response and extracts the text content
        /// </summary>
        protected abstract Task<string> ParseResponseAsync(HttpResponseMessage response);

        /// <summary>
        /// Returns the provider name for logging purposes
        /// </summary>
        protected abstract string GetProviderName();
    }
}