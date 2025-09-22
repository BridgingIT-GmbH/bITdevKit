// MIT-License ...
namespace BridgingIT.DevKit.Domain;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Manages execution of work that requires an ActiveEntityContext, creating a scoped context
/// when one is not supplied and disposing its DI scope after use.
/// </summary>
public static class ActiveEntityContextScope
{
    /// <summary>
    /// Executes an action with an existing or newly created <see cref="ActiveEntityContext{TEntity,TId}"/>.
    /// Creates a new DI scope only when <paramref name="context"/> is null and disposes it after execution.
    /// </summary>
    /// <typeparam name="TEntity">Entity type.</typeparam>
    /// <typeparam name="TId">Identifier type.</typeparam>
    /// <typeparam name="TResult">Result type returned by the action.</typeparam>
    /// <param name="context">Existing context (reused) or null to create a scoped one.</param>
    /// <param name="action">Delegate to execute with the ensured context.</param>
    /// <returns>The delegate result.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="action"/> is null.</exception>
    public static async Task<TResult> UseAsync<TEntity, TId, TResult>(
        ActiveEntityContext<TEntity, TId> context,
        Func<ActiveEntityContext<TEntity, TId>, Task<TResult>> action)
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

            return await action(context);
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