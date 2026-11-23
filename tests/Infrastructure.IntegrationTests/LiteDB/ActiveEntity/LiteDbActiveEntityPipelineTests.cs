// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.ActiveEntity;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using Microsoft.Extensions.DependencyInjection;

[Collection(nameof(LiteDbActiveEntityTestCollection))]
public class LiteDbActiveEntityPipelineTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InsertAsync_ExistingBehaviorPipeline_OrdersCallbacksAndShortCircuits(bool reject)
    {
        var stages = new List<string>();
        await using var host = new LiteDbActiveEntityTestHost(configureServices: services =>
            services.AddSingleton<IActiveEntityBehavior<LedgerEntry>>(new Recorder(stages, reject)));
        using var global = host.ActivateGlobal();
        LedgerEntry.CallbackStages.Value = stages;
        try
        {
            var result = await new LedgerEntry { Name = "Entry", Amount = 12 }.InsertAsync();
            result.IsSuccess.ShouldBe(!reject);
            if (reject)
            {
                stages.ShouldBe(["behavior-before"]);
                result.HasError<ValidationError>().ShouldBeTrue();
                (await host.Provider.CountAsync()).Value.ShouldBe(0);
            }
            else
            {
                stages.ShouldBe(["behavior-before", "before-upsert", "before-insert", "after-insert", "after-upsert", "behavior-after"]);
                (await LedgerEntry.Query().Where(entry => entry.Amount > 10).ToListAsync()).Value.Count().ShouldBe(1);
            }
        }
        finally { LedgerEntry.CallbackStages.Value = null; }
    }

    [Fact]
    public async Task InsertAsync_ExistingDomainEventBehavior_PublishesWithoutStorageOfPendingEvents()
    {
        await using var host = new LiteDbActiveEntityTestHost(configureServices: services =>
            services.AddSingleton<IActiveEntityBehavior<LedgerEntry>>(new ActiveEntityDomainEventPublishingBehavior<LedgerEntry, Guid>(
                options: new ActiveEntityDomainEventPublishingBehaviorOptions { PublishBefore = false })));
        using var global = host.ActivateGlobal();
        var entry = new LedgerEntry();
        entry.RegisterDomainEvent(new LedgerEvent());
        (await entry.InsertAsync()).IsSuccess.ShouldBeTrue();
        entry.DomainEvents.ShouldBeEmpty();
        (await LedgerEntry.FindOneAsync(entry.Id)).Value.DomainEvents.ShouldBeEmpty();
    }

    private sealed class Recorder(List<string> stages, bool reject) : ActiveEntityBehaviorBase<LedgerEntry>
    {
        public override Task<Result> BeforeInsertAsync(LedgerEntry entity, CancellationToken cancellationToken = default)
        {
            stages.Add("behavior-before");
            return Task.FromResult(reject ? Result.Failure(new ValidationError("Rejected")) : Result.Success());
        }

        public override Task<Result> AfterInsertAsync(LedgerEntry entity, bool success, CancellationToken cancellationToken = default)
        {
            stages.Add("behavior-after");
            return Task.FromResult(Result.Success());
        }
    }
}
