using System.Text.Json;
using System.Text.Json.Serialization;
using TaskFlow.Api.Extensions;
using TaskFlow.Api.Middleware;
using TaskFlow.Application.Extensions;
using TaskFlow.Infrastructure.Extensions;
using TaskFlow.Infrastructure.Health;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    });

builder.Services.AddTaskFlowJwtAuthentication(builder.Configuration);
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(TaskFlow.Application.UseCases.Tasks.CreateTask.CreateTaskCommand).Assembly));
builder.Services.AddApplicationValidation();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddTaskFlowHealthChecks(TaskFlowHealthRole.Api);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddTaskFlowSwagger();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseExceptionHandler();


var enableSwagger = app.Environment.IsDevelopment()
    || app.Configuration.GetValue("EnableSwagger", false);

if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "TaskFlow API v1");
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health/ready");

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<TaskFlow.Infrastructure.Persistence.MongoIndexesInitializer>();
    await initializer.InitializeAsync();
}

app.Run();
