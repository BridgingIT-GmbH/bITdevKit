// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

[IntegrationTest("Infrastructure")]
public class RepositoryBuilderContextTests : TestsBase
{
    public RepositoryBuilderContextTests(ITestOutputHelper output)
        : base(output, s =>
        {
            s.AddMediatR()
                .AddSqlServerDbContext<StubDbContext>("dummy")
                    .WithHealthChecks()
                    .WithDatabaseCreatorService(o => o.DeleteOnStartup())
                    .WithOutboxMessageService(o => o
                        .ProcessingInterval("00:00:10").StartupDelay("00:00:30").PurgeOnStartup())
                    .WithOutboxDomainEventService(o => o
                        .ProcessingInterval("00:00:10").StartupDelay("00:00:05").PurgeOnStartup());

            s.AddEntityFrameworkRepository<PersonStub, StubDbContext>()
                //.WithTransactions()
                .WithBehavior<RepositoryTracingBehavior<PersonStub>>()
                .WithBehavior<RepositoryLoggingBehavior<PersonStub>>()
                .WithBehavior<RepositoryDomainEventBehavior<PersonStub>>()
                .WithBehavior<RepositoryOutboxDomainEventBehavior<PersonStub, StubDbContext>>();
            //.WithBehavior<RepositoryDomainEventPublisherBehavior<PersonStub>>();
        })
    {
    }

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