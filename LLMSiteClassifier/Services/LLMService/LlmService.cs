using System.Text.RegularExpressions;
using LLMSiteClassifier.Services.LLMService.HttpClientFactory;
using LLMSiteClassifier.Services.LLMService.models;

namespace LLMSiteClassifier.Services.LLMService
{
    public class LlmService
    {
        private readonly LlmHttpClientFactoryAbstract clientFactory;
        private readonly List<string> categories = ["military", "midicine", "automobiles", "agronomy", "education", "industry", "other"];
        private readonly string responseFormat = @"
        <output>
        {
            ""Categories"": 
            [
                ""military"",
                ""automobiles""
            ]
        }
        </output>";

        public LlmService(LlmHttpClientFactoryAbstract llmHttpClientFactory)
        {
            this.clientFactory = llmHttpClientFactory;
        }

        public async Task<string> GetCompletionAsync(LlmProvider provider, string content)
        {
            var promptTemplate = $"Categorize the company as a potential lead in the following categories by content from its site or its page from a company catalog site."
            +$"\nThe categories: {string.Join(", ", this.categories)}"
            +$"\nThe answer must be in the schema: ${this.responseFormat}"
            +$"\nThe highest possible category must be the first"
            +$"\nThe content: ${content}";
            var client = clientFactory.CreateClient(provider);
            var response = await client.SendToLlm(promptTemplate);
            string[] resultCategories = [.. Regex.Matches(response, @"""Categories""[^[]*\[([^\]]*)\]", RegexOptions.Singleline)
                .SelectMany(m => Regex.Matches(m.Groups[1].Value, @"""([^""]+)"""))
                .Select(m => m.Groups[1].Value)];
            return resultCategories[0];
        }
    }
}