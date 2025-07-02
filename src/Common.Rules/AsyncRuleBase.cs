// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Base class for asynchronous rules that provides standard implementation.
/// </summary>
public abstract class AsyncRuleBase : IRule
{
    /// <summary>
    /// Gets the default message for the rule. Override this property to provide a specific message.
    /// </summary>
    public virtual string Message => "Rule not satisfied";

    /// <summary>
    /// Gets a value indicating whether the rule should be executed.
    /// Override this property to implement conditional rule execution.
    /// </summary>
    public virtual bool IsEnabled => true;

    /// <summary>
    /// Implements the core asynchronous validation logic for the rule.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the Result indicating success or failure of the rule.</returns>
    public abstract Task<Result> ExecuteAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Throws NotSupportedException as async rules must be executed asynchronously.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown as async rules don't support synchronous execution.</exception>
    public virtual Result IsSatisfied()
    {
        throw new NotSupportedException("This rule only supports async execution");
    }

    /// <summary>
    /// Applies the rule asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the Result indicating success or failure of the rule.</returns>
    public virtual async Task<Result> IsSatisfiedAsync(CancellationToken cancellationToken = default)
    {
        if (!this.IsEnabled)
        {
            return Result.Success();
        }

        return await this.ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }
}