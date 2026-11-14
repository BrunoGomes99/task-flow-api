using MediatR;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;

namespace TaskFlow.Application.UseCases.Tasks.GetTaskSummary;

/// <summary>
/// Query to count the user's tasks per status. UserId is set by the API from JWT (multi-tenancy).
/// </summary>
public sealed record GetTaskSummaryQuery(Guid UserId) : IRequest<Result<TaskSummaryDto>>;
