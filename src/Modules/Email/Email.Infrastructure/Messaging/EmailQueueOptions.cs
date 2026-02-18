namespace Email.Infrastructure.Messaging;

/// <summary>
/// Queue name configuration for the Email module.
/// Bound from the <c>Email:Queues</c> configuration section.
/// </summary>
public sealed class EmailQueueOptions
{
    public const string SectionName = "Email:Queues";

    /// <summary>Primary send queue — <see cref="Consumers.SendEmailConsumer"/>.</summary>
    public string SendQueue { get; set; } = "email-queue";

    /// <summary>
    /// Fault notification queue — <see cref="Consumers.SendEmailFaultConsumer"/>.
    /// This is intentionally separate from MassTransit's auto-generated <c>email-queue_error</c> DLQ.
    /// </summary>
    public string FaultQueue { get; set; } = "email-faults";

    /// <summary>Resend request queue — <see cref="Consumers.ResendEmailConsumer"/>.</summary>
    public string ResendQueue { get; set; } = "email-resend-requests";
}
