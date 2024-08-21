// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;

public class DinnerSchedule : ValueObject
{
    private DinnerSchedule()
    {
    }

    private DinnerSchedule(
        DateTimeOffset startDateTime,
        DateTimeOffset endDateTime)
    {
        this.StartDateTime = startDateTime;
        this.EndDateTime = endDateTime;
    }

    public DateTimeOffset StartDateTime { get; private set; }

    public DateTimeOffset EndDateTime { get; private set; }

    public static DinnerSchedule Create(
        DateTimeOffset startDateTime,
        DateTimeOffset endDateTime)
    {
        DomainRules.Apply(
        [
            DinnerRules.ScheduleShouldBeValid(startDateTime, endDateTime),
        ]);

        return new DinnerSchedule(startDateTime, endDateTime);
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.StartDateTime;
        yield return this.EndDateTime;
    }
}