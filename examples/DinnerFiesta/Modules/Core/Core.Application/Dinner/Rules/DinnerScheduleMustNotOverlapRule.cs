// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

public class DinnerScheduleMustNotOverlapRule(
    IGenericRepository<Dinner> repository,
    HostId hostId,
    DinnerSchedule schedule) : AsyncRuleBase
{
    public override string Message => "Dinners for same host cannot overlap";

    public override async Task<Result> ExecuteAsync(CancellationToken cancellationToken)
    {
        var dinners = await repository.FindAllAsync(DinnerSpecifications.ForSchedule(hostId, schedule),
            cancellationToken: cancellationToken);

        return Result.SuccessIf(!dinners.SafeAny());
    }
}