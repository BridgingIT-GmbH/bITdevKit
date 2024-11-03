// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public static partial class Rules
{
    /// <summary>
    /// Creates an empty rule builder.
    /// </summary>
    /// <returns>A new empty rule builder instance.</returns>
    public static RulesBuilder For()
    {
        return new RulesBuilder();
    }

    /// <summary>
    /// Creates a new rule builder starting with the specified rule.
    /// </summary>
    /// <param name="rule">The initial rule.</param>
    /// <returns>A new rule builder instance.</returns>
    public static RulesBuilder For(IRule rule)
    {
        return new RulesBuilder().Add(rule);
    }

    /// <summary>
    /// Creates a new rule builder starting with the specified rules.
    /// </summary>
    /// <param name="rules">The initial rules.</param>
    /// <returns>A new rule builder instance.</returns>
    public static RulesBuilder For(params IRule[] rules)
    {
        var builder = new RulesBuilder();
        foreach (var rule in rules)
        {
            builder.Add(rule);
        }

        return builder;
    }
}