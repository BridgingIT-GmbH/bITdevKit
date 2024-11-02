// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

using Common;
using DevKit.Domain;
using DevKit.Domain.Repositories;
using Domain;

public class UserEmailMustBeUniqueRule : AsyncDomainRuleBase
{
    private readonly IGenericRepository<User> repository;
    private readonly User user;

    public UserEmailMustBeUniqueRule(IGenericRepository<User> repository, User user)
    {
        EnsureArg.IsNotNull(repository, nameof(repository));

        this.repository = repository;
        this.user = user;
    }

    public override string Message => "User should be unique (email)";

    protected override async Task<Result> ExecuteRuleAsync(CancellationToken cancellationToken)
    {
        return Result.SuccessIf(!(await this.repository.FindAllAsync(UserSpecifications.ForEmail(this.user.Email),
            cancellationToken: cancellationToken)).SafeAny());
    }
}

public static class UserRules
{
    public static IDomainRule EmailMustBeUnique(IGenericRepository<User> repository, User user)
    {
        return new UserEmailMustBeUniqueRule(repository, user);
    }
}