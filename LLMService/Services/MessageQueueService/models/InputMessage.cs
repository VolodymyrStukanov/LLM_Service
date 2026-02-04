namespace LLMService.Services.MessageQueueService.models
{
    public record InputMessage (string ReplyTo, string Prompt, string ModelProvider);
}