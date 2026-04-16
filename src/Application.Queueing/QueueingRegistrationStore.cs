namespace BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Stores queue subscriptions collected from one or more <c>AddQueueing</c> calls.
/// </summary>
public class QueueingRegistrationStore
{
    private readonly List<QueueSubscriptionRegistration> subscriptions = [];

    /// <summary>
    /// Gets the collected subscriptions.
    /// </summary>
    public IReadOnlyCollection<QueueSubscriptionRegistration> Subscriptions => this.subscriptions.AsReadOnly();

    /// <summary>
    /// Adds a queue subscription when it has not already been registered.
    /// </summary>
    /// <param name="messageType">The queued message type.</param>
    /// <param name="handlerType">The queue handler type.</param>
    public void Add(Type messageType, Type handlerType)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        ArgumentNullException.ThrowIfNull(handlerType);

        if (this.subscriptions.Any(item => item.MessageType == messageType && item.HandlerType == handlerType))
        {
            return;
        }

        this.subscriptions.Add(new QueueSubscriptionRegistration(messageType, handlerType));
    }
}

/// <summary>
/// Represents a collected queue subscription.
/// </summary>
/// <param name="MessageType">The queued message type.</param>
/// <param name="HandlerType">The queue handler type.</param>
public sealed record QueueSubscriptionRegistration(Type MessageType, Type HandlerType);
