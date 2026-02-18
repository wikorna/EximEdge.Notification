using System.Reflection;
using Email.Infrastructure;
using Email.Infrastructure.Consumers;
using EximEdge.Notification.Infrastructure;
using EximEdge.Notification.WorkerHost;
using MassTransit;

var builder = Host.CreateApplicationBuilder(args);

// Shared messaging infrastructure (MassTransit + RabbitMQ) — with module consumers
builder.Services.AddRabbitMqMessaging(builder.Configuration, cfg =>
{
    // Discovers all IConsumer<T> implementations in Email.Infrastructure
    cfg.AddConsumer<SendEmailConsumer>(typeof(SendEmailConsumerDefinition));

    // Future modules:
    // cfg.AddConsumers(typeof(SendSmsConsumer).Assembly);
    // cfg.AddConsumers(typeof(SendLineConsumer).Assembly);
});

// Module-specific infrastructure (DbContext, repositories, etc.)
builder.Services.AddEmailInfrastructure(builder.Configuration);

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
