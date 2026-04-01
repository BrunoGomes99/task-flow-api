using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using TaskFlow.Application.Interfaces;
using TaskFlow.Infrastructure.Configuration;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Infrastructure.Repositories;
using TaskFlow.Infrastructure.Security;

namespace TaskFlow.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var settings = configuration
            .GetSection(MongoDbSettings.SectionName)
            .Get<MongoDbSettings>() ?? new MongoDbSettings();

        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
            throw new InvalidOperationException("MongoDb:ConnectionString must be configured.");

        if (string.IsNullOrWhiteSpace(settings.DatabaseName))
            throw new InvalidOperationException("MongoDb:DatabaseName must be configured.");

        services.AddSingleton(settings);
        services.AddSingleton<IMongoClient>(_ => new MongoClient(settings.ConnectionString));
        services.AddSingleton<TaskFlowMongoContext>();
        services.AddSingleton<MongoIndexesInitializer>();

        services.Configure<PasswordHashingSettings>(configuration.GetSection(PasswordHashingSettings.SectionName));
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddSingleton<IJwtService, JwtService>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();

        return services;
    }
}
