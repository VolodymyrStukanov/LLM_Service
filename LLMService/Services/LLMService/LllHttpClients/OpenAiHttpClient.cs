using System.Text.Json;
using LLMService.Services.LLMService.LllHttpClients.Abstractions;
using LLMService.Services.LLMService.models;

namespace LLMService.Services.LLMService.LllHttpClients
{
    public class OpenAiHttpClient : BaseLlmHttpClient
    {
        private readonly int _maxTokens = 1000;

        public OpenAiHttpClient(HttpClient httpClient, ILogger<OpenAiHttpClient> logger)
            : base(httpClient, logger) { }

        protected override async Task<HttpResponseMessage> SendRequestAsync(string prompt, string model)
        {
            var requestBody = new
            {
                model = model,
                input = new[] { new { role = "user", content = prompt } },
                max_output_tokens = _maxTokens
            };

            return await HttpClient.PostAsJsonAsync("", requestBody);
        }

        protected override async Task<HttpResponseMessage> SendRequestWithAttachmentsAsync(string prompt, string model, List<FileAttachment> attachments)
        {
            object[] AttachmentsBuilder(List<FileAttachment> files)
            {
                var attachments = new List<object>();
                foreach (var file in files)
                {
                    var item = new
                    {
                        type = "input_file",
                        filename = file.FileName,
                        file_data = $"data:{file.ContentType};base64,{file.GetBase64Content()}"
                    };
                    attachments.Add(item);
                }
                return attachments.ToArray();
            }

            object[] content = [
                new {
                    type = "input_text",
                    text = prompt
                },
                .. AttachmentsBuilder(attachments)
            ];
            var requestBody = new
            {
                model = model,
                input = new[] 
                { 
                    new { 
                        role = "user", 
                        content = content
                    } 
                },
                max_output_tokens = _maxTokens
            };

            return await HttpClient.PostAsJsonAsync("", requestBody);
        }

        protected override async Task<string> ParseResponseAsync(HttpResponseMessage response)
        {
            var resultJson = await response.Content.ReadFromJsonAsync<JsonElement>();
            
            if (!resultJson.TryGetProperty("output", out var output))
                throw new InvalidOperationException("Response missing 'output' property");

            if (output.GetArrayLength() == 0)
                throw new InvalidOperationException("Response output array is empty");

            if (!output[0].TryGetProperty("content", out var content))
                throw new InvalidOperationException("Response missing 'content' property");

            if (content.GetArrayLength() == 0)
                throw new InvalidOperationException("Response missing 'content' property");

            if (!content[0].TryGetProperty("text", out var text))
                throw new InvalidOperationException("Response missing 'text' property");

            return text.GetString() ?? string.Empty;
        }

        protected override string GetProviderName() => "OpenAI";
    }
}