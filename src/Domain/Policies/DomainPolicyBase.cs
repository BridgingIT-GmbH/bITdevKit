// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

/// <summary>
///     Base class for implementing domain-specific policies. These policies enforce business rules
///     and decision-making processes that dictate how operations or procedures are executed within
///     a particular domain context. This base class provides default implementations for checking
///     whether the policy is enabled and an abstract method for applying the policy.
/// </summary>
/// <typeparam name="TContext">The type of the context to which the policy applies.</typeparam>
public abstract class DomainPolicyBase<TContext> : IDomainPolicy<TContext>
{
    /// <summary>
    ///     Determines if the specified context meets the criteria to enable the current policy asynchronously.
    /// </summary>
    /// <param name="context">The context to evaluate the policy against.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains a boolean value indicating
    ///     whether the policy is enabled for the given context.
    /// </returns>
    public virtual Task<bool> IsEnabledAsync(TContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    /// <summary>
    ///     Applies the domain policy asynchronously.
    /// </summary>
    /// <param name="context">The context in which the policy is to be applied.</param>
    /// <param name="cancellationToken">A token for cancelling the operation.</param>
    /// <returns>
    ///     A task representing the asynchronous operation, with a result indicating the success or failure of applying
    ///     the policy.
    /// </returns>
    public abstract Task<Result> ApplyAsync(TContext context, CancellationToken cancellationToken = default);
}