// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public class FuncRule(Func<bool> predicate, string message = "Predicate rule not satisfied") : RuleBase
{
    public override string Message { get; } = message;

    protected override Result ExecuteRule() =>
        Result.SuccessIf(predicate());
}