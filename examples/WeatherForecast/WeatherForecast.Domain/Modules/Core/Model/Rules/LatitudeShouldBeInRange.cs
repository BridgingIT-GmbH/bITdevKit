// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Domain;

using DevKit.Domain;

public class LatitudeShouldBeInRange : IDomainRule
{
    private readonly double? value;

    public LatitudeShouldBeInRange(double? value)
    {
        this.value = value;
    }

    public LatitudeShouldBeInRange(double value)
    {
        this.value = value;
    }

    public string Message => "Latitude should be between -90 and 90";

    public Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task<bool> ApplyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(this.value.HasValue && this.value >= -180 && this.value <= 180);
    }
}