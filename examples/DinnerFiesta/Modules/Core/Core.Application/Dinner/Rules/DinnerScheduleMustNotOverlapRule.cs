// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

public class DinnerScheduleMustNotOverlapRule(IGenericRepository<Dinner> repository, HostId hostId, DinnerSchedule schedule) : IDomainRule
{
    public string Message => "Dinners for same host cannot overlap";

    public Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public async Task<bool> ApplyAsync(CancellationToken cancellationToken = default)
    {
        var dinners = await repository.FindAllAsync(
            DinnerSpecifications.ForSchedule(hostId, schedule),
            cancellationToken: cancellationToken);

        return !dinners.SafeAny();
    }
}