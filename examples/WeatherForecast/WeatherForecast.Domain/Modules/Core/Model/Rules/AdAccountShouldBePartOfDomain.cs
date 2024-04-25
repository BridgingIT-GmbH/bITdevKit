// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Domain;

using BridgingIT.DevKit.Domain;

public class AdAccountShouldBePartOfDomain : IBusinessRule
{
    private readonly string value;

    public AdAccountShouldBePartOfDomain(string value)
    {
        this.value = value;
    }

    public string Message => "AD Account should be part of a domain";

    public Task<bool> IsSatisfiedAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(this.value.Contains("\\"));
    }
}