// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

using Common;
using Domain.Model;

/// <summary>
/// Builds entity command messaging behavior options configuration.
/// </summary>
public class EntityCommandMessagingBehaviorOptionsBuilder
    : OptionsBuilderBase<EntityCommandMessagingBehaviorOptions, EntityCommandMessagingBehaviorOptionsBuilder>
{
    private static readonly List<Type> ExcludedEntityTypes = [];

    /// <summary>
    /// Executes the enabled operation.
    /// </summary>
    /// <param name="enabled">The enabled used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public EntityCommandMessagingBehaviorOptionsBuilder Enabled(bool enabled)
    {
        this.Target.Enabled = enabled;

        return this;
    }

    /// <summary>
    /// Represents exclude.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    public EntityCommandMessagingBehaviorOptionsBuilder Exclude<TEntity>()
        where TEntity : class, IEntity
    {
        ExcludedEntityTypes.Add(typeof(TEntity));

        this.Target.ExcludedEntityTypes = ExcludedEntityTypes;

        return this;
    }

    /// <summary>
    /// Publishes delay.
    /// </summary>
    /// <param name="publishDelay">The publish delay used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public EntityCommandMessagingBehaviorOptionsBuilder PublishDelay(int publishDelay)
    {
        this.Target.PublishDelay = publishDelay;

        return this;
    }
}
