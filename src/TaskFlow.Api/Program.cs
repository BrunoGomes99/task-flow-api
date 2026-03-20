using MediatR;
using TaskFlow.Application.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(TaskFlow.Application.UseCases.Tasks.CreateTask.CreateTaskCommand).Assembly));
builder.Services.AddApplicationValidation();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "TaskFlow API",
        Version = "v1",
        Description = "API REST for task management."
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "TaskFlow API v1");
    });
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
