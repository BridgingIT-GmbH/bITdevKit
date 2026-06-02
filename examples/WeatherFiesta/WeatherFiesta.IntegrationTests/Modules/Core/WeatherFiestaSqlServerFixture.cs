// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.IntegrationTests;

using Testcontainers.MsSql;

/// <summary>
/// Shared SQL Server container fixture for WeatherFiesta relational integration tests.
/// </summary>
public sealed class WeatherFiestaSqlServerFixture : IAsyncLifetime
{
    public WeatherFiestaSqlServerFixture()
    {
        this.SqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    }

    /// <summary>Gets the SQL Server connection string.</summary>
    public string ConnectionString => this.SqlContainer.GetConnectionString();

    /// <summary>Gets the SQL Server container.</summary>
    public MsSqlContainer SqlContainer { get; }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await this.SqlContainer.StartAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        await this.SqlContainer.DisposeAsync();
    }
}

/// <summary>
/// Shared collection for SQL Server-backed WeatherFiesta tests.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WeatherFiestaSqlServerCollection : ICollectionFixture<WeatherFiestaSqlServerFixture>
{
    /// <summary>The xUnit collection name.</summary>
    public const string Name = "WeatherFiestaSqlServer";
}
