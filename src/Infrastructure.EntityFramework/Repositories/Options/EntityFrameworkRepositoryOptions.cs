// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

/// <summary>
/// Provides configuration options for an entity framework repository.
/// </summary>
/// <example>
/// To configure the repository with a specific DbContext and an optional IEntityMapper:
/// <code>
/// var options = new EntityFrameworkRepositoryOptions(myDbContext, myEntityMapper);
/// options.Autosave = false; // Disables automatic saving
/// </code>
/// </example>
public class EntityFrameworkRepositoryOptions : OptionsBase
{
    /// <summary>
    /// Represents configuration options for the EntityFramework repository, including context, mapper, version generator, and autosave settings.
    /// </summary>
    public EntityFrameworkRepositoryOptions() { }

    /// <summary>
    /// Represents options for configuring the Entity Framework repository.
    /// </summary>
    /// <example>
    /// <code>
    /// var options = new EntityFrameworkRepositoryOptions(dbContext, mapper);
    /// </code>
    /// </example>
    public EntityFrameworkRepositoryOptions(DbContext context, IEntityMapper mapper = null)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        this.DbContext = context;
        this.Mapper = mapper;
    }

    /// <summary>
    /// Gets or sets the database context used for querying and saving data.
    /// </summary>
    /// <value>
    /// The database context instance.
    /// </value>
    /// <example>
    /// The following code shows how to set the DbContext for the repository options:
    /// <code>
    /// var options = new EntityFrameworkRepositoryOptions();
    /// options.DbContext = new YourDbContext();
    /// </code>
    /// </example>
    public virtual DbContext DbContext { get; set; }

    /// <summary>
    /// Gets or sets the entity mapper.
    /// </summary>
    /// <value>
    /// The entity mapper.
    /// </value>
    /// <example>
    /// var entityFrameworkOptions = new EntityFrameworkRepositoryOptions();
    /// entityFrameworkOptions.Mapper = new CustomEntityMapper();
    /// </example>
    public virtual IEntityMapper Mapper { get; set; }

    /// <summary>
    /// Gets or sets whether optimistic concurrency control is enabled.
    /// When enabled, updates will check the Version property for concurrency conflicts.
    /// </summary>
    public bool EnableOptimisticConcurrency { get; set; } = true;

    /// <summary>
    /// Gets or sets the function used to generate version GUIDs.
    /// </summary>
    /// <value>
    /// A delegate that returns a <see cref="Guid" /> representing the version.
    /// </value>
    /// <example>
    /// options.VersionGenerator = GuidGenerator.CreateSequential;
    /// </example>
    public virtual Func<Guid> VersionGenerator { get; set; } = GuidGenerator.CreateSequential;

    /// <summary>
    /// Gets or sets a value indicating whether changes to the database context
    /// should be automatically saved.
    /// </summary>
    /// <value>
    /// <c>true</c> to automatically save changes; otherwise, <c>false</c>.
    /// The default is <c>true</c>.
    /// </value>
    /// <example>
    /// options.Autosave = false;  // Disables automatic saving of changes.
    /// </example>
    public virtual bool Autosave { get; set; } = true;
}