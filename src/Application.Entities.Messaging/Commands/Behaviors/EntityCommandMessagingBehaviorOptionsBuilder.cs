// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;

public class EntityCommandMessagingBehaviorOptionsBuilder :
    OptionsBuilderBase<EntityCommandMessagingBehaviorOptions, EntityCommandMessagingBehaviorOptionsBuilder>
{
    private static readonly List<Type> ExcludedEntityTypes = [];

    public EntityCommandMessagingBehaviorOptionsBuilder Enabled(bool enabled)
    {
        this.Target.Enabled = enabled;
        return this;
    }

    public EntityCommandMessagingBehaviorOptionsBuilder Exclude<TEntity>()
        where TEntity : class, IEntity
    {
        ExcludedEntityTypes.Add(typeof(TEntity));

        this.Target.ExcludedEntityTypes = ExcludedEntityTypes;

        return this;
    }

    public EntityCommandMessagingBehaviorOptionsBuilder PublishDelay(int publishDelay)
    {
        this.Target.PublishDelay = publishDelay;
        return this;
    }
}