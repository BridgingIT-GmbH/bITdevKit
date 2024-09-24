// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.EventSourcingDemo.IntegrationTests;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Testcontainers.Azurite;
using Testcontainers.MsSql;

public class TestEnvironmentFixture : IAsyncLifetime
{
    private IServiceProvider serviceProvider;

    public TestEnvironmentFixture()
    {
        this.Services.AddLogging(c => c.AddProvider(new XunitLoggerProvider(this.Output)));

        this.Network = new NetworkBuilder()
            .WithName(this.NetworkName)
            .Build();

        this.SqlContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithNetworkAliases(this.NetworkName)
            .WithExposedPort(1433)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1433))
            .Build();
    }

    public IServiceCollection Services { get; set; } = new ServiceCollection();

    public ITestOutputHelper Output { get; private set; }

    public string NetworkName => "bit_devkit_eventsourcing_demo";

    public string SqlConnectionString => this.SqlContainer.GetConnectionString();

    public INetwork Network { get; }

    public MsSqlContainer SqlContainer { get; }

    public AzuriteContainer AzuriteContainer { get; }

    public IServiceProvider ServiceProvider
    {
        get
        {
            this.serviceProvider ??= this.Services.BuildServiceProvider();
            return this.serviceProvider;
        }
    }

    public EventSourcingDemoDbContext StubContext { get; private set; }

    public TestEnvironmentFixture WithOutput(ITestOutputHelper output)
    {
        this.Output = output;
        return this;
    }

    public async Task InitializeAsync()
    {
        await this.Network.CreateAsync().AnyContext();

        await this.SqlContainer.StartAsync().AnyContext();

        this.StubContext = this.CreateSqlServerDbContext();
        await this.StubContext.Database.MigrateAsync(); // ensure migrations are applied
    }

    public async Task DisposeAsync()
    {
        this.StubContext?.Dispose();

        await this.SqlContainer.DisposeAsync().AnyContext();

        await this.Network.DeleteAsync().AnyContext();
    }

    public EventSourcingDemoDbContext CreateSqlServerDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<EventSourcingDemoDbContext>();

        if (this.Output is not null)
        {
            optionsBuilder = new DbContextOptionsBuilder<EventSourcingDemoDbContext>()
                .LogTo(this.Output.WriteLine);
        }

        optionsBuilder.UseSqlServer(this.SqlConnectionString);

        return new EventSourcingDemoDbContext(optionsBuilder.Options);
    }
}