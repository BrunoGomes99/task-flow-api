using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using TaskFlow.Application.Interfaces;
using TaskFlow.Application.UseCases.Reminders;
using TaskFlow.Infrastructure.Configuration;

namespace TaskFlow.Infrastructure.Messaging;

public sealed class DueSoonPublisher : IDueSoonPublisher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RabbitMqSettings _settings;
    private readonly ILogger<DueSoonPublisher> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public DueSoonPublisher(RabbitMqSettings settings, ILogger<DueSoonPublisher> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task PublishAsync(DueSoonMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var body = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var channel = await GetChannelAsync(cancellationToken);
            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: DueSoonTopology.Queue,
                mandatory: false,
                basicProperties: new BasicProperties { Persistent = true },
                body: body,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish DueSoon for task {TaskId}", message.TaskId);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
            await _channel.DisposeAsync();

        if (_connection is not null)
            await _connection.DisposeAsync();
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
            return _channel;

        _channel?.Dispose();
        if (_connection is not { IsOpen: true })
        {
            _connection?.Dispose();
            var factory = RabbitMqConnectionFactory.Create(_settings);
            _connection = await factory.CreateConnectionAsync(cancellationToken);
        }

        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await DueSoonTopology.DeclareAsync(_channel, cancellationToken);
        return _channel;
    }
}
