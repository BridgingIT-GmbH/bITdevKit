﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

public class DinnerNameMustBeUniqueRule(IGenericRepository<Dinner> repository, string name) : AsyncRuleBase
{
    public override string Message => "Name should be unique";

    protected override async Task<Result> ExecuteRuleAsync(CancellationToken cancellationToken)
    {
        return Result.SuccessIf(!(await repository.FindAllAsync(DinnerSpecifications.ForName(name),
            cancellationToken: cancellationToken)).SafeAny());
    }
}