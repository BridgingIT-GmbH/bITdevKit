// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

/// <summary>
/// A factory for creating instances of <see cref="CoreDbContext"/> during design-time operations,
/// such as Entity Framework Core migrations. Extends the generic factory to provide SQL Server-specific configuration
/// for the CoreModule, using a convention-based connection string key.
/// </summary>
public class CoreDbContextFactory : IDesignTimeDbContextFactory<CoreDbContext>
{
    /// <inheritdoc />
    public CoreDbContext CreateDbContext(string[] args)
    {
        var connectionString = args?.FirstOrDefault(a => a.StartsWith("--connection-string=", StringComparison.OrdinalIgnoreCase))?["--connection-string=".Length..];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();

            connectionString = configuration["Modules:Core:ConnectionStrings:Default"];
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string for key 'Modules:Core:ConnectionStrings:Default' not found in configuration or command-line arguments.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<CoreDbContext>();
        optionsBuilder.UseSqlServer(
            connectionString,
            sqlOptions => sqlOptions.MigrationsAssembly(typeof(CoreDbContext).Assembly.GetName().Name));

        return new CoreDbContext(optionsBuilder.Options);
    }
}
