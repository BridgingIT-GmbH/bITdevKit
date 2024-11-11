// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Linq.Expressions;

/// <summary>
/// Provides extension methods for the rule builder.
/// </summary>
public static class RuleBuilderExtensions
{
    public static RuleBuilder Add(this RuleBuilder builder, Func<CancellationToken, Task<bool>> predicate, string message = null)
    {
        return builder.Add(new AsyncFuncRule(predicate, message));
    }

    public static RuleBuilder Add<T>(this RuleBuilder builder, Func<T, IRule> ruleFactory)
    {
        return builder.Add(new ItemRule<T>(ruleFactory));
    }

    public static RuleBuilder And<T>(this RuleBuilder builder, Func<T, IRule> ruleFactory)
    {
        return builder.Add(ruleFactory);
    }
}

public class ItemRule<T>(Func<T, IRule> ruleFactory) : RuleBase
{
    private T item;

    internal void SetItem(T item)
    {
        this.item = item;
    }

    protected override Result Execute() =>
        ruleFactory(this.item).IsSatisfied();

    public override Task<Result> IsSatisfiedAsync(CancellationToken cancellationToken = default) =>
        ruleFactory(this.item).IsSatisfiedAsync(cancellationToken);
}