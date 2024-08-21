// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Domain;

using BridgingIT.DevKit.Domain;

public class LongitudeShouldBeInRange : IDomainRule
{
    private readonly double? value;

    public LongitudeShouldBeInRange(double? value)
    {
        this.value = value;
    }

    public LongitudeShouldBeInRange(double value)
    {
        this.value = value;
    }

    public string Message => "Longitude should be between -180 and 180";

    public Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task<bool> ApplyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(this.value.HasValue && this.value >= -180 && this.value <= 180);
    }
}
