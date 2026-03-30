using System.Reflection;
using DomainTask = TaskFlow.Domain.Entities.Task;
using TaskEntity = TaskFlow.Domain.Entities.Task;
using TaskFlow.Domain.SeedWork;
using TaskFlow.Infrastructure.Persistence.Documents;

namespace TaskFlow.Infrastructure.Persistence.Mappers;

internal static class TaskDocumentMapper
{
    public static TaskDocument ToDocument(DomainTask task)
    {
        ArgumentNullException.ThrowIfNull(task);

        return new TaskDocument
        {
            Id = task.Id,
            UserId = task.UserId,
            Title = task.Title,
            Description = task.Description,
            Status = task.Status,
            DueDate = task.DueDate is null ? null : EnsureUtc(task.DueDate.Value),
            CreatedAt = EnsureUtc(task.CreatedAt),
            UpdatedAt = EnsureUtc(task.UpdatedAt)
        };
    }

    public static DomainTask ToDomain(TaskDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var task = new TaskEntity(
            document.UserId,
            document.Title,
            document.Description,
            document.Status,
            document.DueDate is null ? null : EnsureUtc(document.DueDate.Value));

        SetProperty(task, nameof(Entity.Id), document.Id);
        SetProperty(task, nameof(TaskEntity.CreatedAt), EnsureUtc(document.CreatedAt));
        SetProperty(task, nameof(TaskEntity.UpdatedAt), EnsureUtc(document.UpdatedAt));
        return task;
    }

    private static void SetProperty<TTarget>(TTarget target, string propertyName, object value)
    {
        var property = typeof(TTarget).GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Property '{propertyName}' was not found on '{typeof(TTarget).Name}'.");

        var setter = property.GetSetMethod(nonPublic: true)
            ?? throw new InvalidOperationException($"Property '{propertyName}' does not have a setter.");

        setter.Invoke(target, [value]);
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
