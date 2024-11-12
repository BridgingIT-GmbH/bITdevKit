// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a rule that can be applied to an item of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of the item.</typeparam>
public class ItemRule<T>(Func<T, IRule> ruleFactory) : RuleBase, IItemRule<T>
{
    /// <summary>
    /// Represents the item to which the rule will be applied. It is of a generic type T.
    /// </summary>
    private T item;

    /// <summary>
    /// Sets the item to be validated according to the rule.
    /// </summary>
    /// <param name="item">The item to be validated.</param>
    public void SetItem(T item)
    {
        this.item = item;
    }

    /// <summary>
    /// Executes the rule associated with the current item.
    /// </summary>
    /// <returns>
    /// A <see cref="Result"/> representing whether the rule is satisfied.
    /// </returns>
    protected override Result Execute() =>
        ruleFactory(this.item).IsSatisfied();

    /// <summary>
    /// Asynchronously evaluates whether the rule is satisfied.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Result"/> indicating the outcome of the rule evaluation.</returns>
    public override Task<Result> IsSatisfiedAsync(CancellationToken cancellationToken = default) =>
        ruleFactory(this.item).IsSatisfiedAsync(cancellationToken);
}