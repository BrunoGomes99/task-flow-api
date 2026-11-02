using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using TaskFlow.Application.Enums;
using TaskFlow.Application.UseCases.Reminders;
using TaskFlow.Application.UseCases.Reminders.DeliverDueSoonReminder;
using TaskFlow.Infrastructure.Configuration;

namespace TaskFlow.Infrastructure.Messaging;

public sealed class DueSoonConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RabbitMqSettings _settings;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<DueSoonConsumer> _logger;

    public DueSoonConsumer(
        RabbitMqSettings settings,
        IServiceScopeFactory scopes,
        ILogger<DueSoonConsumer> logger)
    {
        _settings = settings;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = RabbitMqConnectionFactory.Create(_settings);
        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await DueSoonTopology.DeclareAsync(channel, stoppingToken);
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: DueSoonTopology.Prefetch, global: false, cancellationToken: stoppingToken);

        var gate = new SemaphoreSlim(1, 1);
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) => HandleAsync(channel, gate, delivery, stoppingToken);

        await channel.BasicConsumeAsync(
            queue: DueSoonTopology.Queue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task HandleAsync(
        IChannel channel,
        SemaphoreSlim gate,
        BasicDeliverEventArgs delivery,
        CancellationToken stoppingToken)
    {
        DueSoonMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<DueSoonMessage>(delivery.Body.Span, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Discarding DueSoon message {DeliveryTag} with an unreadable body", delivery.DeliveryTag);
            await NackAsync(channel, gate, delivery.DeliveryTag, requeue: false, stoppingToken);
            return;
        }

        if (message is null || message.TaskId == Guid.Empty || message.UserId == Guid.Empty)
        {
            _logger.LogError("Discarding DueSoon message {DeliveryTag} with an empty identity", delivery.DeliveryTag);
            await NackAsync(channel, gate, delivery.DeliveryTag, requeue: false, stoppingToken);
            return;
        }

        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<DueSoonReminderDelivery>();
            var result = await handler.DeliverAsync(message, stoppingToken);
            if (result.Outcome == DueSoonDeliveryOutcome.Acknowledge)
            {
                await AckAsync(channel, gate, delivery.DeliveryTag, stoppingToken);
                return;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "DueSoon delivery failed for task {TaskId}", message.TaskId);
        }

        var attempt = DueSoonAttempts.Read(delivery.BasicProperties.Headers);
        if (attempt >= DueSoonTopology.MaxAttempts)
        {
            _logger.LogError(
                "DueSoon for task {TaskId} failed {Attempt} times and is dead-lettered",
                message.TaskId,
                attempt);
            await NackAsync(channel, gate, delivery.DeliveryTag, requeue: false, stoppingToken);
            return;
        }

        await RepublishAsync(channel, gate, delivery, attempt + 1, stoppingToken);
    }

    private static async Task AckAsync(
        IChannel channel,
        SemaphoreSlim gate,
        ulong deliveryTag,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await channel.BasicAckAsync(deliveryTag, multiple: false, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task NackAsync(
        IChannel channel,
        SemaphoreSlim gate,
        ulong deliveryTag,
        bool requeue,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await channel.BasicNackAsync(deliveryTag, multiple: false, requeue, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task RepublishAsync(
        IChannel channel,
        SemaphoreSlim gate,
        BasicDeliverEventArgs delivery,
        int nextAttempt,
        CancellationToken cancellationToken)
    {
        var properties = new BasicProperties
        {
            Persistent = true,
            Headers = new Dictionary<string, object?>
            {
                [DueSoonTopology.AttemptHeader] = nextAttempt,
            },
        };

        await gate.WaitAsync(cancellationToken);
        try
        {
            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: DueSoonTopology.Queue,
                mandatory: false,
                basicProperties: properties,
                body: delivery.Body,
                cancellationToken: cancellationToken);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }
}
