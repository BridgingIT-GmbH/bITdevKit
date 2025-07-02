// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

public static partial class DbContextBuilderContextExtensions
{
    public static DbContextBuilderContext<TContext> WithDatabaseMigratorService<TContext>(
        this DbContextBuilderContext<TContext> context,
        Builder<DatabaseMigratorOptionsBuilder, DatabaseMigratorOptions> optionsBuilder)
        where TContext : DbContext
    {
        context.Services.AddDatabaseMigratorService<TContext>(optionsBuilder);

        return context;
    }

    public static DbContextBuilderContext<TContext> WithDatabaseMigratorService<TContext>(
        this DbContextBuilderContext<TContext> context,
        DatabaseMigratorOptions options = null)
        where TContext : DbContext
    {
        context.Services.AddDatabaseMigratorService<TContext>(options);

        return context;
    }

    public static DbContextBuilderContext<TContext> WithDatabaseCreatorService<TContext>(
        this DbContextBuilderContext<TContext> context,
        Builder<DatabaseCreatorOptionsBuilder, DatabaseCreatorOptions> optionsBuilder)
        where TContext : DbContext
    {
        context.Services.AddDatabaseCreatorService<TContext>(optionsBuilder);

        return context;
    }

    public static DbContextBuilderContext<TContext> WithDatabaseCreatorService<TContext>(
        this DbContextBuilderContext<TContext> context,
        DatabaseCreatorOptions options = null)
        where TContext : DbContext
    {
        context.Services.AddDatabaseCreatorService<TContext>(options);

        return context;
    }

    public static DbContextBuilderContext<TContext> WithDatabaseCheckerService<TContext>(
        this DbContextBuilderContext<TContext> context,
        DatabaseCheckerOptions options = null)
        where TContext : DbContext
    {
        context.Services.AddDatabaseCheckerService<TContext>(options);

        return context;
    }
}