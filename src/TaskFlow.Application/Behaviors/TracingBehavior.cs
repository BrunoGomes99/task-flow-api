using System.Diagnostics;
using MediatR;

namespace TaskFlow.Application.Behaviors;

public static class TracingBehavior
{
    public const string ActivitySourceName = "TaskFlow.Application";

    internal static readonly ActivitySource Source = new(ActivitySourceName);
}

public sealed class TracingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        using var activity = TracingBehavior.Source.StartActivity(typeof(TRequest).Name);
        return await next(cancellationToken);
    }
}
