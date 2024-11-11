// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

public class DinnerSchedule : ValueObject
{
    private DinnerSchedule() { }

    private DinnerSchedule(
        DateTimeOffset startDateTime,
        DateTimeOffset endDateTime)
    {
        this.StartDateTime = startDateTime;
        this.EndDateTime = endDateTime;
    }

    public DateTimeOffset StartDateTime { get; }

    public DateTimeOffset EndDateTime { get; }

    public static DinnerSchedule Create(
        DateTimeOffset startDateTime,
        DateTimeOffset endDateTime)
    {
        Rule.Add(DinnerRules.ScheduleShouldBeValid(startDateTime, endDateTime)).Check();

        return new DinnerSchedule(startDateTime, endDateTime);
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.StartDateTime;
        yield return this.EndDateTime;
    }
}