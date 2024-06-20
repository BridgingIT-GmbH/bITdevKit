// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Domain;

using BridgingIT.DevKit.Domain;

public class DeleteCannotBeDoneTwiceRule(bool isDeleted) : IBusinessRule
{
    private readonly bool isDeleted = isDeleted;

    public string Message => "Deleting can only be done once";

    public Task<bool> IsSatisfiedAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(!this.isDeleted);
    }
}
