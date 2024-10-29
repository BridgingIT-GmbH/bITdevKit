// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

/// <summary>
///     Policies enforce business rules
///     and decision-making processes that dictate how operations or procedures are executed within a particular domain
///     context.
///     This interface ensures that policies can be checked for applicability and executed against a given context.
/// </summary>
/// <typeparam name="TContext">The type of the context to which the policy applies.</typeparam>
public interface IDomainPolicy<TContext>
{
    /// <summary>
    ///     Determines if the policy is enabled based on the provided context and cancellation token.
    /// </summary>
    /// <param name="context">The context in which the policy is applied.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to signal the operation should be canceled.</param>
    /// <returns>
    ///     Returns a Task that represents the asynchronous operation, containing a boolean value indicating whether the
    ///     policy is enabled.
    /// </returns>
    Task<bool> IsEnabledAsync(TContext context, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Applies the domain policy to the given context.
    /// </summary>
    /// <param name="context">The context in which the policy should be applied.</param>
    /// <param name="cancellationToken">An optional token used to observe cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation, containing the result of applying the policy.</returns>
    Task<IResult> ApplyAsync(TContext context, CancellationToken cancellationToken = default);
}