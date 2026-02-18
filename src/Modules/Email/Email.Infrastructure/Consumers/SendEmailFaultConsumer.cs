using EximEdge.Notification.Contracts.Email;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Email.Infrastructure.Consumers;

/// <summary>
/// Consumes <see cref="Fault{SendEmailMessage}"/> published automatically by MassTransit
/// when <see cref="SendEmailConsumer"/> exhausts all retry attempts.
/// Use this to persist failures, send ops alerts, or update job status.
/// </summary>
public sealed partial class SendEmailFaultConsumer(ILogger<SendEmailFaultConsumer> logger) : IConsumer<Fault<SendEmailMessage>>
{
    public Task Consume(ConsumeContext<Fault<SendEmailMessage>> context)
    {
        var fault = context.Message;
        var original = fault.Message;

        LogEmailFault(logger, original.JobId, original.To, fault.Exceptions.Length);

        // TODO: Persist failure to DB, send ops alert, update job status, etc.

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Email {JobId} to '{To}' permanently failed after {ExceptionCount} exception(s).")]
    private static partial void LogEmailFault(ILogger logger, Guid jobId, string to, int exceptionCount);
}
