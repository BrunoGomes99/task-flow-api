using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace TaskFlow.Api.Extensions;

/// <summary>
/// Adds root-level OpenAPI <c>security</c> so Swagger UI sends the Bearer token.
/// Microsoft.OpenApi 2.x + Swashbuckle may not serialize <c>AddSecurityRequirement(...)</c> into swagger.json;
/// </summary>
/// <remarks>
/// Anonymous endpoints (e.g. login) still work at runtime without a token; the OpenAPI spec may still list the default security.
/// </remarks>
internal sealed class BearerSecurityDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(swaggerDoc);

        swaggerDoc.Security ??= [];
        swaggerDoc.Security.Add(new OpenApiSecurityRequirement
        {
            { new OpenApiSecuritySchemeReference("Bearer", swaggerDoc), new List<string>() },
        });
    }
}
