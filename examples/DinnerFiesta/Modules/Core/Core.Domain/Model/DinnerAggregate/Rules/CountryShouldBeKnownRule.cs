// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using System;
using System.Linq;
using BridgingIT.DevKit.Domain;

public class CountryShouldBeKnownRule(string value) : IBusinessRule
{
    private readonly string[] countries = ["NL", "DE", "FR", "ES", "IT", "USA"];
    private readonly string value = value;

    public string Message => $"Country should be one of the following: {string.Join(", ", this.countries)}";

    public Task<bool> IsSatisfiedAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(!string.IsNullOrEmpty(this.value)
            && this.countries.Contains(this.value));
    }
}

public static partial class DinnerRules
{
    public static IBusinessRule CountryShouldBeKnown(string value) => new CountryShouldBeKnownRule(value);
}