namespace Email.Infrastructure.Exceptions;

/// <summary>
/// Thrown when an upstream email provider responds with HTTP 429 (Too Many Requests).
/// Used as a filter for delayed redelivery policies.
/// </summary>
public sealed class TooManyRequestsException : Exception
{
    public TooManyRequestsException() { }

    public TooManyRequestsException(string message) : base(message) { }

    public TooManyRequestsException(string message, Exception innerException) : base(message, innerException) { }
}
