using Email.Infrastructure;
using Email.Infrastructure.Consumers;
using Email.Infrastructure.Messaging;
using EximEdge.Notification.Infrastructure;
using EximEdge.Notification.WorkerHost;

var builder = Host.CreateApplicationBuilder(args);

var emailQueues = builder.Configuration
    .GetSection(EmailQueueOptions.SectionName)
    .Get<EmailQueueOptions>() ?? new EmailQueueOptions();

// Shared messaging infrastructure (MassTransit + RabbitMQ) — with module consumers
builder.Services.AddRabbitMqMessaging(
    builder.Configuration,
    configureBus: cfg =>
    {
        cfg.AddEmailConsumers();

        // Future modules:
        // cfg.AddSmsConsumers();
        // cfg.AddLineConsumers();
    },
    configureEndpoints: (context, bus) =>
    {
        bus.MapEmailEndpoints(context, emailQueues);

        // Future modules:
        // bus.MapSmsEndpoints(context, smsQueues);
        // bus.MapLineEndpoints(context, lineQueues);
    });

// Module-specific infrastructure (DbContext, repositories, etc.)
builder.Services.AddEmailInfrastructure(builder.Configuration);

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
