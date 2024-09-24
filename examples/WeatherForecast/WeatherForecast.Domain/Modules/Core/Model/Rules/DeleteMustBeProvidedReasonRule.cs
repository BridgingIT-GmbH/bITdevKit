// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Domain;

using DevKit.Domain;

public class DeleteMustBeProvidedReasonRule(string reason) : IDomainRule
{
    private readonly string reason = reason;

    public string Message => "Reason of deleting a must be provided";

    public Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task<bool> ApplyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(!string.IsNullOrEmpty(this.reason));
    }
}