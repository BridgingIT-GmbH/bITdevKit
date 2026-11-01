namespace BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Creates queue handler instances for runtime dispatch.
/// </summary>
public interface IQueueMessageHandlerFactory
{
    /// <summary>
    /// Creates an owned instance of the specified queue handler type.
    /// </summary>
    /// <param name="messageHandlerType">The concrete queue handler type.</param>
    /// <returns>The created handler instance together with its owned lifetime.</returns>
    QueueMessageHandlerFactoryResult Create(Type messageHandlerType);

}

/// <summary>Optionally creates source handlers with awaited cleanup of scoped asynchronous dependencies on resolution failure.</summary>
/// <example><code>await using var handler = await factory.CreateAsync(typeof(CustomerHandler));</code></example>
public interface IAsyncQueueMessageHandlerFactory
{
    /// <summary>Resolves a handler or disposes its failed resolution scope before rethrowing the original exception.</summary>
    /// <example><code>var handler = await factory.CreateAsync(typeof(CustomerHandler));</code></example>
    ValueTask<QueueMessageHandlerFactoryResult> CreateAsync(Type messageHandlerType);
}
