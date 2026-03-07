using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Application.Behaviors;
using TaskFlow.Application.DTOs.Tasks.CreateTask;

namespace TaskFlow.Application.Extensions;

/// <summary>
/// Extension methods for registering Application layer services (validators and validation pipeline).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all FluentValidation validators from the Application assembly and adds
    /// the ValidationBehavior to the MediatR pipeline so every request is validated before its handler runs.
    /// </summary>
    public static IServiceCollection AddApplicationValidation(this IServiceCollection services)
    {
        Assembly assembly = typeof(CreateTaskCommandValidator).Assembly;

        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
