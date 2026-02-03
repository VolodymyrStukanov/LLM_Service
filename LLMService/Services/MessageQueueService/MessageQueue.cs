using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using LLMService.Services.LLMService;
using LLMService.Services.LLMService.models;
using LLMService.Services.MessageQueueService;
using LLMService.Services.MessageQueueService.models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace LLMService.Sevices.MessageQueueService
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

        private IConnection? inputConnection;
        private IConnection? outputConnection;
        private bool publishingStarted = false;
        private IChannel? outputChannel;
        private readonly string inputQueueName;
        private readonly ConnectionFactory connectionFactory;
        private readonly LlmService llmService;
        private readonly ILogger<MessageQueue> logger;

        private readonly ConcurrentQueue<OutputMessage> outputQueue = new();

        private readonly int maxConcurrentInputChannels;
        private readonly SemaphoreSlim outputConnectionLock = new(1, 1);
        private readonly SemaphoreSlim startPublishingLock = new(1, 1);

        private TaskCompletionSource<bool> reconnectionTrigger = new();
        private int consecutiveFailuresCounter = 0;
        private const int MaxBackoffSeconds = 60;
        public MessageQueue(IConfiguration config,  ILogger<MessageQueue> logger, LlmService llmService)
        {
            var hostName = config["RabbitMQ:HostName"]!;
            this.inputQueueName = config["RabbitMQ:QueueName"]!;
            this.maxConcurrentInputChannels = config.GetValue<int>("RabbitMQ:MaxConcurrentInputChannels", 10);
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
                        $"Connection failed (consecutive failures: {consecutiveFailuresCounter}). " +
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

            this.inputConnection = await this.connectionFactory.CreateConnectionAsync();
            this.inputConnection.ConnectionShutdownAsync += OnConnectionShutdown;
            this.inputConnection.CallbackExceptionAsync += OnCallbackException;

            await CreateConsumingChannels(stoppingToken);
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
            if (this.inputConnection != null)
            {
                try { await this.inputConnection.CloseAsync(); } catch { }
                try { await this.inputConnection.DisposeAsync(); } catch { }
                this.inputConnection = null;
            }
        }

        private async Task CreateConsumingChannels(CancellationToken stoppingToken)
        {
            for (int i = 0; i < maxConcurrentInputChannels; i++)
            {
                var consumingChannel = new ConsumingChannel(this.inputQueueName, this.inputConnection!, StartConsuming);
                _ = Task.Run(async () => 
                {
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        try
                        {
                            await consumingChannel.RunChannel(stoppingToken);
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
                });
                
            }
        }

        private async Task StartConsuming(IChannel channel)
        {
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                string? correlationId = ea.BasicProperties.CorrelationId;

                try
                {
                    if (string.IsNullOrEmpty(correlationId))
                    {
                        this.logger.LogWarning("Received message without correlationId");
                        await channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
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
                        await channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                        return;
                    }

                    try
                    {
                        if (!llmProvidersDict.TryGetValue(inputMessage.ModelProvider, out var provider))
                        {
                            this.logger.LogError($"Unknown provider: {inputMessage.ModelProvider}. Correlation id: {correlationId}");
                            await channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                            return;
                        }

                        var response = await GetCompletionWithRetry(provider, inputMessage.Prompt, correlationId);
                        
                        await startPublishingLock.WaitAsync();
                        if(!publishingStarted) await StartPublishing();
                        startPublishingLock.Release();
                        
                        var responseObject = new
                        {
                            LlmResponse = JsonDocument.Parse(response, new JsonDocumentOptions
                            {
                                AllowTrailingCommas = true
                            }).RootElement
                        };
                        var bodyJson = JsonSerializer.Serialize(responseObject);
                        var bodyBytes = Encoding.UTF8.GetBytes(bodyJson);

                        this.outputQueue.Enqueue(new OutputMessage(correlationId, inputMessage.ReplyTo, bodyBytes));
                        await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);                        
                        this.logger.LogInformation($"Successfully processed message. Correlation id: {correlationId}");
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError(ex, $"Error during processing message. Correlation id: {correlationId}");
                        var shouldRequeue = IsTransientError(ex);
                        await channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: shouldRequeue);
                        
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
                    await channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                }
            };

            await channel.BasicConsumeAsync(this.inputQueueName, autoAck: false, consumer: consumer);
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

        private async Task EnsureOutputConnectionAsync()
        {
            if (this.outputConnection != null && this.outputConnection.IsOpen 
                && this.outputChannel != null && this.outputChannel.IsOpen) return;

            await outputConnectionLock.WaitAsync();
            try
            {
                if (this.outputConnection != null && this.outputConnection.IsOpen 
                    && this.outputChannel != null && this.outputChannel.IsOpen) return;

                this.outputConnection = await this.connectionFactory.CreateConnectionAsync();
                this.logger.LogInformation("RabbitMQ connection established");

                this.outputChannel = await this.outputConnection.CreateChannelAsync();
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to establish RabbitMQ output connection");
                
                if (this.outputConnection != null)
                {
                    await this.outputConnection.CloseAsync();
                    await this.outputConnection.DisposeAsync();
                    this.outputConnection = null;
                }
                
                throw;
            }
            finally
            {
                outputConnectionLock.Release();
            }
        }

        private async Task StartPublishing()
        {
            try
            {
                publishingStarted = true;
                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        if(this.outputQueue.TryDequeue(out var message))
                        {
                            var attempt = 0;
                            const int maxAttempts = 5;
                            var published = false;

                            while (attempt < maxAttempts && !published)
                            {
                                try
                                {
                                    attempt++;

                                    string jsonString = JsonSerializer.Serialize(message);
                                    var body = Encoding.UTF8.GetBytes(jsonString);

                                    await EnsureOutputConnectionAsync();

                                    await this.outputChannel!.BasicPublishAsync(
                                        exchange: "",
                                        routingKey: message.ReplyTo,
                                        mandatory: true,
                                        basicProperties: new BasicProperties { CorrelationId = message.CorrelationId },
                                        body: message.ResponseBody); 
                                    
                                    published = true;
                                }
                                catch (Exception ex) when (attempt < maxAttempts)
                                {
                                    this.outputQueue.Enqueue(message);
                                    this.logger.LogWarning(ex, 
                                        $"Failed to publish message (attempt {attempt}/{maxAttempts}), retrying...");
                                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                                }
                                catch (Exception ex)
                                {
                                    this.outputQueue.Enqueue(message);
                                    this.logger.LogError(ex, 
                                        $"Failed to publish message after {maxAttempts} attempts");
                                }
                            }
                        }
                        else await Task.Delay(1000);
                    }
                });
            }
            catch(Exception ex)
            {
                publishingStarted = false;
                this.logger.LogError(ex, $"Failed to publish messages.");
            }
        }
    }
}