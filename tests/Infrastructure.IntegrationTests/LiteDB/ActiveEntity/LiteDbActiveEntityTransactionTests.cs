// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.ActiveEntity;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.Extensions.DependencyInjection;

[Collection(nameof(LiteDbActiveEntityTestCollection))]
public class LiteDbActiveEntityTransactionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Transaction_AwaitedWrites_CommitOrRollbackTogether(bool commit)
    {
        await using var host = new LiteDbActiveEntityTestHost();
        var sut = host.Provider;
        var transaction = await sut.BeginTransactionAsync();
        transaction.IsSuccess.ShouldBeTrue();
        transaction.Value.ShouldNotBeNull();
        (await sut.BeginTransactionAsync()).IsSuccess.ShouldBeTrue();
        await transaction.Value.ExecuteScopedAsync(async () =>
        {
            (await sut.InsertAsync(new LedgerEntry { Name = "First" })).IsSuccess.ShouldBeTrue();
            await Task.Run(() => 42);
            (await sut.InsertAsync(new LedgerEntry { Name = "Second" })).IsSuccess.ShouldBeTrue();
            (await sut.CountAsync()).Value.ShouldBe(2);
        });
        (await (commit ? sut.CommitTransactionAsync() : sut.RollbackAsync())).IsSuccess.ShouldBeTrue();
        (await sut.CountAsync()).Value.ShouldBe(commit ? 2 : 0);
        (await sut.CommitTransactionAsync()).IsFailure.ShouldBeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WithTransactionAsync_UnchangedCore_CommitsSuccessAndRollsBackFailure(bool success)
    {
        await using var host = new LiteDbActiveEntityTestHost();
        using var global = host.ActivateGlobal();
        var result = await LedgerEntry.WithTransactionAsync(async context =>
        {
            (await new LedgerEntry { Name = "First" }.InsertAsync(context)).IsSuccess.ShouldBeTrue();
            await Task.Yield();
            (await new LedgerEntry { Name = "Second" }.InsertAsync(context)).IsSuccess.ShouldBeTrue();
            return success ? Result.Success() : Result.Failure("Rejected");
        });
        result.IsSuccess.ShouldBe(success);
        (await host.Provider.CountAsync()).Value.ShouldBe(success ? 2 : 0);
    }

    [Fact]
    public async Task WithTransactionAsync_GenericAndException_UsesExistingCore()
    {
        await using var host = new LiteDbActiveEntityTestHost();
        using var global = host.ActivateGlobal();
        var success = await LedgerEntry.WithTransactionAsync<LedgerEntry>(context => new LedgerEntry { Name = "Committed" }.InsertAsync(context));
        success.IsSuccess.ShouldBeTrue();
        var failed = await LedgerEntry.WithTransactionAsync(async context =>
        {
            await new LedgerEntry { Name = "Rolled back" }.InsertAsync(context);
            throw new InvalidOperationException("Rejected");
            #pragma warning disable CS0162
            return Result.Success();
            #pragma warning restore CS0162
        });
        failed.IsFailure.ShouldBeTrue();
        failed.HasError<InvalidOperationError>().ShouldBeFalse();
        (await host.Provider.CountAsync()).Value.ShouldBe(1);
    }

    [Fact]
    public async Task Sessions_SecondScopeWaits_AndJoinsNoOtherTransaction()
    {
        await using var host = new LiteDbActiveEntityTestHost();
        var sut = host.Provider;
        await using var otherScope = host.Services.CreateAsyncScope();
        var other = otherScope.ServiceProvider.GetRequiredService<IActiveEntityEntityProvider<LedgerEntry, Guid>>();
        (await sut.BeginTransactionAsync()).IsSuccess.ShouldBeTrue();
        (await sut.InsertAsync(new LedgerEntry { Name = "Uncommitted" })).IsSuccess.ShouldBeTrue();
        var pending = other.InsertAsync(new LedgerEntry { Name = "Independent" });
        pending.IsCompleted.ShouldBeFalse();
        (await sut.RollbackAsync()).IsSuccess.ShouldBeTrue();
        (await pending.WaitAsync(TimeSpan.FromSeconds(10))).IsSuccess.ShouldBeTrue();
        (await other.CountAsync()).Value.ShouldBe(1);
        (await other.FindAllAsync()).Value.Single().Name.ShouldBe("Independent");
    }

    [Fact]
    public async Task Sessions_DifferentEntityTypesInSameScope_ShareRollback()
    {
        await using var host = new LiteDbActiveEntityTestHost();
        var sut = host.Provider;
        (await sut.BeginTransactionAsync()).IsSuccess.ShouldBeTrue();
        (await sut.InsertAsync(new LedgerEntry())).IsSuccess.ShouldBeTrue();
        var other = host.Get<LedgerItem<int>, int>();
        (await other.InsertAsync(new LedgerItem<int>())).IsSuccess.ShouldBeTrue();
        (await sut.RollbackAsync()).IsSuccess.ShouldBeTrue();
        (await other.CountAsync()).Value.ShouldBe(0);
    }

    [Fact]
    public async Task FailedTransaction_CallerIgnoresFailure_CannotCommitPartialWrites()
    {
        await using var host = new LiteDbActiveEntityTestHost();
        var sut = host.Provider;
        (await sut.BeginTransactionAsync()).IsSuccess.ShouldBeTrue();
        var entry = (await sut.InsertAsync(new LedgerEntry())).Value;
        (await sut.InsertAsync(entry)).IsFailure.ShouldBeTrue();
        (await sut.CommitTransactionAsync()).IsFailure.ShouldBeTrue();
        (await sut.RollbackAsync()).IsSuccess.ShouldBeTrue();
        (await sut.CountAsync()).Value.ShouldBe(0);
        entry.ConcurrencyVersion.ShouldBe(Guid.Empty);
    }

    [Fact]
    public async Task DisposeSession_AbandonedTransaction_ReleasesOwnershipAndRollsBack()
    {
        await using var host = new LiteDbActiveEntityTestHost();
        var scope = host.Services.CreateAsyncScope();
        var sut = scope.ServiceProvider.GetRequiredService<IActiveEntityEntityProvider<LedgerEntry, Guid>>();
        await sut.BeginTransactionAsync();
        await sut.InsertAsync(new LedgerEntry());
        await scope.DisposeAsync();
        (await host.Provider.CountAsync()).Value.ShouldBe(0);
        (await host.Provider.InsertAsync(new LedgerEntry())).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Cancellation_WaitingForOwnership_DoesNotWriteLater()
    {
        await using var host = new LiteDbActiveEntityTestHost();
        await host.Provider.BeginTransactionAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var sut = scope.ServiceProvider.GetRequiredService<IActiveEntityEntityProvider<LedgerEntry, Guid>>();
        using var cancellation = new CancellationTokenSource();
        var pending = sut.InsertAsync(new LedgerEntry(), cancellationToken: cancellation.Token);
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => pending);
        await host.Provider.RollbackAsync();
        (await sut.CountAsync()).Value.ShouldBe(0);
    }

    [Fact]
    public async Task Cancellation_AfterMutation_RollsBackAndRestoresVersion()
    {
        await using var host = new LiteDbActiveEntityTestHost();
        using var cancellation = new CancellationTokenSource();
        var entity = new LedgerEntry();
        await Should.ThrowAsync<OperationCanceledException>(() => host.Provider.InsertAsync(entity,
            new ActiveEntityCallbackOptions<LedgerEntry, Guid>
            {
                AfterInsertAsync = (_, _) => { cancellation.Cancel(); return Task.FromResult(Result.Success()); }
            }, cancellation.Token));
        entity.ConcurrencyVersion.ShouldBe(Guid.Empty);
        (await host.Provider.CountAsync()).Value.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteScopedAsync_GenericAdapter_ReturnsEntityWithoutAutoCommit()
    {
        await using var host = new LiteDbActiveEntityTestHost();
        var transaction = (await host.Provider.BeginTransactionAsync()).Value;
        var entity = await transaction.ExecuteScopedAsync(async () => (await host.Provider.InsertAsync(new LedgerEntry())).Value);
        entity.Id.ShouldNotBe(Guid.Empty);
        (await host.Provider.RollbackAsync()).IsSuccess.ShouldBeTrue();
        (await host.Provider.CountAsync()).Value.ShouldBe(0);
    }

    [Fact]
    public async Task Cancellation_ExplicitTransactionWithPreCanceledWrite_CannotCommitEarlierWrites()
    {
        await using var host = new LiteDbActiveEntityTestHost();
        await host.Provider.BeginTransactionAsync();
        await host.Provider.InsertAsync(new LedgerEntry());
        await Should.ThrowAsync<OperationCanceledException>(() => host.Provider.InsertAsync(new LedgerEntry(), cancellationToken: new CancellationToken(true)));
        (await host.Provider.CommitTransactionAsync()).IsFailure.ShouldBeTrue();
        (await host.Provider.RollbackAsync(new CancellationToken(true))).IsSuccess.ShouldBeTrue();
        (await host.Provider.CountAsync()).Value.ShouldBe(0);
    }

    [Fact]
    public async Task InsertAsync_OverlappingImplicitWrites_RejectsUnrelatedOperation()
    {
        await using var host = new LiteDbActiveEntityTestHost();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = host.Provider.InsertAsync(new LedgerEntry(), new ActiveEntityCallbackOptions<LedgerEntry, Guid>
        {
            BeforeInsertAsync = async (_, _) => { entered.SetResult(); await release.Task; return Result.Success(); }
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            (await host.Provider.InsertAsync(new LedgerEntry())).HasError<InvalidOperationError>().ShouldBeTrue();
        }
        finally { release.SetResult(); }

        (await pending).IsSuccess.ShouldBeTrue();
        (await host.Provider.CountAsync()).Value.ShouldBe(1);
    }

    [Fact]
    public async Task CommitAsync_NativeTransactionAlreadyAborted_DoesNotReportSuccess()
    {
        await using var host = new LiteDbActiveEntityTestHost();
        await host.Provider.BeginTransactionAsync();
        await host.Provider.InsertAsync(new LedgerEntry());
        var owner = host.Services.GetRequiredKeyedService<LiteDbDatabaseOwner>("default");
        (await owner.RunAsync(database => database.Rollback())).ShouldBeTrue();
        (await host.Provider.CommitTransactionAsync()).HasError<InvalidOperationError>().ShouldBeTrue();
        (await host.Provider.RollbackAsync()).IsSuccess.ShouldBeTrue();
        (await host.Provider.CountAsync()).Value.ShouldBe(0);
    }

    [Fact]
    public async Task NativeAsync_WithoutSessionReservation_IsRejected()
    {
        await using var host = new LiteDbActiveEntityTestHost();
        await using var scope = host.Services.CreateAsyncScope();
        var sut = scope.ServiceProvider.GetRequiredKeyedService<LiteDbDatabaseSession>("default");
        Should.Throw<LiteDbProviderException>(() => sut.NativeAsync(database => database.GetCollection("ledger").LongCount()));
    }

    [Fact]
    public async Task Shutdown_ActiveTransaction_ReleasesFileAndAllowsSessionCleanup()
    {
        await using var host = new LiteDbActiveEntityTestHost();
        await host.Provider.BeginTransactionAsync();
        var entry = new LedgerEntry();
        await host.Provider.InsertAsync(entry);
        await host.Services.GetRequiredKeyedService<LiteDbDatabaseOwner>("default").DisposeAsync();
        (await host.Provider.RollbackAsync()).IsSuccess.ShouldBeTrue();
        entry.ConcurrencyVersion.ShouldBe(Guid.Empty);
        using var reopened = new global::LiteDB.LiteDatabase(host.File);
        reopened.GetCollection("ledger").LongCount().ShouldBe(0);
    }

    [Fact]
    public async Task Executor_QueueAtCapacity_CancelsAdmissionAndDrainsAcceptedWork()
    {
        await using var sut = new LiteDbDatabaseExecutor();
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = sut.RunAsync(() => { entered.SetResult(); release.Wait(); return 0; });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Task<int>[] queued = [];
        try
        {
            queued = Enumerable.Range(1, 255).Select(value => sut.RunAsync(() => value)).ToArray();
            using var cancellation = new CancellationTokenSource();
            var pending = sut.RunAsync(() => -1, cancellation.Token);
            pending.IsCompleted.ShouldBeFalse();
            cancellation.Cancel();
            await Should.ThrowAsync<OperationCanceledException>(() => pending);
        }
        finally { release.Set(); }

        (await first).ShouldBe(0);
        (await Task.WhenAll(queued)).ShouldBe(Enumerable.Range(1, 255));
        await sut.DisposeAsync();
        await Should.ThrowAsync<ObjectDisposedException>(() => sut.RunAsync(() => -1));
    }
}
