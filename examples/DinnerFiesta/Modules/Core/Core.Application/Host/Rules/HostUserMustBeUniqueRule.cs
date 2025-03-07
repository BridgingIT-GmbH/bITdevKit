// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

public class HostUserMustBeUniqueRule : AsyncRuleBase
{
    private readonly IGenericRepository<Host> repository;
    private readonly UserId userId;

    public HostUserMustBeUniqueRule(IGenericRepository<Host> repository, UserId userId)
    {
        EnsureArg.IsNotNull(repository, nameof(repository));

        this.repository = repository;
        this.userId = userId;
    }

    public override string Message => "Host UserId should be unique";

    public override async Task<Result> ExecuteAsync(CancellationToken cancellationToken)
    {
        return Result.SuccessIf(!(await this.repository.FindAllAsync(
            HostSpecifications.ForUser(this.userId),
            cancellationToken: cancellationToken)).SafeAny());
    }
}

public static class HostRules
{
    public static IRule UserMustBeUnique(IGenericRepository<Host> repository, UserId userId)
    {
        return new HostUserMustBeUniqueRule(repository, userId);
    }
}