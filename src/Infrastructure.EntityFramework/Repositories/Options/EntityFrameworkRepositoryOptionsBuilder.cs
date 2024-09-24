// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using Common;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;

public class EntityFrameworkRepositoryOptionsBuilder
    : OptionsBuilderBase<EntityFrameworkRepositoryOptions, EntityFrameworkRepositoryOptionsBuilder>
{
    public EntityFrameworkRepositoryOptionsBuilder DbContext(DbContext context)
    {
        this.Target.DbContext = context;
        return this;
    }

    public EntityFrameworkRepositoryOptionsBuilder Mapper(IEntityMapper mapper)
    {
        this.Target.Mapper = mapper;
        return this;
    }
}