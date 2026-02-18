using EximEdge.Notification.Contracts.Email;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Email.Infrastructure.Consumers;

/// <summary>
/// Consumes <see cref="ResendEmailMessage"/> from the resend queue.
/// Re-attempts delivery for previously failed emails.
/// </summary>
public sealed partial class ResendEmailConsumer(ILogger<ResendEmailConsumer> logger) : IConsumer<ResendEmailMessage>
{
    public Task Consume(ConsumeContext<ResendEmailMessage> context)
    {
        var msg = context.Message;

        LogResendReceived(logger, msg.OriginalJobId, msg.To, msg.Subject);

        // TODO: Implement actual email re-delivery (SMTP / SendGrid / SES)

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Resend requested for email {OriginalJobId} to '{To}', subject '{Subject}'.")]
    private static partial void LogResendReceived(ILogger logger, Guid originalJobId, string to, string subject);
}
