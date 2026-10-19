using RabbitMQ.Client;
using TaskFlow.Infrastructure.Configuration;

namespace TaskFlow.Infrastructure.Messaging;

internal static class RabbitMqConnectionFactory
{
    public static ConnectionFactory Create(RabbitMqSettings settings)
    {
        var user = Uri.EscapeDataString(settings.User);
        var password = Uri.EscapeDataString(settings.Password);
        return new ConnectionFactory
        {
            Uri = new Uri($"amqp://{user}:{password}@{settings.Host}:{settings.Port}/"),
            ConsumerDispatchConcurrency = 1,
        };
    }
}
