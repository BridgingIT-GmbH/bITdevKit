namespace BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Defines optional broker-specific background processing that is executed by <see cref="QueueingService"/>.
/// </summary>
/// <remarks>
/// Queueing hosts exactly one <see cref="IHostedService"/> for the feature. Brokers that require polling,
/// lease renewal loops, or other background work expose that behavior through this contract instead of
/// registering their own hosted service.
/// </remarks>
public interface IQueueBrokerBackgroundProcessor
{
    /// <summary>
    /// Runs broker-specific background processing until shutdown is requested.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token that signals host shutdown.</param>
    Task RunAsync(CancellationToken cancellationToken = default);
}