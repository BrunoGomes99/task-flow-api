using MediatR;
using TaskFlow.Application.Interfaces;

namespace TaskFlow.Application.UseCases.Tasks.DeleteTask;

/// <summary>
/// Handles task deletion for the authenticated user.
/// </summary>
public sealed class DeleteTaskCommandHandler : IRequestHandler<DeleteTaskCommand, bool>
{
    private readonly ITaskRepository _taskRepository;

    public DeleteTaskCommandHandler(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public Task<bool> Handle(DeleteTaskCommand request, CancellationToken cancellationToken) =>
        _taskRepository.DeleteAsync(request.UserId, request.TaskId, cancellationToken);
}
