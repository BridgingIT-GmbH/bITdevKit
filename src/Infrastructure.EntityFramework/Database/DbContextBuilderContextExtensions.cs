// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Extension methods for configuring database hosted services on <see cref="DbContextBuilderContext{TContext}" />.
/// </summary>
public static partial class DbContextBuilderContextExtensions
{
    /// <summary>
    ///     Adds the database migrator hosted service using a fluent options builder.
    /// </summary>
    /// <typeparam name="TContext">The database context type.</typeparam>
    /// <param name="context">The database context builder context.</param>
    /// <param name="optionsBuilder">The delegate that configures migrator options.</param>
    /// <returns>The same <paramref name="context" /> instance for fluent chaining.</returns>
    public static DbContextBuilderContext<TContext> WithDatabaseMigratorService<TContext>(
        this DbContextBuilderContext<TContext> context,
        Builder<DatabaseMigratorOptionsBuilder, DatabaseMigratorOptions> optionsBuilder)
        where TContext : DbContext
    {
        context.Services.AddDatabaseMigratorService<TContext>(optionsBuilder);

        return context;
    }

    /// <summary>
    ///     Adds the database migrator hosted service using explicit options.
    /// </summary>
    /// <typeparam name="TContext">The database context type.</typeparam>
    /// <param name="context">The database context builder context.</param>
    /// <param name="options">The migrator options.</param>
    /// <returns>The same <paramref name="context" /> instance for fluent chaining.</returns>
    public static DbContextBuilderContext<TContext> WithDatabaseMigratorService<TContext>(
        this DbContextBuilderContext<TContext> context,
        DatabaseMigratorOptions options = null)
        where TContext : DbContext
    {
        context.Services.AddDatabaseMigratorService<TContext>(options);

        return context;
    }

    /// <summary>
    ///     Adds the database creator hosted service using a fluent options builder.
    /// </summary>
    /// <typeparam name="TContext">The database context type.</typeparam>
    /// <param name="context">The database context builder context.</param>
    /// <param name="optionsBuilder">The delegate that configures creator options.</param>
    /// <returns>The same <paramref name="context" /> instance for fluent chaining.</returns>
    public static DbContextBuilderContext<TContext> WithDatabaseCreatorService<TContext>(
        this DbContextBuilderContext<TContext> context,
        Builder<DatabaseCreatorOptionsBuilder, DatabaseCreatorOptions> optionsBuilder)
        where TContext : DbContext
    {
        context.Services.AddDatabaseCreatorService<TContext>(optionsBuilder);

        return context;
    }

    /// <summary>
    ///     Adds the database creator hosted service using explicit options.
    /// </summary>
    /// <typeparam name="TContext">The database context type.</typeparam>
    /// <param name="context">The database context builder context.</param>
    /// <param name="options">The creator options.</param>
    /// <returns>The same <paramref name="context" /> instance for fluent chaining.</returns>
    public static DbContextBuilderContext<TContext> WithDatabaseCreatorService<TContext>(
        this DbContextBuilderContext<TContext> context,
        DatabaseCreatorOptions options = null)
        where TContext : DbContext
    {
        context.Services.AddDatabaseCreatorService<TContext>(options);

        return context;
    }

    /// <summary>
    ///     Adds the database checker hosted service using explicit options.
    /// </summary>
    /// <typeparam name="TContext">The database context type.</typeparam>
    /// <param name="context">The database context builder context.</param>
    /// <param name="options">The checker options.</param>
    /// <returns>The same <paramref name="context" /> instance for fluent chaining.</returns>
    public static DbContextBuilderContext<TContext> WithDatabaseCheckerService<TContext>(
        this DbContextBuilderContext<TContext> context,
        DatabaseCheckerOptions options = null)
        where TContext : DbContext
    {
        context.Services.AddDatabaseCheckerService<TContext>(options);

        return context;
    }
}
