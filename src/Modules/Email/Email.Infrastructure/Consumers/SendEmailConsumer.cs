using EximEdge.Notification.Contracts.Email;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Email.Infrastructure.Consumers;

/// <summary>
/// Consumes <see cref="SendEmailMessage"/> from the <c>email-send</c> queue.
/// Responsible for the actual email delivery (SMTP, SendGrid, etc.).
/// </summary>
public sealed partial class SendEmailConsumer(ILogger<SendEmailConsumer> logger) : IConsumer<SendEmailMessage>
{
    public Task Consume(ConsumeContext<SendEmailMessage> context)
    {
        var msg = context.Message;

        LogEmailReceived(logger, msg.JobId, msg.To, msg.Subject);

        // TODO: Implement actual email delivery (SMTP / SendGrid / SES)
        // Example:
        //   await _smtpService.SendAsync(msg.To, msg.Subject, msg.Body, context.CancellationToken);

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Consumed email message {JobId} for recipient '{To}', subject '{Subject}'.")]
    private static partial void LogEmailReceived(ILogger logger, Guid jobId, string to, string subject);
}
