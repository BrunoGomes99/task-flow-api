using TaskFlow.Application.UseCases.Reminders;

namespace TaskFlow.Application.Interfaces;

public interface IDueSoonPublisher
{
    Task PublishAsync(DueSoonMessage message, CancellationToken cancellationToken = default);
}
