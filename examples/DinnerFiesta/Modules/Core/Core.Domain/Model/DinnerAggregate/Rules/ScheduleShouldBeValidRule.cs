// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using System;
using BridgingIT.DevKit.Domain;

public class ScheduleShouldBeValidRule : IBusinessRule
{
    private readonly DateTimeOffset startDateTime;
    private readonly DateTimeOffset endDateTime;

    public ScheduleShouldBeValidRule(DateTimeOffset startDateTime, DateTimeOffset endDateTime)
    {
        this.startDateTime = startDateTime;
        this.endDateTime = endDateTime;
    }

    public string Message => "StartDate should be earlier than the EndDate";

    public Task<bool> IsSatisfiedAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(this.startDateTime < this.endDateTime);
    }
}