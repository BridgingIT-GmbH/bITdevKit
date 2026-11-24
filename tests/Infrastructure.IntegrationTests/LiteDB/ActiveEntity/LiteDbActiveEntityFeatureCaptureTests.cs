// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.ActiveEntity;

using BridgingIT.DevKit.Application.Entities;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;
using Microsoft.Extensions.DependencyInjection;

[Collection(nameof(LiteDbActiveEntityTestCollection))]
[IntegrationTest("Infrastructure")]
public class LiteDbActiveEntityFeatureCaptureTests
{
    [Fact]
    public async Task CallbackEventsAndHistory_AreCapturedTogetherBeforeCommit()
    {
        await using var host = new LiteDbActiveEntityTestHost(configureServices: s =>
        {
            s.AddLiteDbDomainEventOutbox();
            s.AddLiteDbChangeHistory(o => o.Track<LedgerEntry>().CaptureChanges().AllowRestore(x => x.Name).UseValidatedSetter());
        });
        var entity = new LedgerEntry { Name = "active" };
        var domainEvent = new AccountRenamed { Name = entity.Name };
        var result = await host.Provider.InsertAsync(entity, new ActiveEntityCallbackOptions<LedgerEntry, Guid>
        {
            AfterInsertAsync = (_, _) =>
            {
                entity.RegisterDomainEvent(domainEvent, false);
                return Task.FromResult(Result.Success());
            }
        });
        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(e => e.Message)));
        entity.GetDomainEvents().ShouldBeEmpty();
        await using var scope = host.Services.CreateAsyncScope();
        var history = scope.ServiceProvider.GetRequiredService<IChangeHistoryService<LedgerEntry, LiteDbContext>>();
        (await history.FindAllAsync(new() { EntityId = entity.Id.ToString() })).Value.TotalCount.ShouldBeGreaterThan(0);
        var session = scope.ServiceProvider.GetRequiredKeyedService<LiteDbDatabaseSession>("default");
        var pending = await session.ExecuteAsync(async () => Result<int>.Success(await session.NativeAsync(db => db.GetCollection<LiteDbOutboxDomainEvent>("domain_event_outbox").Count())), false);
        pending.Value.ShouldBe(1);
    }
}
