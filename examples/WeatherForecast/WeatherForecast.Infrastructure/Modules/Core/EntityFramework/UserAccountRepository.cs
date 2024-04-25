// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Infrastructure;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.WeatherForecast.Domain.Model;
using BridgingIT.DevKit.Examples.WeatherForecast.Infrastructure.EntityFramework;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using Dapper;

public class UserAccountRepository : EntityFrameworkGenericRepository<UserAccount, DbUserAccount>
{
    public UserAccountRepository(EntityFrameworkRepositoryOptions options)
        : base(options)
    {
    }

    public UserAccountRepository(Builder<EntityFrameworkRepositoryOptionsBuilder, EntityFrameworkRepositoryOptions> optionsBuilder)
        : base(optionsBuilder)
    {
    }

    public async Task<IEnumerable<string>> FindAllEmailAddresses()
    {
        return (await this.GetDbConnection().QueryAsync<string>(
            "SELECT EmailAddress FROM core.UserAccounts",
            transaction: this.GetDbTransaction())).ToList();
    }
}
