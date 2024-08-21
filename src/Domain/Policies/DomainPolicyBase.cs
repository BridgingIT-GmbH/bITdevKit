namespace BridgingIT.DevKit.Domain;

using BridgingIT.DevKit.Common;

public abstract class DomainPolicyBase<TContext> : IDomainPolicy<TContext>
{
    public virtual Task<bool> IsEnabledAsync(TContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public abstract Task<Result> ApplyAsync(TContext context, CancellationToken cancellationToken = default);
}