// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using BridgingIT.DevKit.Common;
using DevKit.Domain;

public class IsValidPasswordRule(string password) : DomainRuleBase
{
    private readonly string password = password;

    public override string Message => "Not a valid password";

    protected override Result ExecuteRule()
    {
        return Result.SuccessIf(!string.IsNullOrEmpty(this.password)); // TODO: implement
    }
}

public static class UserRules
{
    public static IDomainRule IsValidPassword(string password)
    {
        return new IsValidPasswordRule(password);
    }
}