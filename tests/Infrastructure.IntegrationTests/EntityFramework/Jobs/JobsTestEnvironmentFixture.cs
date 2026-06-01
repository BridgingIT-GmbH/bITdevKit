// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Jobs;

using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Xunit.Abstractions;

public class JobsTestEnvironmentFixture : IAsyncLifetime
{
    public JobsTestEnvironmentFixture()
    {
        this.SqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        this.PostgresContainer = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("testdb")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
    }

    public string SqlConnectionString => this.SqlContainer.GetConnectionString();

    public string PostgresConnectionString => this.PostgresContainer.GetConnectionString();

    public MsSqlContainer SqlContainer { get; }

    public PostgreSqlContainer PostgresContainer { get; }

    public ITestOutputHelper Output { get; private set; }

    public JobsTestEnvironmentFixture WithOutput(ITestOutputHelper output)
    {
        this.Output = output;
        return this;
    }

    public async Task InitializeAsync()
    {
        await this.SqlContainer.StartAsync();
        await this.PostgresContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await this.PostgresContainer.DisposeAsync();
        await this.SqlContainer.DisposeAsync();
    }
}