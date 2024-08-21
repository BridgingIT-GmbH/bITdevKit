// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Domain;

using System;
using System.Linq;
using BridgingIT.DevKit.Domain;

public class CountryShouldBeKnown(string value) : IDomainRule
{
    private readonly string[] countries = ["NL", "DE", "FR", "ES", "IT"];
    private readonly string value = value;

    public string Message => $"Country should be one of the following: {string.Join(", ", this.countries)}";

    public Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task<bool> ApplyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            !string.IsNullOrEmpty(this.value)
            && this.countries.Contains(this.value));
    }
}