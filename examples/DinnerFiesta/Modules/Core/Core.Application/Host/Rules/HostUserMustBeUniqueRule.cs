// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

using Common;
using DevKit.Domain;
using DevKit.Domain.Repositories;
using Domain;

public class HostUserMustBeUniqueRule : IDomainRule
{
    private readonly IGenericRepository<Host> repository;
    private readonly UserId userId;

    public HostUserMustBeUniqueRule(IGenericRepository<Host> repository, UserId userId)
    {
        EnsureArg.IsNotNull(repository, nameof(repository));

        this.repository = repository;
        this.userId = userId;
    }

    public string Message => "Host UserId should be unique";

    public Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public async Task<bool> ApplyAsync(CancellationToken cancellationToken = default)
    {
        return !(await this.repository.FindAllAsync(HostSpecifications.ForUser(this.userId),
            cancellationToken: cancellationToken)).SafeAny();
    }
}

public static class HostRules
{
    public static IDomainRule UserMustBeUnique(IGenericRepository<Host> repository, UserId userId)
    {
        return new HostUserMustBeUniqueRule(repository, userId);
    }
}