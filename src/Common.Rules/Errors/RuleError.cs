// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Represents an unsatisfied rule as a result error using the rule's message.</summary>
/// <param name="rule">The rule that was not satisfied.</param>
[DebuggerDisplay("Rule={Rule.GetType().Name}, Message={Message}")]
public class RuleError(IRule rule) : ResultErrorBase(rule?.Message)
{
    /// <summary>Gets the rule that was not satisfied.</summary>
    public IRule Rule { get; } = rule;

    /// <summary>Throws a <see cref="RuleException"/> for the recorded rule.</summary>
    /// <exception cref="RuleException">Always thrown for <see cref="Rule"/>.</exception>
    public override void Throw()
    {
        throw new RuleException(this.Rule, string.Empty);
    }
}
