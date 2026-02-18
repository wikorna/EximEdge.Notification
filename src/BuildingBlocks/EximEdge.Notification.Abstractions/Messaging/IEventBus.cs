namespace EximEdge.Notification.Abstractions.Messaging;

/// <summary>
/// Transport-agnostic publish abstraction.
/// Application layer depends on this; Infrastructure implements it via MassTransit.
/// </summary>
public interface IEventBus
{
    Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class;
}
