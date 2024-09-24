// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using DevKit.Domain;

public class CountryShouldBeKnownRule(string value) : IDomainRule
{
    private readonly string[] countries = ["NL", "DE", "FR", "ES", "IT", "USA"];
    private readonly string value = value;

    public string Message => $"Country should be one of the following: {string.Join(", ", this.countries)}";

    public Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task<bool> ApplyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(!string.IsNullOrEmpty(this.value) && this.countries.Contains(this.value));
    }
}

public static partial class DinnerRules
{
    public static IDomainRule CountryShouldBeKnown(string value)
    {
        return new CountryShouldBeKnownRule(value);
    }
}