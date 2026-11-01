namespace BridgingIT.DevKit.Application.Queueing;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Resolves queue handlers from the dependency injection container.
/// </summary>
public class ServiceProviderQueueMessageHandlerFactory(IServiceProvider serviceProvider) : IQueueMessageHandlerFactory, IAsyncQueueMessageHandlerFactory, IAuditExecutionIsolation
{
    private readonly IServiceProvider serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    /// <summary>
    /// Creates a queue handler instance within an owned scoped service provider.
    /// </summary>
    /// <param name="messageHandlerType">The concrete handler type to create.</param>
    /// <returns>The resolved handler instance together with its owned scope.</returns>
    public QueueMessageHandlerFactoryResult Create(Type messageHandlerType)
    {
        var scope = this.serviceProvider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetService(messageHandlerType);
        var createdByActivator = handler is null;

        handler ??= ActivatorUtilities.CreateInstance(scope.ServiceProvider, messageHandlerType);

        return new QueueMessageHandlerFactoryResult(
            handler,
            async () =>
            {
                if (createdByActivator)
                {
                    if (handler is IAsyncDisposable asyncDisposable)
                    {
                        await asyncDisposable.DisposeAsync();
                    }
                    else if (handler is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }

                await scope.DisposeAsync();
            });
    }

    /// <inheritdoc />
    public Task<T> RunIsolatedAsync<T>(string feature, string operationId, string token, string correlationId, Func<Task<T>> next)
        => AuditSourceExecutor.RunIsolatedAsync(this.serviceProvider.GetRequiredService<IServiceScopeFactory>(), feature, operationId, token, correlationId, next);

    /// <inheritdoc />
    public async ValueTask<QueueMessageHandlerFactoryResult> CreateAsync(Type messageHandlerType)
    {
        var scope = this.serviceProvider.CreateAsyncScope();
        try
        {
            var handler = scope.ServiceProvider.GetService(messageHandlerType);
            var createdByActivator = handler is null;
            handler ??= ActivatorUtilities.CreateInstance(scope.ServiceProvider, messageHandlerType);
            return new QueueMessageHandlerFactoryResult(handler, async () =>
            {
                try
                {
                    if (createdByActivator)
                    {
                        if (handler is IAsyncDisposable asynchronous) await asynchronous.DisposeAsync();
                        else if (handler is IDisposable synchronous) synchronous.Dispose();
                    }
                }
                finally { await scope.DisposeAsync(); }
            });
        }
        catch
        {
            try { await scope.DisposeAsync(); }
            catch (Exception) { /* Keep the original resolution exception. */ }

            throw;
        }
    }
}
