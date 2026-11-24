// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.ActiveEntity;

using System.Collections.Concurrent;
using global::LiteDB;

[Collection(nameof(LiteDbActiveEntityTestCollection))]
public class LiteDbActiveEntityEngineTests
{
    [Fact]
    public async Task NativeTransaction_CallerAwaits_RollsBackAllWritesOnOwnerThread()
    {
        // Arrange
        LiteDBPragmas.I_AM_AWARE_MY_DATABASE_BREAKS_WHEN_I_USE_THIS();
        var directory = Directory.CreateTempSubdirectory("bdk-litedb-engine-");
        using var commands = new BlockingCollection<Action>();
        LiteDatabase database = null;
        var thread = new Thread(() =>
        {
            foreach (var command in commands.GetConsumingEnumerable())
            {
                command();
            }
        });
        thread.Start();

        Task<T> Run<T>(Func<T> action)
        {
            var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            commands.Add(() =>
            {
                try { completion.SetResult(action()); }
                catch (Exception exception) { completion.SetException(exception); }
            });
            return completion.Task;
        }

        try
        {
            await Run(() => database = new LiteDatabase(Path.Combine(directory.FullName, "engine.db")));
            var ownerThread = await Run(() => Environment.CurrentManagedThreadId);
            (await Run(database.BeginTrans)).ShouldBeTrue();
            await Run(() => database.GetCollection("entries").Insert(new BsonDocument { ["_id"] = 1 }));

            // Act: caller continuation may change threads, native operations cannot.
            await Task.Run(() => 42);
            (await Task.Run(() => database.GetCollection("entries").LongCount())).ShouldBe(0);
            await Run(() => database.GetCollection("entries").Insert(new BsonDocument { ["_id"] = 2 }));
            (await Run(() => database.GetCollection("entries").LongCount())).ShouldBe(2);
            (await Run(database.Rollback)).ShouldBeTrue();

            // Assert
            (await Run(() => Environment.CurrentManagedThreadId)).ShouldBe(ownerThread);
            (await Run(() => database.GetCollection("entries").LongCount())).ShouldBe(0);
            (await Run(database.BeginTrans)).ShouldBeTrue();
            await Run(() => database.GetCollection("entries").Insert(new BsonDocument { ["_id"] = 3 }));
            (await Run(database.Commit)).ShouldBeTrue();
            (await Run(() => database.GetCollection("entries").LongCount())).ShouldBe(1);
        }
        finally
        {
            await Run(() => { database?.Dispose(); return true; });
            commands.CompleteAdding();
            thread.Join();
            directory.Delete(true);
        }
    }

    [Fact]
    public void NativeMapper_TypedIdentityAndRuntimeEvents_PreservesStoredValuesOnly()
    {
        // Arrange
        var mapper = new BsonMapper();
        mapper.RegisterType<TypedLedgerEntryId>(id => id.Value, value => TypedLedgerEntryId.Create(value.AsGuid));
        mapper.Entity<TypedLedgerEntry>().Id(entry => entry.Id).Ignore(entry => entry.DomainEvents);
        var entry = new TypedLedgerEntry { Id = TypedLedgerEntryId.Create(Guid.NewGuid()), Name = "Ledger" };
        entry.RegisterDomainEvent(new LedgerEvent());

        // Act
        var document = mapper.ToDocument(entry);
        var result = mapper.ToObject<TypedLedgerEntry>(document);

        // Assert
        document["_id"].AsGuid.ShouldBe(entry.Id.Value);
        document.ContainsKey("DomainEvents").ShouldBeFalse();
        result.Id.ShouldBe(entry.Id);
        result.Name.ShouldBe(entry.Name);
        result.DomainEvents.ShouldBeEmpty();

        mapper.Entity<LedgerEntry>().Ignore(item => item.DomainEvents);
        var ledger = new LedgerEntry
        {
            Id = Guid.NewGuid(), ConcurrencyVersion = Guid.NewGuid(), Balance = 123.45m,
            RecordedAt = new DateTime(2026, 11, 19, 10, 0, 0, DateTimeKind.Utc), Content = [1, 2, 3]
        };
        var restored = mapper.ToObject<LedgerEntry>(mapper.ToDocument(ledger));
        restored.ConcurrencyVersion.ShouldBe(ledger.ConcurrencyVersion);
        restored.Balance.ShouldBe(ledger.Balance);
        restored.RecordedAt.ToUniversalTime().ShouldBe(ledger.RecordedAt);
        restored.Content.ShouldBe(ledger.Content);
    }
}
