using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Contracts;
using TaskFlow.Api.Extensions;
using TaskFlow.Application.Common;
using TaskFlow.Application.DTOs;
using TaskFlow.Application.UseCases.Tasks.CreateTask;
using TaskFlow.Application.UseCases.Tasks.DeleteTask;
using TaskFlow.Application.UseCases.Tasks.GetTaskById;
using TaskFlow.Application.UseCases.Tasks.ListTasks;
using TaskFlow.Application.UseCases.Tasks.UpdateTask;
using TaskFlow.Application.UseCases.Tasks.UpdateTaskStatus;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/tasks")]
[Produces("application/json")]
public sealed class TasksController(IMediator mediator) : TaskFlowControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TaskDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(
        [FromQuery] ListTasksQueryParameters query,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await mediator.Send(
            new ListTasksQuery(
                userId,
                query.PageNumber,
                query.PageSize,
                query.TitleContains,
                query.DescriptionContains,
                query.Status,
                query.DueDateOrder),
            cancellationToken);

        return FromResult(result);
    }

    [HttpGet("{taskId:guid}")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid taskId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await mediator.Send(new GetTaskByIdQuery(userId, taskId), cancellationToken);

        return FromResult(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreateTaskResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await mediator.Send(
            new CreateTaskCommand(userId, request.Title, request.Description, request.Status, request.DueDate),
            cancellationToken);

        if (!result.IsSuccess)
            return FromResult(result);

        return CreatedAtAction(nameof(GetById), new { taskId = result.Value!.Id }, result.Value);
    }

    [HttpPut("{taskId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid taskId,
        [FromBody] UpdateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await mediator.Send(
            new UpdateTaskCommand(userId, taskId, request.Title, request.Description),
            cancellationToken);

        return FromResult(result);
    }

    [HttpPatch("{taskId:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        Guid taskId,
        [FromBody] UpdateTaskStatusRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await mediator.Send(new UpdateTaskStatusCommand(userId, taskId, request.Status), cancellationToken);

        return FromResult(result);
    }

    [HttpDelete("{taskId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid taskId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await mediator.Send(new DeleteTaskCommand(userId, taskId), cancellationToken);

        return FromResult(result);
    }
}
