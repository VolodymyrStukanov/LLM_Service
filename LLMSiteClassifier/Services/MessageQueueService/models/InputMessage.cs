namespace LLMSiteClassifier.Services.MessageQueueService.models
{
    public class InputMessage
    {
        public string ReplyTo { get; set; }
        public string Prompt { get; set; }
        public string ModelProvider { get; set; }
    }
}