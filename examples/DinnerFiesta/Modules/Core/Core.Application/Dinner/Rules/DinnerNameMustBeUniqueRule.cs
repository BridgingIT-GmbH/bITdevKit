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

public class DinnerNameMustBeUniqueRule : IBusinessRule
{
    private readonly IGenericRepository<Dinner> repository;
    private readonly string name;

    public DinnerNameMustBeUniqueRule(IGenericRepository<Dinner> repository, string name)
    {
        EnsureArg.IsNotNull(repository, nameof(repository));

        this.repository = repository;
        this.name = name;
    }

    public string Message => "Name should be unique";

    public async Task<bool> IsSatisfiedAsync(CancellationToken cancellationToken = default)
    {
        return !(await this.repository.FindAllAsync(
            new DinnerForNameSpecification(this.name), cancellationToken: cancellationToken)).SafeAny();
    }
}