// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

using DevKit.Domain;
using DevKit.Domain.Repositories;
using Domain;

public class MenuForHostMustExistRule(IGenericRepository<Menu> repository, HostId hostId, MenuId menuId) : IDomainRule
{
    public string Message => "Menu for host must exist";

    public Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public async Task<bool> ApplyAsync(CancellationToken cancellationToken = default)
    {
        var menu = await repository.FindOneAsync(menuId, cancellationToken: cancellationToken);

        return menu?.HostId == hostId;
    }
}