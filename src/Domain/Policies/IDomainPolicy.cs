namespace BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Common;

/// <summary>
/// A policy is a business rule or decision-making logic that governs how
/// certain operations or processes should be carried out within a specific domain.
/// Policies encapsulate important business decisions and help ensure that
/// the system behaves consistently according to the defined rules of the domain.
/// </summary>
public interface IDomainPolicy<TContext>
{
    Task<bool> IsEnabledAsync(TContext context, CancellationToken cancellationToken = default);

    Task<Result> ApplyAsync(TContext context, CancellationToken cancellationToken = default);
}