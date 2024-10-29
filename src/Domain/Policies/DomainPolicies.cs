// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

/// <summary>
///     Provides methods to apply domain policies asynchronously on a given context.
/// </summary>
public static class DomainPolicies
{
    /// <summary>
    ///     Applies domain policies to the given context asynchronously.
    /// </summary>
    /// <typeparam name="TContext">The type of the context to which the policies will be applied.</typeparam>
    /// <param name="context">The context to which the policies are applied.</param>
    /// <param name="policy">The domain policy to apply.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the domain policy result for the
    ///     provided context.
    /// </returns>
    public static async Task<DomainPolicyResult<TContext>> ApplyAsync<TContext>(
        TContext context,
        IDomainPolicy<TContext> policy,
        CancellationToken cancellationToken = default)
    {
        return await ApplyAsync(context,
            [policy],
            DomainPolicyProcessingMode.ContinueOnPolicyFailure,
            cancellationToken);
    }

    /// <summary>
    ///     Applies a set of domain policies to the specified context asynchronously.
    /// </summary>
    /// <typeparam name="TContext">The type of the context to which the policies will be applied.</typeparam>
    /// <param name="context">The context to which the domain policies will be applied.</param>
    /// <param name="policies">An array of domain policies to be applied to the context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation, containing a DomainPolicyResult object.</returns>
    public static async Task<DomainPolicyResult<TContext>> ApplyAsync<TContext>(
        TContext context,
        IDomainPolicy<TContext>[] policies,
        CancellationToken cancellationToken = default)
    {
        return await ApplyAsync(context,
            policies,
            DomainPolicyProcessingMode.ContinueOnPolicyFailure,
            cancellationToken);
    }

    /// <summary>
    ///     Applies the specified domain policies to a given context asynchronously.
    /// </summary>
    /// <typeparam name="TContext">The type of the context to which the policies are applied.</typeparam>
    /// <param name="context">The context to which the policies will be applied.</param>
    /// <param name="policies">An array of domain policies to apply.</param>
    /// <param name="mode">
    ///     The mode in which the policy processing will be executed:
    ///     ContinueOnPolicyFailure, StopOnPolicyFailure, or ThrowOnPolicyFailure.
    /// </param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the domain policy result of the
    ///     applied policies.
    /// </returns>
    public static async Task<DomainPolicyResult<TContext>> ApplyAsync<TContext>(
        TContext context,
        IDomainPolicy<TContext>[] policies,
        DomainPolicyProcessingMode mode,
        CancellationToken cancellationToken = default)
    {
        var result = DomainPolicyResult<TContext>.Success();
        if (policies == null || policies.Length == 0)
        {
            return result;
        }

        foreach (var policy in policies)
        {
            if (!await policy.IsEnabledAsync(context, cancellationToken).AnyContext())
            {
                continue; // skip this policy, does not apply to context
            }

            result = await ApplyAsync(context, result, policy, cancellationToken);

            if (result.IsFailure && mode == DomainPolicyProcessingMode.StopOnPolicyFailure)
            {
                return result;
            }

            if (result.IsFailure && mode == DomainPolicyProcessingMode.ThrowOnPolicyFailure)
            {
                throw new DomainPolicyException($"{policy.GetType().Name} policy failed", result);
            }
        }

        return result;
    }

    /// <summary>
    ///     Applies a given domain policy asynchronously to a specified context.
    /// </summary>
    /// <typeparam name="TContext">The type of the context to which the policy is to be applied.</typeparam>
    /// <param name="context">The context to which the policy is to be applied.</param>
    /// <param name="policy">The domain policy to be applied.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    ///     A task representing the asynchronous operation. The task result contains the outcome of the domain policy
    ///     application.
    /// </returns>
    private static async Task<DomainPolicyResult<TContext>> ApplyAsync<TContext>(
        TContext context,
        DomainPolicyResult<TContext> result,
        IDomainPolicy<TContext> policy,
        CancellationToken cancellationToken)
    {
        var applyResult = await policy.ApplyAsync(context, cancellationToken);
        result = CombineResults(result, applyResult);
        result.PolicyResults.AddValue(policy.GetType(), applyResult.GetValue());

        return result;
    }

    /// <summary>
    ///     Combines the results of two domain policy applications into a single result.
    /// </summary>
    /// <typeparam name="TContext">The type of the context used in the domain policy evaluation.</typeparam>
    /// <param name="result">The original result of the domain policy application.</param>
    /// <param name="applyResult">The newly applied domain policy result to be combined.</param>
    /// <returns>
    ///     A combined DomainPolicyResult object that aggregates the success, messages, errors, and policy results of both
    ///     input results.
    /// </returns>
    private static DomainPolicyResult<TContext> CombineResults<TContext>(
        DomainPolicyResult<TContext> result,
        IResult applyResult)
    {
        return (result.IsSuccess && applyResult.IsSuccess
                ? DomainPolicyResult<TContext>.Success()
                : DomainPolicyResult<TContext>.Failure()).WithMessages(result.Messages.Concat(applyResult.Messages))
            .WithErrors(result.Errors.Concat(applyResult.Errors))
            .WithPolicyResults(result.PolicyResults);
    }
}