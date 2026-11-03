using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Application.Extensions;
using TaskFlow.Application.Behaviors;

namespace TaskFlow.Application.Tests.Behaviors;

public sealed class TracingBehaviorTests
{
    private sealed record PingRequest(string Secret) : IRequest<string>;

    [Fact]
    public async Task Handle_ShouldStartAnActivityNamedAfterTheRequestType()
    {
        var started = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == TracingBehavior.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = started.Add,
        };
        ActivitySource.AddActivityListener(listener);
        var behavior = new TracingBehavior<PingRequest, string>();

        var result = await behavior.Handle(new PingRequest("do-not-trace"), _ => Task.FromResult("pong"), CancellationToken.None);

        Assert.Equal("pong", result);
        var activity = Assert.Single(started);
        Assert.Equal(nameof(PingRequest), activity.OperationName);
        Assert.Empty(activity.Tags);
        Assert.Empty(activity.TagObjects);
    }

    [Fact]
    public async Task Handle_ShouldStillCallNext_WhenNoListenerIsAttached()
    {
        var behavior = new TracingBehavior<PingRequest, string>();
        var called = false;

        var result = await behavior.Handle(
            new PingRequest("x"),
            _ =>
            {
                called = true;
                return Task.FromResult("ok");
            },
            CancellationToken.None);

        Assert.True(called);
        Assert.Equal("ok", result);
    }

    [Fact]
    public void AddApplicationValidation_ShouldRegisterTracingBeforeValidation()
    {
        var services = new ServiceCollection();

        services.AddApplicationValidation();

        var behaviors = services
            .Where(service => service.ServiceType == typeof(IPipelineBehavior<,>))
            .Select(service => service.ImplementationType)
            .ToList();
        var tracing = behaviors.IndexOf(typeof(TracingBehavior<,>));
        var validation = behaviors.IndexOf(typeof(ValidationBehavior<,>));
        Assert.True(tracing >= 0);
        Assert.True(tracing < validation);
    }
}
