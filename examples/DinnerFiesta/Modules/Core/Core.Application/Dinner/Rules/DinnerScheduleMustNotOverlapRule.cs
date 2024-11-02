// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

using Common;
using DevKit.Domain;
using DevKit.Domain.Repositories;
using Domain;

public class DinnerScheduleMustNotOverlapRule(
    IGenericRepository<Dinner> repository,
    HostId hostId,
    DinnerSchedule schedule) : AsyncDomainRuleBase
{
    public override string Message => "Dinners for same host cannot overlap";

    protected override async Task<Result> ExecuteRuleAsync(CancellationToken cancellationToken)
    {
        var dinners = await repository.FindAllAsync(DinnerSpecifications.ForSchedule(hostId, schedule),
            cancellationToken: cancellationToken);

        return Result.SuccessIf(!dinners.SafeAny());
    }
}