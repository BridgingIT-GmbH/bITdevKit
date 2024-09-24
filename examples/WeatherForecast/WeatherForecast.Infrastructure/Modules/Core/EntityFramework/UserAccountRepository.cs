// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Infrastructure;

using Common;
using Dapper;
using DevKit.Infrastructure.EntityFramework.Repositories;
using Domain.Model;
using EntityFramework;

public class UserAccountRepository : EntityFrameworkGenericRepository<UserAccount, DbUserAccount>
{
    public UserAccountRepository(EntityFrameworkRepositoryOptions options)
        : base(options) { }

    public UserAccountRepository(
        Builder<EntityFrameworkRepositoryOptionsBuilder, EntityFrameworkRepositoryOptions> optionsBuilder)
        : base(optionsBuilder) { }

    public async Task<IEnumerable<string>> FindAllEmailAddresses()
    {
        return (await this.GetDbConnection()
            .QueryAsync<string>("SELECT EmailAddress FROM core.UserAccounts",
                transaction: this.GetDbTransaction())).ToList();
        //return await this.ProjectAllAsync<string>(e => e.Email);
    }
}