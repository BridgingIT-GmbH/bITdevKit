// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

/// <summary>
/// The <c>EntityFrameworkRepositoryOptionsBuilder</c> class is used to configure options specific to
/// Entity Framework repositories. It allows the setting of various properties such as the DbContext,
/// IEntityMapper, and a version generator function which returns a Guid.
/// </summary>
/// <example>
/// EntityFrameworkRepositoryOptionsBuilder builder = new EntityFrameworkRepositoryOptionsBuilder();
/// builder.DbContext(myDbContext)
/// .Mapper(myEntityMapper)
/// .VersionGenerator(() => Guid.NewGuid());
/// </example>
public class EntityFrameworkRepositoryOptionsBuilder
    : OptionsBuilderBase<EntityFrameworkRepositoryOptions, EntityFrameworkRepositoryOptionsBuilder>
{
    /// Configures the DbContext for the EntityFrameworkRepositoryOptions.
    /// <param name="context">The DbContext to be used for the repository options.</param>
    /// <returns>Returns the same instance of EntityFrameworkRepositoryOptionsBuilder for further configuration chaining.</returns>
    /// Example usage:
    /// var optionsBuilder = new EntityFrameworkRepositoryOptionsBuilder().DbContext(myDbContext);
    public EntityFrameworkRepositoryOptionsBuilder DbContext(DbContext context)
    {
        this.Target.DbContext = context;

        return this;
    }

    /// <summary>
    /// Sets the IEntityMapper for the repository options.
    /// </summary>
    /// <param name="mapper">An instance of <see cref="IEntityMapper"/> used for mapping entities.</param>
    /// <returns>Returns the current instance of <see cref="EntityFrameworkRepositoryOptionsBuilder"/> to enable fluent configuration.</returns>
    /// <example>
    /// var optionsBuilder = new EntityFrameworkRepositoryOptionsBuilder();
    /// optionsBuilder.Mapper(mapperInstance);
    /// </example>
    public EntityFrameworkRepositoryOptionsBuilder Mapper(IEntityMapper mapper)
    {
        this.Target.Mapper = mapper;

        return this;
    }

    /// <summary>
    /// Sets the version generator function for the repository options.
    /// </summary>
    /// <param name="generator">A function that generates a <see cref="Guid"/> to be used as version identifiers.</param>
    /// <returns>The updated <see cref="EntityFrameworkRepositoryOptionsBuilder"/> instance.</returns>
    /// <example>
    /// <code>
    /// var builder = new EntityFrameworkRepositoryOptionsBuilder()
    /// .VersionGenerator(() => Guid.NewGuid());
    /// </code>
    /// </example>
    public EntityFrameworkRepositoryOptionsBuilder VersionGenerator(Func<Guid> generator)
    {
        this.Target.VersionGenerator = generator;

        return this;
    }

    public EntityFrameworkRepositoryOptionsBuilder EnableOptimisticConcurrency(bool value = true)
    {
        this.Target.EnableOptimisticConcurrency = value;

        return this;
    }
}