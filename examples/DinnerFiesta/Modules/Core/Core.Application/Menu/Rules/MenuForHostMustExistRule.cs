// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

using System.Threading.Tasks;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

public class MenuForHostMustExistRule : IBusinessRule
{
    private readonly IGenericRepository<Menu> repository;
    private readonly HostId hostId;
    private readonly MenuId menuId;

    public MenuForHostMustExistRule(IGenericRepository<Menu> repository, HostId hostId, MenuId menuId)
    {
        EnsureArg.IsNotNull(repository, nameof(repository));

        this.repository = repository;
        this.hostId = hostId;
        this.menuId = menuId;
    }

    public string Message => "Menu for host must exist";

    public async Task<bool> IsSatisfiedAsync(CancellationToken cancellationToken = default)
    {
        var menu = await this.repository.FindOneAsync(this.menuId, cancellationToken: cancellationToken);

        return menu?.HostId == this.hostId;
    }
}