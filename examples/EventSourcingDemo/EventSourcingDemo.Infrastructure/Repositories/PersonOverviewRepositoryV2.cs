// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.EventSourcingDemo.Infrastructure.Repositories;

using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using Domain.Model;
using Domain.Repositories;
using Models;

public class PersonOverviewRepositoryV2 : EntityFrameworkGenericRepository<PersonOverview, PersonDatabaseEntity>,
    IPersonOverviewRepositoryV2
{
    public PersonOverviewRepositoryV2(EntityFrameworkRepositoryOptions options = null)
        : base(options)
    {
    }

    public async Task AddPersonAsync(PersonOverview person)
    {
        await base.InsertAsync(person).AnyContext();
    }
}