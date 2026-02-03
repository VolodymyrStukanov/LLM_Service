using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace LLMService.Services.MessageQueueService
{
    public class ConsumingChannel
    {
        private TaskCompletionSource<bool> reconnectionTrigger = new();
        private readonly string queueName;
        private readonly IConnection connection;
        private IChannel? channel;
        private readonly Func<IChannel, Task> startConsuming;
        private int consecutiveFailuresCounter = 0;
        private const int MaxBackoffSeconds = 60;
        public ConsumingChannel(
            string queueName,
            IConnection connection,
            Func<IChannel, Task> startConsuming)
        {
            this.queueName = queueName;
            this.connection = connection;
            this.startConsuming = startConsuming;
        }

        public async Task RunChannel(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ConnectAndConsume(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    throw new OperationCanceledException("Operation canceled in a channel");
                }
                catch
                {
                    if(this.connection == null || !this.connection.IsOpen)
                        throw new InvalidOperationException("Connection lost in a channel");

                    this.consecutiveFailuresCounter++;

                    var backoffSeconds = Math.Pow(2, this.consecutiveFailuresCounter - 1);
                    backoffSeconds = backoffSeconds > MaxBackoffSeconds ? MaxBackoffSeconds : backoffSeconds;

                    await CleanupConnectionAsync();                
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(backoffSeconds), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        throw new InvalidOperationException("Connection lost in a channel");
                    }
                }
            }
        }

        private async Task ConnectAndConsume(CancellationToken stoppingToken)
        {
            this.channel = await connection.CreateChannelAsync();            
            this.channel.ChannelShutdownAsync += OnChannelShutdown;
            this.channel.CallbackExceptionAsync += OnChannelCallbackException;

            await this.channel.BasicQosAsync(
                prefetchSize: 0, 
                prefetchCount: 1, 
                global: false);

            await this.channel.QueueDeclareAsync(
                queue: this.queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);
                
            await startConsuming(this.channel);

            this.consecutiveFailuresCounter = 0;

            var cancellationTask = Task.Delay(Timeout.Infinite, stoppingToken);
            var reconnectTask = reconnectionTrigger.Task;
            
            var completedTask = await Task.WhenAny(cancellationTask, reconnectTask);            
            if (completedTask == reconnectTask)
            {
                throw new InvalidOperationException("Connection lost in a channel");
            }          
        }        

        private Task OnChannelShutdown(object? sender, ShutdownEventArgs args)
        {
            if (!args.Initiator.Equals(ShutdownInitiator.Application))
                reconnectionTrigger.TrySetResult(true);
            return Task.CompletedTask;
        }

        private Task OnChannelCallbackException(object? sender, CallbackExceptionEventArgs args)
        {
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
        }
    }
}