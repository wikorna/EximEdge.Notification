using MassTransit;

namespace Email.Infrastructure.Consumers;

/// <summary>
/// MassTransit endpoint definition for <see cref="SendEmailConsumer"/>.
/// Controls queue name, concurrency, and retry policy.
/// </summary>
public sealed class SendEmailConsumerDefinition : ConsumerDefinition<SendEmailConsumer>
{
    public SendEmailConsumerDefinition()
    {
        EndpointName = "email-send";
        ConcurrentMessageLimit = 16;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<SendEmailConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        // Exponential retry: 3 attempts (1 s → 5 s → 25 s).
        // After exhaustion MassTransit moves the message to email-send_error (DLQ).
        endpointConfigurator.UseMessageRetry(r =>
            r.Exponential(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5)));
    }
}
