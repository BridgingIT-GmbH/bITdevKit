// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.IntegrationTests.Repositories;

using Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;

[IntegrationTest("Infrastructure")]
public class RepositoryBuilderContextTests(ITestOutputHelper output) : TestsBase(output, s =>
    {
        s.AddMediatR();

        s.AddInMemoryRepository(
                new InMemoryContext<StubEntity>([new StubEntity { Id = "id-1-john", FirstName = "John" }, new StubEntity { Id = "id-2-mary", FirstName = "Mary" }]))
            .WithSequenceNumberGenerator("TestSequence", 100, 5)
            .WithBehavior<RepositoryTracingBehavior<StubEntity>>()
            .WithBehavior<RepositoryLoggingBehavior<StubEntity>>()
            .WithBehavior<RepositoryDomainEventBehavior<StubEntity>>()
            .WithBehavior<RepositoryDomainEventMediatorPublisherBehavior<StubEntity>>();
    })
{
    [Fact]
    public void GetRepository_WhenRequested_ShouldNotBeNull()
    {
        // Arrange
        // Act
        var context = this.ServiceProvider.GetService<InMemoryContext<StubEntity>>();
        var sut = this.ServiceProvider.GetService<IGenericRepository<StubEntity>>();

        // Assert
        sut.ShouldNotBeNull();
        context.ShouldNotBeNull();
    }

    [Fact]
    public async Task SequenceNumberGenerator_GeneratesNextValue()
    {
        // Arrange
        var sut = this.ServiceProvider.GetService<ISequenceNumberGenerator>();
        sut.ShouldNotBeNull();

        // Act
        var nextValue1 = await sut.GetNextAsync("TestSequence");
        var nextValue2 = await sut.GetNextAsync("TestSequence");

        // Assert

        nextValue1.ShouldBeSuccess();
        nextValue1.Value.ShouldBe(100);
        nextValue2.ShouldBeSuccess();
        nextValue2.Value.ShouldBe(105); // increment of 5
    }

    [Fact]
    public async Task SequenceNumberGenerator_UnknownSequenceFailes()
    {
        // Arrange
        var sut = this.ServiceProvider.GetService<ISequenceNumberGenerator>();
        sut.ShouldNotBeNull();

        // Act
        var nextValue = await sut.GetNextAsync("UnknownSequence");

        // Assert

        nextValue.ShouldBeFailure();
        nextValue.HasError<SequenceNotFoundError>().ShouldBeTrue();
    }
}

[IntegrationTest("Infrastructure")]
public class RepositoryBuilderContextNoInMemoryContextTests(ITestOutputHelper output) : TestsBase(output,
    s => s
        .AddMediatR()
        .AddInMemoryRepository<StubEntity>()
        .WithBehavior<RepositoryTracingBehavior<StubEntity>>()
        .WithBehavior<RepositoryLoggingBehavior<StubEntity>>()
        .WithBehavior<RepositoryDomainEventBehavior<StubEntity>>()
        .WithBehavior<RepositoryDomainEventMediatorPublisherBehavior<StubEntity>>())
{
    [Fact]
    public void GetRepository_WhenRequested_ShouldNotBeNull()
    {
        // Arrange
        // Act
        var context = this.ServiceProvider.GetService<InMemoryContext<StubEntity>>();
        var sut = this.ServiceProvider.GetService<IGenericRepository<StubEntity>>();

        // Assert
        sut.ShouldNotBeNull();
        context.ShouldNotBeNull();
    }
}