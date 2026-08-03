// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Repositories.Bulk;

using System.Transactions;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

public class EntityFrameworkEntityBulkInserterTests
{
    [Fact]
    public async Task InsertAsync_EmptyOrNullInput_ReturnsZeroWithoutProviderInvocation()
    {
        await using var context = CreateContext();
        var provider = new TestEntityBulkInsertProvider(context.Database.ProviderName, 1);
        using var services = CreateServiceProvider(context, [provider]);
        using var scope = services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<IEntityBulkInserter<DispatchEntity>>();

        var empty = await sut.InsertAsync([]);
        var nullInput = await sut.InsertAsync(null);
        var nullItem = await sut.InsertAsync([null]);

        empty.Value.ShouldBe(0);
        nullInput.Value.ShouldBe(0);
        nullItem.Value.ShouldBe(0);
        provider.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task InsertAsync_MatchingProvider_InvokesProviderOnceAndReturnsInsertedCount()
    {
        await using var context = CreateContext();
        var provider = new TestEntityBulkInsertProvider(context.Database.ProviderName, 2);
        using var services = CreateServiceProvider(context, [provider]);
        using var scope = services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<IEntityBulkInserter<DispatchEntity>>();

        var result = await sut.InsertAsync([new DispatchEntity { Name = "Ada" }, new DispatchEntity { Name = "Grace" }]);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(2);
        provider.CallCount.ShouldBe(1);
        provider.LastEntityCount.ShouldBe(2);
        context.Database.CurrentTransaction.ShouldBeNull();
    }

    [Fact]
    public async Task InsertAsync_UnsupportedProvider_ReturnsPreconditionBeforeMappingOrTransaction()
    {
        await using var context = CreateContext();
        var provider = new TestEntityBulkInsertProvider(context.Database.ProviderName, 1)
        {
            IsSupported = false,
            UnsupportedReason = "Native writes are not implemented.",
        };
        using var services = CreateServiceProvider(context, [provider]);
        using var scope = services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<IEntityBulkInserter<DispatchEntity>>();
        var entity = new DispatchEntity();

        var result = await sut.InsertAsync([entity]);

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().ShouldBeOfType<EntityBulkInsertPreconditionError>();
        result.Errors[0].Message.ShouldContain("not implemented");
        provider.CallCount.ShouldBe(0);
        entity.Id.ShouldBe(Guid.Empty);
    }

    [Fact]
    public async Task InsertAsync_InvalidMapping_ReturnsPreconditionWithoutProviderInvocation()
    {
        await using var context = CreateContext();
        var entity = new DispatchEntity();
        context.Add(entity);
        var provider = new TestEntityBulkInsertProvider(context.Database.ProviderName, 1);
        using var services = CreateServiceProvider(context, [provider]);
        using var scope = services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<IEntityBulkInserter<DispatchEntity>>();

        var result = await sut.InsertAsync([entity]);

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().ShouldBeOfType<EntityBulkInsertPreconditionError>().Stage.ShouldBe("mapping");
        provider.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task InsertAsync_ProviderFailure_ReturnsProviderErrorAndRollsBackOwnedTransaction()
    {
        await using var context = CreateContext();
        var provider = new TestEntityBulkInsertProvider(context.Database.ProviderName, 1)
        {
            ExceptionToThrow = new InvalidOperationException("Native provider failure."),
        };
        using var services = CreateServiceProvider(context, [provider]);
        using var scope = services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<IEntityBulkInserter<DispatchEntity>>();

        var result = await sut.InsertAsync([new DispatchEntity()]);

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(error =>
            error is EntityBulkInsertProviderError && ((EntityBulkInsertProviderError)error).Stage == "provider");
        context.Database.CurrentTransaction.ShouldBeNull();
    }

    [Fact]
    public async Task InsertAsync_Cancellation_RethrowsAndLeavesNoOwnedTransaction()
    {
        await using var context = CreateContext();
        var provider = new TestEntityBulkInsertProvider(context.Database.ProviderName, 1)
        {
            ExceptionToThrow = new OperationCanceledException(),
        };
        using var services = CreateServiceProvider(context, [provider]);
        using var scope = services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<IEntityBulkInserter<DispatchEntity>>();

        await Should.ThrowAsync<OperationCanceledException>(() => sut.InsertAsync([new DispatchEntity()]));

        context.Database.CurrentTransaction.ShouldBeNull();
    }

    [Fact]
    public async Task InsertAsync_AmbientTransaction_ReturnsPreconditionBeforeProviderInvocation()
    {
        await using var context = CreateContext();
        var provider = new TestEntityBulkInsertProvider(context.Database.ProviderName, 1);
        using var services = CreateServiceProvider(context, [provider]);
        using var scope = services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<IEntityBulkInserter<DispatchEntity>>();
        using var transactionScope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

        var result = await sut.InsertAsync([new DispatchEntity()]);

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().ShouldBeOfType<EntityBulkInsertPreconditionError>().Stage.ShouldBe("transaction.ambient");
        provider.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task InsertAsync_RetryingStrategyWithoutTransaction_ReturnsPreconditionBeforeProviderInvocation()
    {
        await using var context = CreateRetryingContext();
        var provider = new TestEntityBulkInsertProvider(context.Database.ProviderName, 1);
        using var services = CreateServiceProvider(context, [provider]);
        using var scope = services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<IEntityBulkInserter<DispatchEntity>>();

        var result = await sut.InsertAsync([new DispatchEntity()]);

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().ShouldBeOfType<EntityBulkInsertPreconditionError>().Stage.ShouldBe("transaction.retry-strategy");
        provider.CallCount.ShouldBe(0);
    }

    private static ServiceProvider CreateServiceProvider(DispatchDbContext context, IEnumerable<IEntityBulkInsertProvider> providers)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => context);
        foreach (var provider in providers)
        {
            services.AddSingleton(typeof(IEntityBulkInsertProvider), provider);
        }

        services.AddEntityFrameworkBulkInserter<DispatchEntity, DispatchDbContext>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static DispatchDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DispatchDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DispatchDbContext(options);
    }

    private static DispatchDbContext CreateRetryingContext()
    {
        var options = new DbContextOptionsBuilder<DispatchDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .ReplaceService<IExecutionStrategyFactory, RetryingExecutionStrategyFactory>()
            .Options;
        return new DispatchDbContext(options);
    }

    private sealed class DispatchDbContext(DbContextOptions<DispatchDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DispatchEntity>(builder =>
            {
                builder.HasKey(entity => entity.Id);
                builder.Property(entity => entity.Id).ValueGeneratedOnAdd();
            });
        }
    }

    private sealed class DispatchEntity : AggregateRoot<Guid>
    {
        public string Name { get; set; }
    }

    private sealed class RetryingExecutionStrategyFactory(ExecutionStrategyDependencies dependencies) : IExecutionStrategyFactory
    {
        public IExecutionStrategy Create() => new RetryingExecutionStrategy(dependencies);
    }

    private sealed class RetryingExecutionStrategy(ExecutionStrategyDependencies dependencies) : ExecutionStrategy(dependencies, 1, TimeSpan.Zero)
    {
        protected override bool ShouldRetryOn(Exception exception) => true;
    }
}
