// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;

/// <summary>
/// A generic factory for creating instances of a specified <see cref="DbContext"/> during design-time operations,
/// such as Entity Framework Core migrations. This factory supports flexible configuration of the database connection
/// string and provider, making it reusable across different projects and database providers (e.g., SQL Server, PostgreSQL, SQLite).
/// </summary>
/// <typeparam name="TContext">The type of the <see cref="DbContext"/> to create. Must derive from <see cref="DbContext"/>.</typeparam>
/// <remarks>
/// The factory retrieves the connection string from command-line arguments, configuration files (e.g., appsettings.json),
/// or environment variables, in that order of precedence. By default, it uses a convention-based configuration path
/// (e.g., "Modules:Core:ConnectionStrings:Default" for CoreDbContext) derived from the DbContext class name.
/// Consumers can override the configuration path or provide a custom configuration builder.
/// </remarks>
public class SqlServerModuleDbContextFactory<TContext> : IDesignTimeDbContextFactory<TContext>
    where TContext : DbContext
{
    private readonly string connectionStringKey;
    private readonly string commandLineArgPrefix;
    private readonly Action<DbContextOptionsBuilder, string> configureDbContextOptions;
    private readonly Action<IConfigurationBuilder> configureConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlServerModuleDbContextFactory{TContext}"/> class with a convention-based connection string key.
    /// </summary>
    /// <param name="options">An action to configure the <see cref="DbContextOptionsBuilder"/> with the connection string and database provider (e.g., UseSqlServer, UseNpgsql).</param>
    /// <param name="commandLineArgPrefix">The prefix for the command-line argument to override the connection string (default: "--connection-string="). Set to null to disable command-line parsing.</param>
    /// <param name="configuration">An optional action to customize the configuration builder (e.g., add user secrets, custom JSON files).</param>
    /// <remarks>
    /// The connection string key is derived from the DbContext class name (e.g., "Modules:Core:ConnectionStrings:Default" for CoreDbContext).
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    public SqlServerModuleDbContextFactory(
        Action<DbContextOptionsBuilder, string> options,
        string commandLineArgPrefix = "--connection-string=",
        Action<IConfigurationBuilder> configuration = null)
        : this(GetDefaultConnectionStringKey(typeof(TContext)), options, commandLineArgPrefix, configuration)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlServerModuleDbContextFactory{TContext}"/> class with a specified connection string key.
    /// </summary>
    /// <param name="connectionStringKey">The configuration key for the connection string (e.g., "ConnectionStrings:DefaultConnection").</param>
    /// <param name="options">An action to configure the <see cref="DbContextOptionsBuilder"/> with the connection string and database provider (e.g., UseSqlServer, UseNpgsql).</param>
    /// <param name="commandLineArgPrefix">The prefix for the command-line argument to override the connection string (default: "--connection-string="). Set to null to disable command-line parsing.</param>
    /// <param name="configuration">An optional action to customize the configuration builder (e.g., add user secrets, custom JSON files).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connectionStringKey"/> or <paramref name="options"/> is null.</exception>
    public SqlServerModuleDbContextFactory(
        string connectionStringKey,
        Action<DbContextOptionsBuilder, string> options,
        string commandLineArgPrefix = "--connection-string=",
        Action<IConfigurationBuilder> configuration = null)
    {
        this.connectionStringKey = connectionStringKey ?? throw new ArgumentNullException(nameof(connectionStringKey));
        this.configureDbContextOptions = options ?? throw new ArgumentNullException(nameof(options));
        this.commandLineArgPrefix = commandLineArgPrefix;
        this.configureConfiguration = configuration;
    }

    /// <summary>
    /// Creates an instance of the specified <see cref="DbContext"/> for use in design-time operations,
    /// such as generating Entity Framework Core migrations.
    /// </summary>
    /// <param name="args">Command-line arguments passed by the EF Core tools. Supports a connection string override if <see cref="commandLineArgPrefix"/> is set.</param>
    /// <returns>An instance of the specified <see cref="DbContext"/> configured with the appropriate database connection.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no valid connection string is found in command-line arguments, configuration, or environment variables.</exception>
    /// <exception cref="ArgumentException">Thrown when the DbContext type cannot be instantiated due to missing constructors.</exception>
    public TContext CreateDbContext(string[] args)
    {
        // Check for connection string in command-line arguments
        string connectionString = null;
        if (this.commandLineArgPrefix != null && args != null)
        {
            connectionString = args.FirstOrDefault(a => a.StartsWith(this.commandLineArgPrefix, StringComparison.OrdinalIgnoreCase))?.Substring(this.commandLineArgPrefix.Length);
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Build configuration from appsettings.json and environment variables
            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables();

            // Apply custom configuration if provided
            this.configureConfiguration?.Invoke(configurationBuilder);

            var configuration = configurationBuilder.Build();

            // Retrieve the connection string from configuration
            connectionString = configuration[this.connectionStringKey]
                ?? throw new InvalidOperationException($"Connection string for key '{this.connectionStringKey}' not found in configuration or command-line arguments.");
        }

        // Configure DbContextOptions
        var optionsBuilder = new DbContextOptionsBuilder<TContext>();
        this.configureDbContextOptions(optionsBuilder, connectionString);

        if (optionsBuilder.Options.Extensions.Any(e => e.GetType().Name.Contains("SqlServer", StringComparison.OrdinalIgnoreCase)))
        {
            optionsBuilder.ReplaceService<IMigrationsSqlGenerator,
                SqlServerIdempotentMigrationsSqlGenerator>();
        }

        // Instantiate the DbContext
        try
        {
            return Activator.CreateInstance(typeof(TContext), optionsBuilder.Options) as TContext
                ?? throw new ArgumentException($"Unable to instantiate DbContext of type {typeof(TContext).Name}. Ensure it has a constructor accepting DbContextOptions<{typeof(TContext).Name}>.");
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Failed to create instance of DbContext type {typeof(TContext).Name}.", ex);
        }
    }

    /// <summary>
    /// Generates a default connection string key based on the DbContext type name, following the convention
    /// "Modules:{ModuleName}:ConnectionStrings:Default". The module name is derived by removing "DbContext"
    /// and optionally "Module" from the type name.
    /// </summary>
    /// <param name="contextType">The type of the DbContext.</param>
    /// <returns>The default connection string key (e.g., "Modules:Core:ConnectionStrings:Default" for CoreDbContext).</returns>
    private static string GetDefaultConnectionStringKey(Type contextType)
    {
        var moduleName = contextType.Name//.ToLowerInvariant()
            .Replace("dbcontext", string.Empty, StringComparison.OrdinalIgnoreCase);

        //if (moduleName.EndsWith("module", StringComparison.OrdinalIgnoreCase) &&
        //    !moduleName.Equals("module", StringComparison.OrdinalIgnoreCase))
        //{
        //    moduleName = moduleName.Replace("module", string.Empty, StringComparison.OrdinalIgnoreCase);
        //}

        return $"Modules:{moduleName}:ConnectionStrings:Default";
    }
}