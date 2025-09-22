// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public abstract partial class ActiveEntity<TEntity, TId> : Entity<TId> // Transaction support
    where TEntity : ActiveEntity<TEntity, TId>
{
    /// <summary>
    /// Executes a function with a scoped <see cref="ActiveEntityContext{TEntity, TId}"/>
    /// for the current entity type.
    /// <para>
    /// This method creates a new DI scope, resolves the provider and behaviors for the entity,
    /// wraps them in an <see cref="ActiveEntityContext{TEntity, TId}"/>, and passes it into the given
    /// <paramref name="action"/> delegate.
    /// </para>
    /// </summary>
    /// <typeparam name="TResult">The result type returned by the action.</typeparam>
    /// <param name="action">
    /// A function that receives the resolved <see cref="ActiveEntityContext{TEntity, TId}"/>
    /// and returns a <see cref="Task{TResult}"/>.
    /// </param>
    /// <returns>The result of the executed action.</returns>
    /// <remarks>
    /// Use this overload when you only need access to the provider and behaviors wrapped in a context.
    /// </remarks>
    /// <example>
    /// Example: Custom finder defined as a static method on the entity:
    /// <code>
    /// public class Customer : ActiveEntity&lt;Customer, CustomerId&gt;
    /// {
    ///     public string FirstName { get; set; }
    ///     public string LastName { get; set; }
    ///
    ///     public static Task&lt;Result&lt;IEnumerable&lt;Customer&gt;&gt;&gt; FindAllByLastNameAsync(string lastName) =>
    ///         WithContextAsync(ctx =>
    ///             ctx.Provider.FindAllAsync(new Specification&lt;Customer&gt;(c => c.LastName == lastName)));
    /// }
    ///
    /// // Usage:
    /// var does = await Customer.FindAllByLastNameAsync("Doe");
    /// </code>
    /// </example>
    public static Task<TResult> WithContextAsync<TResult>(
        Func<ActiveEntityContext<TEntity, TId>, Task<TResult>> action)
    {
        return ActiveEntityContextScope.UseAsync<TEntity, TId, TResult>(
            null, // always create a fresh context
            async ctx => await action(ctx));
    }

    /// <summary>
    /// Executes a function with a scoped <see cref="ActiveEntityContext{TEntity, TId}"/>
    /// and exposes both the provider and all registered <see cref="IActiveEntityBehavior{TEntity}"/> instances
    /// for the current entity type.
    /// <para>
    /// This method creates a new DI scope, resolves both the provider and behaviors,
    /// wraps them in an <see cref="ActiveEntityContext{TEntity, TId}"/>, and passes them into the given
    /// <paramref name="action"/> delegate.
    /// </para>
    /// </summary>
    /// <typeparam name="TResult">The result type returned by the action.</typeparam>
    /// <param name="action">
    /// A function that receives the resolved <see cref="IActiveEntityEntityProvider{TEntity, TId}"/>
    /// and the collection of <see cref="IActiveEntityBehavior{TEntity}"/>,
    /// and returns a <see cref="Task{TResult}"/>.
    /// </param>
    /// <returns>The result of the executed action.</returns>
    /// <remarks>
    /// Use this overload when you need access to both the provider and the behaviors
    /// (e.g., to manually invoke or inspect behaviors in addition to provider operations).
    /// </remarks>
    /// <example>
    /// Example: Custom finder that also inspects behaviors:
    /// <code>
    /// public static class CustomerQueryExtensions
    /// {
    ///     public static Task&lt;Result&lt;IEnumerable&lt;Customer&gt;&gt;&gt; FindAllActiveByLastNameAsync(string lastName)
    ///     {
    ///         return Customer.WithContextAsync(async (provider, behaviors) =>
    ///         {
    ///             // Example: log via a custom behavior before executing
    ///             foreach (var behavior in behaviors)
    ///             {
    ///                 await behavior.BeforeUpdateAsync(
    ///                     new Customer { LastName = lastName }, CancellationToken.None);
    ///             }
    ///
    ///             return await provider.FindAllAsync(
    ///                 new Specification&lt;Customer&gt;(c => c.LastName == lastName &amp;&amp; c.IsActive));
    ///         });
    ///     }
    /// }
    ///
    /// // Usage:
    /// var activeDoes = await Customer.FindAllActiveByLastNameAsync("Doe");
    /// </code>
    /// </example>
    public static Task<TResult> WithContextAsync<TResult>(
        Func<IActiveEntityEntityProvider<TEntity, TId>, IEnumerable<IActiveEntityBehavior<TEntity>>, Task<TResult>> action)
    {
        return ActiveEntityContextScope.UseAsync<TEntity, TId, TResult>(
            null, // always create a fresh context
            async ctx => await action(ctx.Provider, ctx.Behaviors));
    }

    /// <summary>
    /// Executes an action within a transaction, committing if successful or rolling back if it fails.
    /// </summary>
    /// <param name="action">The action to execute within the transaction, using the <see cref="ActiveEntityContext{TEntity, TId}"/>.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result indicating the success or failure of the transaction.</returns>
    /// <example>
    /// <code>
    /// var result = await Customer.WithTransactionAsync(async ctx =>
    /// {
    ///     var customer = new Customer { FirstName = "John", LastName = "Doe" };
    ///     return await customer.InsertAsync(ctx);
    /// });
    /// if (result.IsSuccess) { Console.WriteLine("Transaction committed"); }
    /// </code>
    /// </example>
    public static Task<Result> WithTransactionAsync(
        Func<ActiveEntityContext<TEntity, TId>, Task<Result>> action,
        CancellationToken cancellationToken = default)
    {
        if (action == null)
        {
            return Task.FromResult(Result.Failure("Action cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync<TEntity, TId, Result>(
            null, // always create a fresh context for a transaction
            async ctx =>
            {
                var provider = ctx.Provider;

                // Begin transaction
                var transactionResult = await provider.BeginTransactionAsync(cancellationToken).AnyContext();
                if (!transactionResult.IsSuccess)
                {
                    return transactionResult.WithMessage("Transaction failed to start.");
                }

                var result = Result.Success().WithMessage("Transaction execution started.");

                try
                {
                    await transactionResult.Value.ExecuteScopedAsync( // Execute the action inside the transaction
                        async () => result = await action(ctx),
                        cancellationToken);

                    if (result.IsSuccess)
                    {
                        var commitResult = await provider.CommitTransactionAsync(cancellationToken).AnyContext();
                        if (!commitResult.IsSuccess)
                        {
                            return Result.Merge(result, transactionResult, commitResult).WithMessage("Transaction execution failed.");
                        }

                        result = Result.Merge(result, transactionResult, commitResult).WithMessage("Transaction execution finished.");
                    }
                    else
                    {
                        var rollbackResult = await provider.RollbackAsync(cancellationToken).AnyContext();
                        return !rollbackResult.IsSuccess
                            ? Result.Merge(Result.Failure("Transaction execution and rollback failed."),
                                result, transactionResult, rollbackResult)
                            : Result.Merge(Result.Failure("Transaction execution failed and has been rolled back."),
                                result, transactionResult, rollbackResult);
                    }
                }
                catch (Exception ex) when (!ex.IsTransientException())
                {
                    var rollbackResult = await provider.RollbackAsync(cancellationToken).AnyContext();
                    return !rollbackResult.IsSuccess
                        ? Result.Merge(Result.Failure("Transaction execution and rollback failed.").WithError(ex),
                            result, transactionResult, rollbackResult)
                        : Result.Merge(Result.Failure("Transaction execution failed and has been rolled back.").WithError(ex),
                            result, transactionResult, rollbackResult);
                }

                return result;
            });
    }

    /// <summary>
    /// Executes an action within a transaction, committing if successful or rolling back if it fails.
    /// Returns a <see cref="Result{T}"/> containing a value if successful.
    /// </summary>
    /// <typeparam name="T">The type of the value returned by the action.</typeparam>
    /// <param name="action">
    /// The action to execute within the transaction, using the <see cref="ActiveEntityContext{TEntity, TId}"/>.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task with a <see cref="Result{T}"/> indicating the success or failure of the transaction,
    /// and containing the value returned by the action if successful.
    /// </returns>
    /// <example>
    /// <code>
    /// var result = await Customer.WithTransactionAsync(async ctx =>
    /// {
    ///     var customer = new Customer { FirstName = "Jane", LastName = "Smith" };
    ///     var insertResult = await customer.InsertAsync(ctx);
    ///     if (insertResult.IsFailure) return Result<Customer>.Failure(insertResult.Errors);
    ///     return Result.Success(insertResult.Value);
    /// });
    /// if (result.IsSuccess) { Console.WriteLine($"Inserted customer with ID: {result.Value.Id}"); }
    /// </code>
    /// </example>
    public static Task<Result<T>> WithTransactionAsync<T>(
        Func<ActiveEntityContext<TEntity, TId>, Task<Result<T>>> action,
        CancellationToken cancellationToken = default)
    {
        if (action == null)
        {
            return Task.FromResult(Result<T>.Failure("Action cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync<TEntity, TId, Result<T>>(
            null, // always create a fresh context for a transaction
            async ctx =>
            {
                var provider = ctx.Provider;

                // Begin transaction
                var transactionResult = await provider.BeginTransactionAsync(cancellationToken).AnyContext();
                if (!transactionResult.IsSuccess)
                {
                    return Result<T>.Merge(Result<T>.Failure("Transaction failed to start."), transactionResult.Unwrap());
                }

                var result = Result<T>.Success(default); // Initialize with default(T) and success state

                try
                {
                    // Execute the action inside the transaction
                    await transactionResult.Value.ExecuteScopedAsync(
                        async () => result = await action(ctx), cancellationToken);

                    if (result.IsSuccess)
                    {
                        var commitResult = await provider.CommitTransactionAsync(cancellationToken).AnyContext();
                        if (!commitResult.IsSuccess)
                        {
                            return Result<T>.Merge(result, transactionResult.Unwrap(), commitResult).WithMessage("Transaction execution failed.");
                        }

                        result = Result<T>.Merge(result, transactionResult.Unwrap(), commitResult).WithMessage("Transaction execution finished.");
                    }
                    else
                    {
                        var rollbackResult = await provider.RollbackAsync(cancellationToken).AnyContext();
                        return !rollbackResult.IsSuccess
                            ? Result<T>.Merge(Result<T>.Failure("Transaction execution and rollback failed."),
                                result, transactionResult.Unwrap(), rollbackResult)
                            : Result<T>.Merge(Result<T>.Failure("Transaction execution failed and has been rolled back."),
                                result, transactionResult.Unwrap(), rollbackResult);
                    }
                }
                catch (Exception ex) when (!ex.IsTransientException())
                {
                    var rollbackResult = await provider.RollbackAsync(cancellationToken).AnyContext();
                    return !rollbackResult.IsSuccess
                        ? Result<T>.Merge(Result<T>.Failure("Transaction execution and rollback failed.").WithError(ex),
                            result, transactionResult.Unwrap(), rollbackResult)
                        : Result<T>.Merge(Result<T>.Failure("Transaction execution failed and has been rolled back.").WithError(ex),
                            result, transactionResult.Unwrap(), rollbackResult);
                }

                return result;
            });
    }
}