// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Domain.Repositories;
using Infrastructure.EntityFramework;
using Microsoft.Extensions.DependencyInjection;

[IntegrationTest("Infrastructure")]
public class RepositoryBuilderContextTests(ITestOutputHelper output) : TestsBase(output, services =>
    {
        services.AddMediatR()
            .AddSqlServerDbContext<StubDbContext>("dummy")
            .WithDatabaseCreatorService(o => o.DeleteOnStartup())
            .WithOutboxMessageService(o => o
                .ProcessingInterval("00:00:10")
                .StartupDelay("00:00:30")
                .PurgeOnStartup())
            .WithOutboxDomainEventService(o => o
                .ProcessingInterval("00:00:10")
                .StartupDelay("00:00:05")
                .PurgeOnStartup());

        services.AddEntityFrameworkRepository<PersonStub, StubDbContext>()
            //.WithTransactions()
            .WithBehavior<RepositoryTracingBehavior<PersonStub>>()
            .WithBehavior<RepositoryLoggingBehavior<PersonStub>>()
            .WithBehavior<RepositoryDomainEventBehavior<PersonStub>>()
            .WithBehavior<RepositoryOutboxDomainEventBehavior<PersonStub, StubDbContext>>();
        //.WithBehavior<RepositoryDomainEventPublisherBehavior<PersonStub>>();
    })
{
    [Fact]
    public void GetDbContext_WhenRequested_ShouldNotBeNull()
    {
        // Arrange
        // Act
        var sut = this.ServiceProvider.GetService<StubDbContext>();

        // Assert
        sut.ShouldNotBeNull();
    }

    [Fact]
    public void GetRepository_WhenRequested_ShouldNotBeNull()
    {
        // Arrange
        // Act
        var sut = this.ServiceProvider.GetService<IGenericRepository<PersonStub>>();

        // Assert
        sut.ShouldNotBeNull();
    }
}