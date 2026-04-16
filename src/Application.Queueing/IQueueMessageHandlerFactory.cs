namespace BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Creates queue handler instances for runtime dispatch.
/// </summary>
public interface IQueueMessageHandlerFactory
{
    /// <summary>
    /// Creates an instance of the specified queue handler type.
    /// </summary>
    /// <param name="messageHandlerType">The concrete queue handler type.</param>
    /// <returns>The created handler instance.</returns>
    object Create(Type messageHandlerType);
}
