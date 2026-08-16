// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Represents an exception raised while evaluating a rule as a result error.</summary>
/// <param name="rule">The rule whose evaluation raised an exception.</param>
/// <param name="exception">The exception raised by rule evaluation.</param>
[DebuggerDisplay("Rule={Rule.GetType().Name}, Message={Message}")]
public class RuleExceptionError(IRule rule, Exception exception) : ResultErrorBase(rule?.Message)
{
    /// <summary>Gets the rule whose evaluation raised an exception.</summary>
    public IRule Rule { get; } = rule;

    /// <summary>Gets the exception raised during rule evaluation.</summary>
    public Exception Exception { get; } = exception;

    /// <summary>Throws a <see cref="RuleException"/> for the recorded rule.</summary>
    /// <remarks>The recorded <see cref="Exception"/> remains available as metadata but is not attached as the thrown exception's inner exception.</remarks>
    /// <exception cref="RuleException">Always thrown for <see cref="Rule"/>.</exception>
    public override void Throw()
    {
        throw new RuleException(this.Rule, string.Empty);
    }
}
