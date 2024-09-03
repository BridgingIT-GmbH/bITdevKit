namespace BridgingIT.DevKit.Domain;

using System.Linq;
using BridgingIT.DevKit.Common;

/// <summary>
/// This class represents a collection of domain policies that can be applied for a given context.
/// </summary>
public static class DomainPolicies
{
    public static async Task<DomainPolicyResult<TContext>> ApplyAsync<TContext>(
        TContext context,
        IDomainPolicy<TContext> policy,
        CancellationToken cancellationToken = default)
        => await ApplyAsync(context, [policy], DomainPolicyProcessingMode.ContinueOnPolicyFailure, cancellationToken);

    public static async Task<DomainPolicyResult<TContext>> ApplyAsync<TContext>(
        TContext context,
        IDomainPolicy<TContext>[] policies,
        CancellationToken cancellationToken = default)
        => await ApplyAsync(context, policies, DomainPolicyProcessingMode.ContinueOnPolicyFailure, cancellationToken);

    public static async Task<DomainPolicyResult<TContext>> ApplyAsync<TContext>(
        TContext context,
        IDomainPolicy<TContext>[] policies,
        DomainPolicyProcessingMode mode, CancellationToken cancellationToken = default)
    {
        var result = DomainPolicyResult<TContext>.Success();
        if (policies?.Length == 0)
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
            else if (result.IsFailure && mode == DomainPolicyProcessingMode.ThrowOnPolicyFailure)
            {
                throw new DomainPolicyException($"{policy.GetType().Name} policy failed", result);
            }
        }

        return result;
    }

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

    private static DomainPolicyResult<TContext> CombineResults<TContext>(
        DomainPolicyResult<TContext> result,
        Result applyResult)
        => (result.IsSuccess && applyResult.IsSuccess
            ? DomainPolicyResult<TContext>.Success()
            : DomainPolicyResult<TContext>.Failure())
            .WithMessages(result.Messages.Concat(applyResult.Messages))
            .WithErrors(result.Errors.Concat(applyResult.Errors))
            .WithPolicyResults(result.PolicyResults);
}