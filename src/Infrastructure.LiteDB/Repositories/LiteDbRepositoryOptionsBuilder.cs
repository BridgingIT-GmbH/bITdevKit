// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;

using Common;
using Domain.Repositories;

public class LiteDbRepositoryOptionsBuilder
    : OptionsBuilderBase<LiteDbRepositoryOptions, LiteDbRepositoryOptionsBuilder>
{
    public LiteDbRepositoryOptionsBuilder DbContext(ILiteDbContext context)
    {
        this.Target.DbContext = context;

        return this;
    }

    public LiteDbRepositoryOptionsBuilder Mapper(IEntityMapper mapper)
    {
        this.Target.Mapper = mapper;

        return this;
    }
}