// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.EventSourcingDemo.IntegrationTests;

using BridgingIT.DevKit.Examples.EventSourcingDemo.Infrastructure.Repositories;
using Common;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.Azurite;
using Testcontainers.MsSql;
using Xunit.Abstractions;

public class TestEnvironmentFixture : IAsyncLifetime
{
    private ITestOutputHelper output = null;
    private IServiceProvider serviceProvider;

    public TestEnvironmentFixture()
    {
        this.Services.AddLogging(c => c.AddProvider(new XunitLoggerProvider(this.output)));

        this.Network = new NetworkBuilder()
            .WithName(this.NetworkName)
            .Build();

        this.SqlContainer = new MsSqlBuilder()
            .WithNetworkAliases(this.NetworkName)
            .WithExposedPort(1433)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1433))
            .Build();
    }

    public IServiceCollection Services { get; set; } = new ServiceCollection();

    public ITestOutputHelper Output => this.output;

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
        this.output = output;
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

        if (this.output is not null)
        {
            optionsBuilder = new DbContextOptionsBuilder<EventSourcingDemoDbContext>()
                .LogTo(this.output.WriteLine);
        }

        optionsBuilder.UseSqlServer(this.SqlConnectionString);

        return new EventSourcingDemoDbContext(optionsBuilder.Options);
    }
}
