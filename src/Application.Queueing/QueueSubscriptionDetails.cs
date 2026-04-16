namespace BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Represents the single registered handler for a queue message type.
/// </summary>
public class QueueSubscriptionDetails
{
    private QueueSubscriptionDetails(Type messageType, Type handlerType)
    {
        this.MessageType = messageType;
        this.HandlerType = handlerType;
    }

    /// <summary>
    /// Gets the message type.
    /// </summary>
    public Type MessageType { get; }

    /// <summary>
    /// Gets the handler type.
    /// </summary>
    public Type HandlerType { get; }

    /// <summary>
    /// Creates a queue subscription detail instance.
    /// </summary>
    /// <param name="messageType">The message type.</param>
    /// <param name="handlerType">The handler type.</param>
    /// <returns>The subscription detail.</returns>
    public static QueueSubscriptionDetails Create(Type messageType, Type handlerType)
    {
        return new QueueSubscriptionDetails(messageType, handlerType);
    }
}
