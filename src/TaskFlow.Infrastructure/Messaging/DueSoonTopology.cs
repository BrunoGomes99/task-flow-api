using RabbitMQ.Client;

namespace TaskFlow.Infrastructure.Messaging;

public static class DueSoonTopology
{
    public const string Queue = "taskflow.due-soon";
    public const string DeadLetterQueue = "taskflow.due-soon.dlq";
    public const string AttemptHeader = "x-reminder-attempt";
    public const int MaxAttempts = 5;
    public const ushort Prefetch = 1;

    public static async Task DeclareAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.QueueDeclareAsync(
            queue: DeadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        var arguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = string.Empty,
            ["x-dead-letter-routing-key"] = DeadLetterQueue,
        };

        await channel.QueueDeclareAsync(
            queue: Queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: arguments,
            cancellationToken: cancellationToken);
    }
}
