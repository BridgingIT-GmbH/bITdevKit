// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using DevKit.Domain;

public class ScheduleShouldBeValidRule(DateTimeOffset startDateTime, DateTimeOffset endDateTime) : IDomainRule
{
    private readonly DateTimeOffset startDateTime = startDateTime;
    private readonly DateTimeOffset endDateTime = endDateTime;

    public string Message => "StartDate should be earlier than the EndDate";

    public Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task<bool> ApplyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(this.startDateTime < this.endDateTime);
    }
}

public static partial class DinnerRules
{
    public static IDomainRule ScheduleShouldBeValid(DateTimeOffset startDateTime, DateTimeOffset endDateTime)
    {
        return new ScheduleShouldBeValidRule(startDateTime, endDateTime);
    }
}