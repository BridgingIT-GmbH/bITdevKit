// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using Common;
using Model;

public class InMemoryContext<TEntity>
    where TEntity : class, IEntity
{
    public InMemoryContext() { }

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