namespace EximEdge.Notification.Contracts.Email;

/// <summary>
/// Message contract for requesting re-delivery of a previously failed email.
/// Published by error handling or admin tooling, consumed by WorkerHost.
/// </summary>
public sealed record ResendEmailMessage
{
    public required Guid OriginalJobId { get; init; }
    public required string To { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public required DateTime RequestedAtUtc { get; init; }
}
