namespace BridgingIT.DevKit.Application.Queueing;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Resolves queue handlers from the dependency injection container.
/// </summary>
public class ServiceProviderQueueMessageHandlerFactory(IServiceProvider serviceProvider) : IQueueMessageHandlerFactory
{
    private readonly IServiceProvider serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    /// <summary>
    /// Creates a queue handler instance within a scoped service provider.
    /// </summary>
    /// <param name="messageHandlerType">The concrete handler type to create.</param>
    /// <returns>The resolved handler instance.</returns>
    public object Create(Type messageHandlerType)
    {
        using var scope = this.serviceProvider.CreateScope();

        return ActivatorUtilities.CreateInstance(scope.ServiceProvider, messageHandlerType);
    }
}
