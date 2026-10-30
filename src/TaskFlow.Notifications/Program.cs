using TaskFlow.Infrastructure.Extensions;
using TaskFlow.Infrastructure.Health;
using TaskFlow.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddDueSoonNotifications(builder.Configuration);
builder.Services.AddTaskFlowHealthChecks(TaskFlowHealthRole.Notifications);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<MongoIndexesInitializer>();
    await initializer.InitializeAsync();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }));
app.MapHealthChecks("/health/ready");
app.Run();
