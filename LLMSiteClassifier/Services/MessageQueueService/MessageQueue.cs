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
        private readonly Dictionary<string, LlmProvider> llmProvidersDict = new()
        {
            {"Gemini", LlmProvider.Gemini },
            {"OpenAI", LlmProvider.OpenAI },
            {"Grok", LlmProvider.Grok },
            {"Claude", LlmProvider.Claude },
            {"Mistral", LlmProvider.Mistral },
        };

        private IChannel? channel;
        private IConnection? connection;
        private readonly string inputQueueName;
        private readonly ConnectionFactory connectionFactory;
        private readonly LlmService llmService;
        private readonly ILogger<MessageQueue> logger;

        private readonly SemaphoreSlim processingSlots;
        private readonly int maxConcurrentMessages;

        private TaskCompletionSource<bool> reconnectionTrigger = new();
        private int consecutiveFailuresCounter = 0;
        private const int MaxBackoffSeconds = 60;
        public MessageQueue(IConfiguration config,  ILogger<MessageQueue> logger, LlmService llmService)
        {
            var hostName = config["RabbitMQ:HostName"]!;
            this.inputQueueName = config["RabbitMQ:QueueName"]!;
            this.maxConcurrentMessages = config.GetValue<int>("RabbitMQ:MaxConcurrentMessages", 10);
            this.connectionFactory = new ConnectionFactory() 
            { 
                HostName = hostName,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                RequestedHeartbeat = TimeSpan.FromSeconds(60),
                RequestedConnectionTimeout = TimeSpan.FromSeconds(30),
                ContinuationTimeout = TimeSpan.FromSeconds(120)
            };
            this.logger = logger;
            this.llmService = llmService;
            this.processingSlots = new SemaphoreSlim(maxConcurrentMessages, maxConcurrentMessages);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ConnectAndConsume(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    this.logger.LogInformation("Consumer shutting down gracefully");
                    break;
                }
                catch(Exception ex)
                {
                    this.consecutiveFailuresCounter++;

                    var backoffSeconds = Math.Pow(2, this.consecutiveFailuresCounter - 1);
                    backoffSeconds = backoffSeconds > MaxBackoffSeconds ? MaxBackoffSeconds : backoffSeconds;
                    this.logger.LogError(ex, 
                        $"Consumer failed (consecutive failures: {consecutiveFailuresCounter}). " +
                        $"Reconnecting in {backoffSeconds}s");

                    await CleanupConnectionAsync();                
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(backoffSeconds), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private async Task ConnectAndConsume(CancellationToken stoppingToken)
        {
            this.reconnectionTrigger = new TaskCompletionSource<bool>();

            this.connection = await this.connectionFactory.CreateConnectionAsync();
            this.connection.ConnectionShutdownAsync += OnConnectionShutdown;
            this.connection.CallbackExceptionAsync += OnCallbackException;

            this.channel = await this.connection.CreateChannelAsync();
            await this.channel.BasicQosAsync(
                prefetchSize: 0, 
                prefetchCount: (ushort)maxConcurrentMessages, 
                global: false);
            
            await this.channel.QueueDeclareAsync(
                queue: this.inputQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            await StartConsumingMessages();
            this.consecutiveFailuresCounter = 0;

            var cancellationTask = Task.Delay(Timeout.Infinite, stoppingToken);
            var reconnectTask = reconnectionTrigger.Task;
            
            var completedTask = await Task.WhenAny(cancellationTask, reconnectTask);            
            if (completedTask == reconnectTask)
            {
                this.logger.LogWarning("Connection lost, triggering reconnection");
                throw new InvalidOperationException("Connection lost");
            }
        }

        private Task OnConnectionShutdown(object? sender, ShutdownEventArgs args)
        {
            if (!args.Initiator.Equals(ShutdownInitiator.Application))
            {
                this.logger.LogWarning(
                    $"Connection shutdown: Initiator={args.Initiator}, " +
                    $"Code={args.ReplyCode}, Text={args.ReplyText}");
                reconnectionTrigger.TrySetResult(true);
            }
            return Task.CompletedTask;
        }

        private Task OnCallbackException(object? sender, CallbackExceptionEventArgs args)
        {
            this.logger.LogError(args.Exception, "Connection callback exception");
            reconnectionTrigger.TrySetResult(true);
            return Task.CompletedTask;
        }

        private async Task CleanupConnectionAsync()
        {
            if (this.channel != null)
            {
                try { await this.channel.CloseAsync(); } catch { }
                try { await this.channel.DisposeAsync(); } catch { }
                this.channel = null;
            }
            
            if (this.connection != null)
            {
                try { await this.connection.CloseAsync(); } catch { }
                try { await this.connection.DisposeAsync(); } catch { }
                this.connection = null;
            }
        }

        private async Task StartConsumingMessages()
        {
            var consumer = new AsyncEventingBasicConsumer(channel!);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                string? correlationId = ea.BasicProperties.CorrelationId;

                await processingSlots.WaitAsync();
                try
                {
                    if (string.IsNullOrEmpty(correlationId))
                    {
                        this.logger.LogWarning("Received message without correlationId");
                        await channel!.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                        return;
                    }

                    this.logger.LogInformation($"Start processing message. Correlation id: {correlationId}");

                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var inputMessage = JsonSerializer.Deserialize<InputMessage>(message, new JsonSerializerOptions
                    {
                        AllowTrailingCommas = true
                    });

                    if (inputMessage == null)
                    {
                        this.logger.LogError($"Failed to deserialize message. Correlation id: {correlationId}");
                        await channel!.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                        return;
                    }

                    try
                    {
                        if (!llmProvidersDict.TryGetValue(inputMessage.ModelProvider, out var provider))
                        {
                            this.logger.LogError($"Unknown provider: {inputMessage.ModelProvider}. Correlation id: {correlationId}");
                            await channel!.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                            return;
                        }

                        var response = await GetCompletionWithRetry(provider, inputMessage.Prompt, correlationId);                        
                        await PublishMessage(correlationId, inputMessage.ReplyTo, response);                        
                        await channel!.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);                        
                        this.logger.LogInformation($"Successfully processed message. Correlation id: {correlationId}");
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError(ex, $"Error during processing message. Correlation id: {correlationId}");
                        var shouldRequeue = IsTransientError(ex);
                        await channel!.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: shouldRequeue);
                        
                        if (shouldRequeue)
                        {
                            this.logger.LogWarning($"Message requeued. Correlation id: {correlationId}");
                        }
                        else
                        {
                            this.logger.LogError($"Message rejected (not requeued). Correlation id: {correlationId}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.logger.LogError(exception: ex, $"Error during processing message. Correlation id: {correlationId}.\nMessage: {ex.Message}");
                    await channel!.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                }
                finally
                {
                    processingSlots.Release();
                }
            };

            await this.channel!.BasicConsumeAsync(this.inputQueueName, autoAck: false, consumer: consumer);
        }

        private async Task<string> GetCompletionWithRetry(LlmProvider provider, string prompt, string correlationId)
        {
            const int maxAttempts = 5;
            var attempt = 0;

            while (attempt < maxAttempts)
            {
                try
                {
                    attempt++;
                    return await this.llmService.GetCompletionAsync(provider, prompt);
                }
                catch (Exception ex) when (attempt < maxAttempts && IsTransientError(ex))
                {
                    this.logger.LogWarning(ex, 
                        $"LLM call failed (attempt {attempt}/{maxAttempts}). Correlation id: {correlationId}");
                    
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                }
            }
            throw new InvalidOperationException($"Failed to get LLM completion after {maxAttempts} attempts");
        }

        private bool IsTransientError(Exception ex)
        {
            return ex is HttpRequestException ||
                ex is TaskCanceledException ||
                ex is TimeoutException ||
                ex is ArgumentException ||
                ex is JsonException ||
                (ex.Message?.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ?? false) ||
                (ex.Message?.Contains("timeout", StringComparison.OrdinalIgnoreCase) ?? false);
        }

        public async Task PublishMessage(string correlationId, string replyTo, string response)
        {
            var responseObject = new
            {
                LlmResponse = JsonDocument.Parse(response, new JsonDocumentOptions
                {
                    AllowTrailingCommas = true
                }).RootElement
            };
            
            var bodyJson = JsonSerializer.Serialize(responseObject);
            var bodyBytes = Encoding.UTF8.GetBytes(bodyJson);

            await this.channel!.BasicPublishAsync(
                exchange: "",
                routingKey: replyTo,
                mandatory: true,
                basicProperties: new BasicProperties { CorrelationId = correlationId },
                body: bodyBytes);                
        }
    }
}