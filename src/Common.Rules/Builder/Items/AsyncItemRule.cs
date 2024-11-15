// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an asynchronous rule for a specific item type.
/// </summary>
/// <typeparam name="T">The type of the item to validate.</typeparam>
public class AsyncItemRule<T>(Func<T, CancellationToken, Task<IRule>> ruleFactory) : AsyncRuleBase, IItemRule<T>
{
    /// <summary>
    /// The specific item to be validated by the asynchronous rule.
    /// </summary>
    private T item;

    /// <summary>
    /// Sets the item to be validated by the rule.
    /// </summary>
    /// <param name="item">The item of type <typeparamref name="T"/> to be set for the rule.</param>
    public void SetItem(T item)
    {
        this.item = item;
    }

    /// <summary>
    /// Executes the asynchronous rule.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Result"/> indicating the outcome of the rule execution.</returns>
    protected override async Task<Result> ExecuteAsync(CancellationToken cancellationToken)
    {
        var rule = await ruleFactory(this.item, cancellationToken);
        return await rule.IsSatisfiedAsync(cancellationToken);
    }
}