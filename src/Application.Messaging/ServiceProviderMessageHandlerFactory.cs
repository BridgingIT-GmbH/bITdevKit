// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Represents service provider message handler factory.
/// </summary>
public class ServiceProviderMessageHandlerFactory : IMessageHandlerFactory, IAsyncMessageHandlerFactory, IAuditExecutionIsolation
{
    private readonly IServiceProvider serviceProvider;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ServiceProviderMessageHandlerFactory" /> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    public ServiceProviderMessageHandlerFactory(IServiceProvider serviceProvider)
    {
        EnsureArg.IsNotNull(serviceProvider, nameof(serviceProvider));

        this.serviceProvider = serviceProvider;
    }

    /// <summary>
    ///     Creates the specified message handler type within an owned scoped service provider.
    /// </summary>
    /// <param name="messageHandlerType">Type of the message handler.</param>
    public MessageHandlerFactoryResult Create(Type messageHandlerType)
    {
        var scope = this.serviceProvider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetService(messageHandlerType);
        var createdByActivator = handler is null;

        handler ??= ActivatorUtilities.CreateInstance(scope.ServiceProvider, messageHandlerType);

        return new MessageHandlerFactoryResult(
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
    public async ValueTask<MessageHandlerFactoryResult> CreateAsync(Type messageHandlerType)
    {
        var scope = this.serviceProvider.CreateAsyncScope();
        try
        {
            var handler = scope.ServiceProvider.GetService(messageHandlerType);
            var createdByActivator = handler is null;
            handler ??= ActivatorUtilities.CreateInstance(scope.ServiceProvider, messageHandlerType);
            return new MessageHandlerFactoryResult(handler, async () =>
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
