using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace TaskFlow.Api.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddTaskFlowSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "TaskFlow API",
                Version = "v1",
                Description = "REST API for task management. Click Authorize and paste only the JWT from POST /api/users/login (Swagger adds the \"Bearer \" prefix). Login/register work without a token.",
            });

            var bearerScheme = new OpenApiSecurityScheme
            {
                Description = "Paste only the accessToken value (do not include \"Bearer \").",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
            };

            options.AddSecurityDefinition("Bearer", bearerScheme);

            // Microsoft.OpenApi 2.x: AddSecurityRequirement(...) may not emit root "security" in swagger.json.
            options.DocumentFilter<BearerSecurityDocumentFilter>();
        });

        return services;
    }
}
