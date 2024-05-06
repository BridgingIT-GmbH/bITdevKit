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

public class HostUserMustBeUniqueRule : IBusinessRule
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

    public async Task<bool> IsSatisfiedAsync(CancellationToken cancellationToken = default)
    {
        return !(await this.repository.FindAllAsync(
            new HostForUserSpecification(this.userId), cancellationToken: cancellationToken)).SafeAny();
    }
}