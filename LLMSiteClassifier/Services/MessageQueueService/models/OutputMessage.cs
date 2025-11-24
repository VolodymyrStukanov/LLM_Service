namespace LLMSiteClassifier.Services.MessageQueueService.models
{
    public class OutputMessage
    {
        private string correlationId;
        public string CorrelationId => this.correlationId;
        private byte[] responseBody;
        public byte[] ResponseBody => this.responseBody;
        private string replyTo;
        public string ReplyTo => this.replyTo;
        
        public OutputMessage(string correlationId, string replyTo, byte[] response)
        {
            this.replyTo = replyTo;
            this.correlationId = correlationId;
            this.responseBody = response;
        }
    }
}