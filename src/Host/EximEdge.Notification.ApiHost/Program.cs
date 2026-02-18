using Email.Api;
using EximEdge.Notification.ApiHost.Modules;
using EximEdge.Notification.Infrastructure;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Shared messaging infrastructure (MassTransit + RabbitMQ) — publisher only, no consumers
builder.Services.AddRabbitMqMessaging(builder.Configuration);

// Shared caching infrastructure (HybridCache L1 + Redis L2 + health check)
builder.Services.AddCaching(builder.Configuration);

// Module registrations (Application + Infrastructure services per module)
builder.Services.AddModule(builder.Configuration);


builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "EximEdge Notification API",
        Description = "API for managing notifications in the EximEdge system.",
        Contact = new OpenApiContact
        {
            Name = "EximEdge Support",
            Email = "support@eximedge.com",
            Url = new Uri("https://www.eximedge.com/support")
        }
    });
});

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "EximEdge Notification API V1");
        c.RoutePrefix = string.Empty; // Set Swagger UI at the app's root
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.MapEmailEndpoints();

app.MapHealthChecks("/health");

await app.RunAsync();
