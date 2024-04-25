// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;

public class InMemoryRepositoryOptions<TEntity> : OptionsBase
    where TEntity : class, IEntity
{
    public InMemoryContext<TEntity> Context { get; set; }

    public IEntityMapper Mapper { get; set; }

    public bool PublishEvents { get; set; } = true;

    public IEntityIdGenerator<TEntity> IdGenerator { get; set; }
}