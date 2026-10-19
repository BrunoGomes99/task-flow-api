using TaskFlow.Infrastructure.Extensions;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Scheduler;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddDueSoonScheduler(builder.Configuration);
builder.Services.AddHostedService<DueSoonScanHostedService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<MongoIndexesInitializer>();
    await initializer.InitializeAsync();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.Run();
