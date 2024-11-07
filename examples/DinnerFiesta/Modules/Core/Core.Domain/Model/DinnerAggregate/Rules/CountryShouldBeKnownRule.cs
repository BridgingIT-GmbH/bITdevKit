// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

public class CountryShouldBeKnownRule(string value) : RuleBase
{
    private readonly string[] countries = ["NL", "DE", "FR", "ES", "IT", "USA"];

    public override string Message => $"Country should be one of the following: {string.Join(", ", this.countries)}";

    protected override Result Execute()
    {
        return Result.SuccessIf(!string.IsNullOrEmpty(value) && this.countries.Contains(value));
    }
}

public static partial class DinnerRules
{
    public static IRule CountryShouldBeKnown(string value)
    {
        return new CountryShouldBeKnownRule(value);
    }
}