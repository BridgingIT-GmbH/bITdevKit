// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

/// <summary>
/// Base class for asynchronous domain rules that provides standard implementation and error handling.
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
    /// <returns>A task containing the <see cref="Result"/> indicating success or failure of the rule.</returns>
    protected abstract Task<Result> ExecuteRuleAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Throws NotSupportedException as async rules must be executed asynchronously.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown as async rules don't support synchronous execution.</exception>
    public Result Apply()
    {
        throw new NotSupportedException("This rule only supports async execution");
    }

    /// <summary>
    /// Applies the rule asynchronously with proper error handling and cancellation support.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task containing the <see cref="Result"/> indicating success or failure of the rule.</returns>
    public async Task<Result> ApplyAsync(CancellationToken cancellationToken = default)
    {
        if (!this.IsEnabled)
        {
            return Result.Success();
        }

        try
        {
            return await this.ExecuteRuleAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) // TODO: maybe don't hide this inside an error?
        {
            return Result.Failure().WithError(new OperationCancelledError(this.GetType().Name));
        }
        catch (Exception ex) when (ex is not AggregateException)
        {
            return Result.Failure().WithError(new ExceptionError(ex));
        }
        catch (AggregateException ex)
        {
            var innerEx = ex.InnerExceptions.FirstOrDefault() ?? ex;
            if (innerEx is OperationCanceledException)
            {
                return Result.Failure().WithError(new OperationCancelledError(this.GetType().Name));
            }

            return Result.Failure().WithError(new ExceptionError(ex));
        }
    }
}