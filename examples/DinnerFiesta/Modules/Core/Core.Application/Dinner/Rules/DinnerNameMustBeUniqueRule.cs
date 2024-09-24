// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

using Common;
using DevKit.Domain;
using DevKit.Domain.Repositories;
using Domain;

public class DinnerNameMustBeUniqueRule(IGenericRepository<Dinner> repository, string name) : IDomainRule
{
    public string Message => "Name should be unique";

    public Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public async Task<bool> ApplyAsync(CancellationToken cancellationToken = default)
    {
        return !(await repository.FindAllAsync(DinnerSpecifications.ForName(name),
            cancellationToken: cancellationToken)).SafeAny();
    }
}