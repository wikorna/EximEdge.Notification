using Email.Infrastructure.Exceptions;
using Email.Infrastructure.Messaging;
using MassTransit;

namespace Email.Infrastructure.Consumers;

/// <summary>
/// Encapsulates MassTransit consumer registration and explicit endpoint mapping
/// for the Email module. Called from WorkerHost during startup.
/// </summary>
public static class EmailMessagingConfiguration
{
    /// <summary>
    /// Registers all Email module consumers with the MassTransit bus.
    /// </summary>
    public static void AddEmailConsumers(this IBusRegistrationConfigurator cfg)
    {
        cfg.AddConsumer<SendEmailConsumer>();
        cfg.AddConsumer<SendEmailFaultConsumer>();
        cfg.AddConsumer<ResendEmailConsumer>();
    }

    /// <summary>
    /// Maps Email module consumers to explicit receive endpoints with per-queue
    /// durability, prefetch, concurrency, retry, and redelivery settings.
    /// </summary>
    public static void MapEmailEndpoints(
        this IBusFactoryConfigurator bus,
        IBusRegistrationContext context,
        EmailQueueOptions queues)
    {
        // ── Primary send queue ─────────────────────────────────────────
        bus.ReceiveEndpoint(queues.SendQueue, e =>
        {
            e.PrefetchCount = 4;
            e.ConcurrentMessageLimit = 2;

            e.ConfigureConsumer<SendEmailConsumer>(context);

            // Two-level retry pipeline (no RabbitMQ plugin required):
            //   [Outer: rate-limit] → [Inner: transient HTTP] → Consumer

            // Outer retry: rate-limit backoff for 429 responses.
            // Catches TooManyRequestsException that passes through the inner retry.
            e.UseMessageRetry(r =>
            {
                r.Handle<TooManyRequestsException>();
                r.Intervals(
                    TimeSpan.FromSeconds(15),
                    TimeSpan.FromSeconds(45),
                    TimeSpan.FromMinutes(2),
                    TimeSpan.FromMinutes(5));
            });

            // Inner retry: transient HTTP failures with short exponential backoff.
            e.UseMessageRetry(r =>
            {
                r.Handle<HttpRequestException>();
                r.Exponential(
                    retryLimit: 3,
                    minInterval: TimeSpan.FromSeconds(1),
                    maxInterval: TimeSpan.FromSeconds(10),
                    intervalDelta: TimeSpan.FromSeconds(2));
            });
        });

        // ── Fault notification queue ───────────────────────────────────
        // Receives Fault<SendEmailMessage> published by MassTransit when
        // the primary consumer exhausts all retries.
        // Intentionally separate from MassTransit's auto _error DLQ.
        bus.ReceiveEndpoint(queues.FaultQueue, e =>
        {
            e.PrefetchCount = 2;
            e.ConcurrentMessageLimit = 1;

            e.ConfigureConsumer<SendEmailFaultConsumer>(context);
        });

        // ── Resend requests queue ──────────────────────────────────────
        bus.ReceiveEndpoint(queues.ResendQueue, e =>
        {
            e.PrefetchCount = 4;
            e.ConcurrentMessageLimit = 2;

            e.ConfigureConsumer<ResendEmailConsumer>(context);

            e.UseMessageRetry(r =>
            {
                r.Exponential(
                    retryLimit: 3,
                    minInterval: TimeSpan.FromSeconds(1),
                    maxInterval: TimeSpan.FromSeconds(10),
                    intervalDelta: TimeSpan.FromSeconds(2));
                r.Handle<HttpRequestException>();
            });
        });
    }
}
