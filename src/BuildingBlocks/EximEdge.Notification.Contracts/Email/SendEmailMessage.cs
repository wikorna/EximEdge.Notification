namespace EximEdge.Notification.Contracts.Email;

/// <summary>
/// Message contract for sending an email via the notification pipeline.
/// Published by ApiHost, consumed by WorkerHost.
/// </summary>
public sealed record SendEmailMessage
{
    public required Guid JobId { get; init; }
    public required string To { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}
