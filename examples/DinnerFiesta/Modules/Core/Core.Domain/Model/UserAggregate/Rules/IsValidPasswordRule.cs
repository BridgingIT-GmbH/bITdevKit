// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

public class IsValidPasswordRule(string password) : RuleBase
{
    private readonly string password = password;

    public override string Message => "Not a valid password";

    public override Result Execute()
    {
        return Result.SuccessIf(!string.IsNullOrEmpty(this.password)); // TODO: implement
    }
}

public static class UserRules
{
    public static IRule IsValidPassword(string password)
    {
        return new IsValidPasswordRule(password);
    }
}