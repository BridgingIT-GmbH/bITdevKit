// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Infrastructure.Modules.Core.EntityFramework;

using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// A factory for creating instances of <see cref="CoreDbContext"/> during design-time operations,
/// such as Entity Framework Core migrations. Extends the generic factory to provide SQL Server-specific configuration
/// for the CoreModule, using a convention-based connection string key.
/// </summary>
public class CoreDbContextFactory : SqlServerModuleDbContextFactory<CoreDbContext>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CoreDbContextFactory"/> class with settings specific to CoreModule.
    /// Uses SQL Server as the database provider and retrieves the connection string from a convention-based key
    /// ("Modules:Core:ConnectionStrings:Default") or a command-line override.
    /// </summary>
    public CoreDbContextFactory()
        : base(
            options: (builder, connectionString) =>
                builder.UseSqlServer(
                    connectionString,
                    sqlOptions => sqlOptions.MigrationsAssembly(typeof(CoreDbContext).Assembly.GetName().Name)))
    { }
}