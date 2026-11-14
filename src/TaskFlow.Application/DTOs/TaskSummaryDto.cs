namespace TaskFlow.Application.DTOs;

/// <summary>
/// Count of all the user's tasks per status, ignoring list filters.
/// </summary>
public sealed record TaskSummaryDto(int Pending, int InProgress, int Completed, int Total);
