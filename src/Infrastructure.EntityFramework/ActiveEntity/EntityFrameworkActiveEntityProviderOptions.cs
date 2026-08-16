// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.Extensions.Logging;

/// <summary>
/// Configures entity framework active entity provider.
/// </summary>
/// <typeparam name="TContext">The context type.</typeparam>
/// <typeparam name="TEntity">The entity type.</typeparam>
public class EntityFrameworkActiveEntityProviderOptions<TContext, TEntity> : OptionsBase
    where TEntity : class, IEntity
    where TContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <c>EntityFrameworkActiveEntityProviderOptions</c> class.
    /// </summary>
    public EntityFrameworkActiveEntityProviderOptions()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <c>EntityFrameworkActiveEntityProviderOptions</c> class.
    /// </summary>
    /// <param name="loggerFactory">The factory used to create loggers.</param>
    public EntityFrameworkActiveEntityProviderOptions(ILoggerFactory loggerFactory) : base(loggerFactory)
    {
    }

    /// <summary>
    /// Gets or sets the publish events.
    /// </summary>
    public bool PublishEvents { get; set; } = true;

    /// <summary>
    /// Gets or sets whether optimistic concurrency control is enabled.
    /// When enabled, updates will check the Version property for concurrency conflicts.
    /// </summary>
    public bool EnableOptimisticConcurrency { get; set; } = true;

    /// <summary>
    /// Gets or sets the strategy for generating new version identifiers.
    /// </summary>
    public Func<Guid> VersionGenerator { get; set; } = GuidGenerator.CreateSequential;

    /// <summary>
    /// Gets or sets the merge strategy for handling updates.
    /// </summary>
    public Func<TContext, TEntity, CancellationToken, Task<TEntity>> MergeStrategy { get; set; }
}
