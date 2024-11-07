// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

public class MenuForHostMustExistRule(IGenericRepository<Menu> repository, HostId hostId, MenuId menuId) : AsyncRuleBase
{
    public override string Message => "Menu for host must exist";

    protected override async Task<Result> ExecuteAsync(CancellationToken cancellationToken)
    {
        var menu = await repository.FindOneAsync(menuId, cancellationToken: cancellationToken);

        return Result.SuccessIf(menu?.HostId == hostId);
    }
}