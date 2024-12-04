// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

public class InMemoryRepositoryOptions<TEntity> : OptionsBase
    where TEntity : class, IEntity
{
    public InMemoryContext<TEntity> Context { get; set; }

    public IEntityMapper Mapper { get; set; }

    public bool PublishEvents { get; set; } = true;

    public IEntityIdGenerator<TEntity> IdGenerator { get; set; }

    /// <summary>
    /// Gets or sets whether optimistic concurrency control is enabled.
    /// When enabled, updates will check the Version property for concurrency conflicts.
    /// </summary>
    public bool EnableOptimisticConcurrency { get; set; } = true;

    /// <summary>
    /// Gets or sets the strategy for generating new version identifiers.
    /// </summary>
    public Func<Guid> VersionGenerator { get; set; } = GuidGenerator.CreateSequential;
}