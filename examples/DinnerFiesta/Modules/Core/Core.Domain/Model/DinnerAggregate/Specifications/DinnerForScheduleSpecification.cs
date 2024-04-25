// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using System;
using System.Linq.Expressions;
using BridgingIT.DevKit.Domain.Specifications;

public class DinnerForScheduleSpecification : Specification<Dinner>
{
    private readonly HostId hostId;
    private readonly DinnerSchedule schedule;

    public DinnerForScheduleSpecification(HostId hostId, DinnerSchedule schedule)
    {
        this.hostId = hostId;
        this.schedule = schedule;
    }

    public override Expression<Func<Dinner, bool>> ToExpression()
    {
        return d => d.HostId == this.hostId &&
            ((d.Schedule.StartDateTime >= this.schedule.StartDateTime && d.Schedule.StartDateTime < this.schedule.EndDateTime) ||
            (d.Schedule.EndDateTime > this.schedule.StartDateTime && d.Schedule.EndDateTime <= this.schedule.EndDateTime));
    }
}