namespace LLMService.Services.MessageQueueService.models
{
    public record OutputMessage (string CorrelationId, string ReplyTo, byte[] ResponseBody);
}