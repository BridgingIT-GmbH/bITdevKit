// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System.Collections.Generic;
using System.Linq;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;

public class InMemoryContext<TEntity>
    where TEntity : class, IEntity
{
    public InMemoryContext()
    {
    }

    public InMemoryContext(List<TEntity> entities)
    {
        this.Entities = entities.SafeNull().ToList();
    }

    public InMemoryContext(IEnumerable<TEntity> entities)
    {
        this.Entities = entities.SafeNull().ToList();
    }

    public List<TEntity> Entities { get; set; } = [];
}