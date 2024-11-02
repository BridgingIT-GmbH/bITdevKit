// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

/// <summary>
/// Base class for synchronous domain rules that provides standard implementation and error handling.
/// </summary>
public abstract class DomainRuleBase : IDomainRule
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
    /// Implements the core validation logic for the rule.
    /// </summary>
    /// <returns>A <see cref="Result"/> indicating success or failure of the rule.</returns>
    protected abstract Result ExecuteRule();

    /// <summary>
    /// Applies the rule with proper error handling and disabled state checking.
    /// </summary>
    /// <returns>A <see cref="Result"/> indicating success or failure of the rule.</returns>
    public Result Apply()
    {
        if (!this.IsEnabled)
        {
            return Result.Success();
        }

        try
        {
            return this.ExecuteRule();
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new DomainRuleError(this.GetType().Name, ex.Message));
        }
    }

    /// <summary>
    /// Provides an async-compatible wrapper around the synchronous rule execution.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation (not used in sync rules).</param>
    /// <returns>A task containing the result of the rule execution.</returns>
    public Task<Result> ApplyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(this.Apply());
    }
}