using System.Text;
using System.Text.Json;
using LLMSiteClassifier.Services.LLMService;
using LLMSiteClassifier.Services.LLMService.models;
using LLMSiteClassifier.Services.MessageQueueService.models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace LLMSiteClassifier.Sevices.MessageQueueService
{
    public class MessageQueue : BackgroundService
    {
        private IChannel channel;
        private readonly string inputQueueName;
        private readonly ConnectionFactory connectionFactory;
        private readonly LlmService llmService;
        private readonly ILogger<MessageQueue> logger;

        private readonly SemaphoreSlim channelSemaphore = new SemaphoreSlim(1, 1);
        public MessageQueue(IConfiguration config,  ILogger<MessageQueue> logger, LlmService llmService)
        {
            var hostName = config["RabbitMQ:HostName"]!;
            this.inputQueueName = config["RabbitMQ:QueueName"]!;
            this.connectionFactory = new ConnectionFactory() { HostName = hostName };
            this.logger = logger;
            this.llmService = llmService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var connection = await this.connectionFactory.CreateConnectionAsync();
            this.channel = await connection.CreateChannelAsync();
            await CreateQueues(this.channel);
            StartConsumingMessages();
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task CreateQueues(IChannel channel)
        {
            await channel.QueueDeclareAsync(queue: this.inputQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        }

        private async Task StartConsumingMessages()
        {
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                string? correlationId = ea.BasicProperties.CorrelationId;
                try
                {
                    if (!string.IsNullOrEmpty(correlationId))
                    {
                        this.logger.LogInformation($"Start processing message. Correlation id: {correlationId}");

                        var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);
                        var inputMessage = JsonSerializer.Deserialize<InputMessage>(message, new JsonSerializerOptions
                        {
                            AllowTrailingCommas = true
                        })!;

                        this.logger.LogInformation($"Finished processing message. Correlation id: {correlationId}");

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var response = await this.llmService.GetCompletionAsync(LlmProvider.Gemini, inputMessage.Prompt);
                                await PublishMessage(correlationId, inputMessage.ReplyTo, response);

                                await LockedActionWithChannelSemaphore(
                                    async () => await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false));

                                this.logger.LogInformation($"Finished sending response back. Correlation id: {correlationId}");
                            }
                            catch (Exception ex)
                            {
                                this.logger.LogError(exception: ex, $"Error during processing message. Correlation id: {correlationId}.\nMessage: {ex.Message}");
                                await LockedActionWithChannelSemaphore(
                                    async () => await channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true));
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    this.logger.LogError(exception: ex, $"Error during processing message. Correlation id: {correlationId}.\nMessage: {ex.Message}");
                    await channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                }
            };

            await this.channel.BasicConsumeAsync(this.inputQueueName, autoAck: false, consumer: consumer);
        }

        private async Task LockedActionWithChannelSemaphore(Func<Task> action) 
        {
            await channelSemaphore.WaitAsync();
            try
            {
                await action.Invoke();
            }
            finally
            {
                channelSemaphore.Release();
            }
        }

        public async Task PublishMessage(string correlationId, string replyTo, string response)
        {
            await this.channel.QueueDeclareAsync(queue: replyTo,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("LlmResponse");
                writer.WriteRawValue(response);
                writer.WriteEndObject();
            }

            var bodyBytes = stream.ToArray();

            await this.channel.BasicPublishAsync(exchange: "",
                routingKey: replyTo,
                mandatory: true,
                basicProperties: new BasicProperties { CorrelationId = correlationId },
                body: bodyBytes);
        }
    }
}