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

public class UserEmailMustBeUniqueRule : IBusinessRule
{
    private readonly IGenericRepository<User> repository;
    private readonly User user;

    public UserEmailMustBeUniqueRule(IGenericRepository<User> repository, User user)
    {
        EnsureArg.IsNotNull(repository, nameof(repository));

        this.repository = repository;
        this.user = user;
    }

    public string Message => "User should be unique (email)";

    public async Task<bool> IsSatisfiedAsync(CancellationToken cancellationToken = default)
    {
        return !(await this.repository.FindAllAsync(
            UserSpecifications.ForEmail(this.user.Email), cancellationToken: cancellationToken)).SafeAny();
    }
}

public static partial class UserRules
{
    public static IBusinessRule EmailMustBeUnique(IGenericRepository<User> repository, User user) => new UserEmailMustBeUniqueRule(repository, user);
}