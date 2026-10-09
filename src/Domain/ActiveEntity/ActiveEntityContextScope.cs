// MIT-License ...
namespace BridgingIT.DevKit.Domain;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Manages execution of work that requires an ActiveEntityContext, creating a scoped context
/// when one is not supplied and disposing its DI scope after use.
/// </summary>
public static class ActiveEntityContextScope
{
    /// <summary>Executes work with an existing or newly created context using the default operation name.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TId">The identifier type.</typeparam>
    /// <typeparam name="TResult">The delegate's result type.</typeparam>
    /// <param name="context">The existing context, or null to create a scoped one.</param>
    /// <param name="action">The business delegate to execute once.</param>
    /// <returns>The delegate's result.</returns>
    /// <example><code>var result = await ActiveEntityContextScope.UseAsync(context, ctx =&gt; ctx.Provider.CountAsync());</code></example>
    public static Task<TResult> UseAsync<TEntity, TId, TResult>(
        ActiveEntityContext<TEntity, TId> context,
        Func<ActiveEntityContext<TEntity, TId>, Task<TResult>> action)
        where TEntity : ActiveEntity<TEntity, TId> =>
        UseAsync(context, action, default, "UseContext");

    /// <summary>
    /// Executes an action with an existing or newly created <see cref="ActiveEntityContext{TEntity,TId}"/>.
    /// Creates a new DI scope only when <paramref name="context"/> is null and disposes it after execution.
    /// </summary>
    /// <typeparam name="TEntity">Entity type.</typeparam>
    /// <typeparam name="TId">Identifier type.</typeparam>
    /// <typeparam name="TResult">Result type returned by the action.</typeparam>
    /// <param name="context">Existing context (reused) or null to create a scoped one.</param>
    /// <param name="action">Delegate to execute with the ensured context.</param>
    /// <param name="cancellationToken">The application's token, forwarded to optional operation behaviors.</param>
    /// <param name="operation">The calling operation name, supplied automatically unless overridden.</param>
    /// <returns>The delegate result.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="action"/> is null.</exception>
    /// <example><code>var result = await ActiveEntityContextScope.UseAsync(context, ctx =&gt; ctx.Provider.CountAsync(cancellationToken), cancellationToken);</code></example>
    public static async Task<TResult> UseAsync<TEntity, TId, TResult>(
        ActiveEntityContext<TEntity, TId> context,
        Func<ActiveEntityContext<TEntity, TId>, Task<TResult>> action,
        CancellationToken cancellationToken,
        [CallerMemberName] string operation = null)
        where TEntity : ActiveEntity<TEntity, TId>
    {
        ArgumentNullException.ThrowIfNull(action);

        IAsyncDisposable scope = null;

        try
        {
            if (context == null)
            {
                var createdScope = GetServiceProvider().CreateAsyncScope();
                scope = createdScope;

                context = new ActiveEntityContext<TEntity, TId>(
                    GetProvider<TEntity, TId>(createdScope.ServiceProvider),
                    GetBehaviors<TEntity, TId>(createdScope.ServiceProvider));
            }

            if (!context.Behaviors.Any(static behavior => behavior is IActiveEntityOperationBehavior<TEntity>))
            {
                return await action(context).ConfigureAwait(false);
            }

            Func<Task<TResult>> next = () => action(context);
            foreach (var behavior in context.Behaviors.Reverse())
            {
                if (behavior is IActiveEntityOperationBehavior<TEntity> operationBehavior)
                {
                    var continuation = next;
                    next = () => operationBehavior.ExecuteAsync(operation, continuation, cancellationToken);
                }
            }

            return await next().ConfigureAwait(false);
        }
        finally
        {
            if (scope != null) // dispose scope after action has been executed
            {
                await scope.DisposeAsync();
            }
        }
    }

    private static IActiveEntityEntityProvider<TEntity, TId> GetProvider<TEntity, TId>(IServiceProvider sp)
        where TEntity : ActiveEntity<TEntity, TId> =>
        sp.GetRequiredService<IActiveEntityEntityProvider<TEntity, TId>>();

    private static IEnumerable<IActiveEntityBehavior<TEntity>> GetBehaviors<TEntity, TId>(IServiceProvider sp)
        where TEntity : ActiveEntity<TEntity, TId> =>
        sp.GetServices<IActiveEntityBehavior<TEntity>>();

    private static IServiceProvider GetServiceProvider() =>
        ActiveEntityConfigurator.GetGlobalServiceProvider() ??
        throw new InvalidOperationException("No service provider configured for active entities. Call app.UseActiveEntity(app.Services) or ActiveEntityConfigurator.SetGlobalServiceProvider(services.BuildServiceProvider()).");
}
