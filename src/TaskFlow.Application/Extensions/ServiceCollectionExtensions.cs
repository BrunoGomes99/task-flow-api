using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Application.Behaviors;
using TaskFlow.Application.Caching;
using TaskFlow.Application.Interfaces;
using TaskFlow.Application.UseCases.Tasks.CreateTask;

namespace TaskFlow.Application.Extensions;

/// <summary>
/// Extension methods for registering Application layer services (validators and validation pipeline).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all FluentValidation validators from the Application assembly, adds
    /// the ValidationBehavior to the MediatR pipeline, and registers task-cache invalidation.
    /// </summary>
    public static IServiceCollection AddApplicationValidation(this IServiceCollection services)
    {
        Assembly assembly = typeof(CreateTaskCommandValidator).Assembly;

        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddSingleton<ITaskCacheInvalidator, TaskCacheInvalidator>();

        return services;
    }
}
