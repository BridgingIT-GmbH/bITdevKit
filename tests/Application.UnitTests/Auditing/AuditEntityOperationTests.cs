// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Auditing;

using System.Collections;
using Application.Auditing;
using Domain;
using Domain.Model;
using Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;

public sealed class AuditEntityOperationTests
{
    [Fact]
    public async Task Repository_RepeatedWritesKeepDistinctOccurrencesAndReturnOriginalValues()
    {
        using var host = Host();
        using var services = host.CreateScope();
        var audit = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var inner = Substitute.For<IGenericRepository<Customer>>();
        var customer = new Customer { Name = "private entity value" };
        inner.InsertAsync(customer, Arg.Any<CancellationToken>()).Returns(customer);
        inner.UpdateAsync(customer, Arg.Any<CancellationToken>()).Returns(customer);
        inner.DeleteAsync(customer, Arg.Any<CancellationToken>()).Returns(RepositoryActionResult.None);
        var sut = new RepositoryAuditingBehavior<Customer>(inner, audit);
        var root = audit.BeginScope(builder => builder.Key("customers.process"));
        (await sut.InsertAsync(customer)).ShouldBeSameAs(customer);
        (await sut.UpdateAsync(customer)).ShouldBeSameAs(customer);
        (await sut.DeleteAsync(customer)).ShouldBe(RepositoryActionResult.None);
        root.Complete();
        await root.DisposeAsync();
        var stored = (await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(root.AuditId)).Value;
        stored.Actions.Length.ShouldBe(3);
        stored.Actions.Select(action => action.Id).Distinct().Count().ShouldBe(3);
        stored.Actions.Select(action => action.Details["repository.invocationId"].Data.GetString()).Distinct().Count().ShouldBe(3);
        stored.Actions.All(action => action.Feature == "Repository" && action.Resources.Single().Id == customer.Id.ToString()).ShouldBeTrue();
        stored.Actions.Last().ExecutionStatus.ShouldBe("NoChange");
        stored.EntityChanges.ShouldBeEmpty();
        System.Text.Encoding.UTF8.GetString(AuditRecordCodec.Encode(stored)).ShouldNotContain(customer.Name);
        await inner.Received(1).InsertAsync(customer, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Repository_ReadsAreLocalOptInAndDoNotEnumerateReturnedStreams()
    {
        using var host = Host();
        using var services = host.CreateScope();
        var audit = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var inner = Substitute.For<IGenericRepository<Customer>>();
        var pending = Task.FromResult(7L);
        inner.CountAsync(Arg.Any<CancellationToken>()).Returns(pending);
        new RepositoryAuditingBehavior<Customer>(inner, audit).CountAsync().ShouldBeSameAs(pending);
        var values = new OnceEnumerable([new Customer(), new Customer()]);
        inner.FindAllAsync(Arg.Any<IFindOptions<Customer>>(), Arg.Any<CancellationToken>()).Returns(values);
        var sut = new RepositoryAuditingBehavior<Customer>(inner, audit, new() { SelectRead = _ => true });
        var found = await sut.FindAllAsync();
        found.ShouldBeSameAs(values);
        values.Enumerations.ShouldBe(0);
        found.Count().ShouldBe(2);
        values.Enumerations.ShouldBe(1);
        var records = await Records(host);
        records.Single().Kind.ShouldBe("Repository");
        records.Single().Resources.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true, ServiceLifetime.Scoped)]
    [InlineData(false, ServiceLifetime.Scoped)]
    [InlineData(true, ServiceLifetime.Singleton)]
    [InlineData(false, ServiceLifetime.Singleton)]
    public async Task Builder_AuditRemainsOutsidePostMutationDecoratorsInEitherRegistrationOrder(bool auditFirst, ServiceLifetime lifetime)
    {
        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddAuditing();
        var terminal = Substitute.For<IGenericRepository<Customer>>();
        terminal.InsertAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>()).Returns(call => call.Arg<Customer>());
        if (lifetime == ServiceLifetime.Singleton) collection.AddSingleton<IGenericRepository<Customer>>(_ => terminal);
        else collection.AddScoped<IGenericRepository<Customer>>(_ => terminal);
        var configured = new RepositoryBuilderContext<Customer>(collection, lifetime);
        if (auditFirst) configured.WithAuditing();
        var observed = false;
        configured.WithBehavior<IGenericRepository<Customer>>((inner, services) =>
        {
            var decorator = Substitute.For<IGenericRepository<Customer>>();
            decorator.InsertAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>()).Returns(async call =>
            {
                var value = await inner.InsertAsync(call.Arg<Customer>());
                observed = services.GetRequiredService<AuditRuntime>().Current?.IsRecording == true;
                return value;
            });
            return decorator;
        });
        if (!auditFirst) configured.WithAuditing();
        using var host = collection.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = host.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IGenericRepository<Customer>>().InsertAsync(new());
        observed.ShouldBeTrue();
        (await Records(host)).Length.ShouldBe(1);
    }

    [Fact]
    public async Task ActiveEntity_LifecycleHooksCaptureGeneratedResourceIdentityWithinTheOwnedOperation()
    {
        using var host = Host();
        using var services = host.CreateScope();
        var sut = new ActiveEntityAuditingBehavior<Customer>(services.ServiceProvider.GetRequiredService<IAuditContext>());
        var customer = new Customer { Id = Guid.Empty };
        var expected = Result.Success();
        var actual = await sut.ExecuteAsync("InsertAsync", async () =>
        {
            (await sut.BeforeInsertAsync(customer)).IsSuccess.ShouldBeTrue();
            customer.Id = Guid.NewGuid();
            (await sut.AfterInsertAsync(customer, true)).IsSuccess.ShouldBeTrue();
            return expected;
        });
        actual.ShouldBe(expected);
        var record = (await Records(host)).Single();
        record.Kind.ShouldBe("ActiveEntity");
        record.Resources.Single().Id.ShouldBe(customer.Id.ToString());
        record.EntityChanges.ShouldBeEmpty();
    }

    [Fact]
    public async Task BulkInput_IsEnumeratedOnlyByTheRealInserterAndRecordsTheObservedCount()
    {
        using var host = Host();
        using var services = host.CreateScope();
        var values = new OnceEnumerable([new(), new(), new()]);
        var inner = Substitute.For<IEntityBulkInserter<Customer>>();
        inner.InsertAsync(values, Arg.Any<CancellationToken>()).Returns(call => Result<long>.Success(call.Arg<IEnumerable<Customer>>().LongCount()));
        var sut = new EntityBulkInserterAuditingBehavior<Customer>(inner, services.ServiceProvider.GetRequiredService<IAuditContext>());
        (await sut.InsertAsync(values)).Value.ShouldBe(3);
        values.Enumerations.ShouldBe(1);
        var record = (await Records(host)).Single();
        record.Details["bulk.observedCount"].Data.GetInt64().ShouldBe(3);
        record.Details.ContainsKey("bulk.requestedCount").ShouldBeFalse();
        record.EntityChanges.ShouldBeEmpty();
        await inner.Received(1).InsertAsync(values, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompletionMetadataFailure_PreservesKnownOutcomeAndReleasesRootOwnership()
    {
        using var host = Host();
        using var services = host.CreateScope();
        var audit = services.ServiceProvider.GetRequiredService<IAuditContext>();
        (await AuditBehaviorExecutor.RunAsync(() => audit.BeginScope(builder => builder.Key("metadata.failure")), () => Task.FromResult(7), completed: (_, _) => throw new InvalidOperationException("private metadata"))).ShouldBe(7);
        await host.GetRequiredService<AuditRuntime>().WaitForIdleAsync().WaitAsync(TimeSpan.FromSeconds(10));
        var record = (await Records(host)).Single();
        record.Outcome.ShouldBe(AuditOutcome.Succeeded);
        record.Notes.ShouldContain(note => note.Code == "AuditSourceMetadataFailed");
    }

    [Fact]
    public async Task ActiveEntity_RealOperationIncludesProviderAndLifecycleAndSkipsOrdinaryRead()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuditing();
        services.AddActiveEntity(configuration => configuration.For<ActiveCustomer, Guid>().WithAuditing().WithAuditing().UseInMemoryProvider());
        using var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = host.CreateScope();
        var context = new ActiveEntityContext<ActiveCustomer, Guid>(scope.ServiceProvider.GetRequiredService<IActiveEntityEntityProvider<ActiveCustomer, Guid>>(),
            scope.ServiceProvider.GetServices<IActiveEntityBehavior<ActiveCustomer>>());
        var customer = new ActiveCustomer { Id = Guid.NewGuid(), Name = "private customer" };
        (await customer.InsertAsync(context)).IsSuccess.ShouldBeTrue();
        var stored = (await Records(host)).ShouldHaveSingleItem();
        stored.Kind.ShouldBe("ActiveEntity");
        stored.Resources.ShouldHaveSingleItem().Id.ShouldBe(customer.Id.ToString("D"));
        stored.Outcome.ShouldBe(AuditOutcome.Succeeded);
        stored.EntityChanges.ShouldBeEmpty();
        (await ActiveCustomer.FindOneAsync(context, customer.Id)).IsSuccess.ShouldBeTrue();
        (await Records(host)).Length.ShouldBe(1);
    }

    [Fact]
    public async Task ChangeEvidence_IsBoundedDeduplicatedAndCannotBeConfirmedAfterRootCloses()
    {
        var services = new ServiceCollection();
        services.AddAuditing(builder => builder.Limits(limits => limits with { MaxChangeGroups = 1 }));
        using var host = services.BuildServiceProvider();
        using var scope = host.CreateScope();
        var root = scope.ServiceProvider.GetRequiredService<IAuditContext>().BeginScope(builder => builder.Key("changes.bounds"));
        var sink = (IAuditChangeEvidenceSink)root;
        var evidence = new AuditChangeEvidence
        {
            SourceStoreKey = "customers", SourceRowIds = ["row-1"], EntityType = "Customer", EntityId = "42",
            Properties = [new() { Path = "Name", OldValue = AuditValue.From("private before"), NewValue = AuditValue.From("private after") }]
        };
        var handle = sink.StageChange(evidence);
        handle.ShouldNotBeNull();
        sink.StageChange(evidence).ShouldBeNull();
        sink.StageChange(evidence with { SourceRowIds = ["row-2"] }).ShouldBeNull();
        root.Complete();
        await root.DisposeAsync();
        handle.IsOpen.ShouldBeFalse();
        handle.Confirm(new HashSet<string> { "row-1" });
        var stored = (await Records(host)).Single();
        var group = stored.EntityChanges.ShouldHaveSingleItem();
        group.PersistenceState.ShouldBe("Unknown");
        group.Properties.Single().OldValue.ShouldBeNull();
        group.Properties.Single().NewValue.ShouldBeNull();
        stored.Notes.ShouldContain(note => note.Code == "AuditChangeLimitOrDuplicate");
        System.Text.Encoding.UTF8.GetString(AuditRecordCodec.Encode(stored)).ShouldNotContain("private");
    }

    public sealed class ActiveCustomer : ActiveEntity<ActiveCustomer, Guid>
    {
        public string Name { get; set; }
    }

    private static ServiceProvider Host()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuditing();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
    private static async Task<AuditRecord[]> Records(ServiceProvider host)
    {
        var provider = host.GetRequiredService<IAuditingStorageProvider>();
        var plan = host.GetRequiredService<AuditSearchPlanBuilder>().Build(null, withRange: false).Value;
        return (await Task.WhenAll((await provider.SearchAsync(plan)).Value.Select(async summary => (await provider.FindAsync(summary.AuditId)).Value)));
    }
    public sealed class Customer : IEntity
    {
        public object Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; }
    }
    public sealed class OnceEnumerable(IEnumerable<Customer> values) : IEnumerable<Customer>
    {
        public int Enumerations { get; private set; }
        public IEnumerator<Customer> GetEnumerator()
        {
            if (++this.Enumerations != 1) throw new InvalidOperationException("The source was enumerated more than once.");
            return values.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}
