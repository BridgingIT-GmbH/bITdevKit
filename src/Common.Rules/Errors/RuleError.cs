// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

[DebuggerDisplay("Rule={Rule.GetType().Name}, Message={Message}")]
public class RuleError(IRule rule) : ResultErrorBase(rule?.Message)
{
    public IRule Rule { get; } = rule;

    public override void Throw()
    {
        throw new RuleException(this.Rule, string.Empty);
    }
}