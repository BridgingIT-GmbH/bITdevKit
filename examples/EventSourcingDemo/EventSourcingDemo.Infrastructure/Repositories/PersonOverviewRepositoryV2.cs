// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.EventSourcingDemo.Infrastructure.Repositories;

using Common;
using DevKit.Infrastructure.EntityFramework.Repositories;
using Domain.Model;
using Domain.Repositories;
using Models;

public class PersonOverviewRepositoryV2(EntityFrameworkRepositoryOptions options = null)
    : EntityFrameworkGenericRepository<PersonOverview, PersonDatabaseEntity>(options),
        IPersonOverviewRepositoryV2
{
    public async Task AddPersonAsync(PersonOverview person)
    {
        await base.InsertAsync(person).AnyContext();
    }
}