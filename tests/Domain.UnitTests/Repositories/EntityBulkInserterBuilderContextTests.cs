// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests.Repositories;

using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;

public class EntityBulkInserterBuilderContextTests
{
    [Fact]
    public async Task WithBehavior_AllRegistrationForms_AppliesFirstBehaviorOutermostAndInvokesTerminalOnce()
    {
        // Arrange
        var events = new List<string>();
        var counter = new InvocationCounter();
        var services = CreateServices(events, counter);
        var sut = new EntityBulkInserterBuilderContext<BuilderEntity>(services);

        sut.WithBehavior<TypeBehavior>()
            .WithBehavior<FactoryBehavior>(inner => new FactoryBehavior(inner, events))
            .WithBehavior<ServiceProviderFactoryBehavior>((inner, serviceProvider) =>
                new ServiceProviderFactoryBehavior(
                    inner,
                    serviceProvider.GetRequiredService<IList<string>>()));
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var bulkInserter = scope.ServiceProvider.GetRequiredService<IEntityBulkInserter<BuilderEntity>>();

        // Act
        var result = await bulkInserter.InsertAsync([new BuilderEntity()]);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        bulkInserter.ShouldBeOfType<TypeBehavior>();
        counter.Count.ShouldBe(1);
        events.ShouldBe([
            "type.before",
            "factory.before",
            "service-provider.before",
            "terminal",
            "service-provider.after",
            "factory.after",
            "type.after",
        ]);
        services.ShouldNotContain(descriptor =>
            descriptor.ServiceType == typeof(IGenericRepository<BuilderEntity>));
    }

    [Fact]
    public async Task WithBehavior_ShortCircuitBehavior_DoesNotInvokeTerminal()
    {
        // Arrange
        var events = new List<string>();
        var counter = new InvocationCounter();
        var services = CreateServices(events, counter);
        var sut = new EntityBulkInserterBuilderContext<BuilderEntity>(services);
        sut.WithBehavior<ShortCircuitBehavior>();
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var bulkInserter = scope.ServiceProvider.GetRequiredService<IEntityBulkInserter<BuilderEntity>>();

        // Act
        var result = await bulkInserter.InsertAsync([new BuilderEntity()]);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(0);
        counter.Count.ShouldBe(0);
        events.ShouldBe(["short-circuit"]);
    }

    private static ServiceCollection CreateServices(IList<string> events, InvocationCounter counter)
    {
        var services = new ServiceCollection();
        services.AddSingleton(events);
        services.AddSingleton(counter);
        services.AddScoped<IEntityBulkInserter<BuilderEntity>, TerminalBulkInserter>();

        return services;
    }

    private sealed class BuilderEntity : Entity<Guid>;

    private sealed class InvocationCounter
    {
        public int Count { get; set; }
    }

    private sealed class TerminalBulkInserter(
        InvocationCounter counter,
        IList<string> events) : IEntityBulkInserter<BuilderEntity>
    {
        public Task<Result<long>> InsertAsync(
            IEnumerable<BuilderEntity> entities,
            CancellationToken cancellationToken = default)
        {
            counter.Count++;
            events.Add("terminal");

            return Task.FromResult(Result<long>.Success(entities.Count()));
        }
    }

    private sealed class TypeBehavior(
        IEntityBulkInserter<BuilderEntity> inner,
        IList<string> events) : IEntityBulkInserter<BuilderEntity>
    {
        public async Task<Result<long>> InsertAsync(
            IEnumerable<BuilderEntity> entities,
            CancellationToken cancellationToken = default)
        {
            events.Add("type.before");
            var result = await inner.InsertAsync(entities, cancellationToken);
            events.Add("type.after");

            return result;
        }
    }

    private sealed class FactoryBehavior(
        IEntityBulkInserter<BuilderEntity> inner,
        IList<string> events) : IEntityBulkInserter<BuilderEntity>
    {
        public async Task<Result<long>> InsertAsync(
            IEnumerable<BuilderEntity> entities,
            CancellationToken cancellationToken = default)
        {
            events.Add("factory.before");
            var result = await inner.InsertAsync(entities, cancellationToken);
            events.Add("factory.after");

            return result;
        }
    }

    private sealed class ServiceProviderFactoryBehavior(
        IEntityBulkInserter<BuilderEntity> inner,
        IList<string> events) : IEntityBulkInserter<BuilderEntity>
    {
        public async Task<Result<long>> InsertAsync(
            IEnumerable<BuilderEntity> entities,
            CancellationToken cancellationToken = default)
        {
            events.Add("service-provider.before");
            var result = await inner.InsertAsync(entities, cancellationToken);
            events.Add("service-provider.after");

            return result;
        }
    }

    private sealed class ShortCircuitBehavior(
        IEntityBulkInserter<BuilderEntity> inner,
        IList<string> events) : IEntityBulkInserter<BuilderEntity>
    {
        public Task<Result<long>> InsertAsync(
            IEnumerable<BuilderEntity> entities,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(inner);
            events.Add("short-circuit");

            return Task.FromResult(Result<long>.Success(0));
        }
    }
}
