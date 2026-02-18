namespace EximEdge.Notification.Infrastructure.Messaging;

/// <summary>
/// Shared RabbitMQ connection options bound from the "RabbitMQ" configuration section.
/// Used by all hosts (ApiHost, WorkerHost) — single broker for every module.
/// </summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";

    public bool Enabled { get; set; }
    public RabbitMqConnectionOptions Connection { get; set; } = new();
}

public sealed class RabbitMqConnectionOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public bool UseSsl { get; set; }
}
