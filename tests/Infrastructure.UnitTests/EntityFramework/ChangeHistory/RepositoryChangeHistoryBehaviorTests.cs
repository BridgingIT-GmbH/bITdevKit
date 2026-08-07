// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.ChangeHistory;

using System.Diagnostics;
using System.Linq.Expressions;
using BridgingIT.DevKit.Application.Entities;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

public class RepositoryChangeHistoryBehaviorTests
{
    [Fact]
    public async Task UpdateAsync_WithPendingEntityChange_PersistsRows()
    {
        using var context = CreateContext();
        var entity = new ChangeHistoryStubEntity { Id = Guid.NewGuid(), Name = "old", Email = "old@example.test" };
        context.Entities.Add(entity);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        entity.Change()
            .Set(e => e.Name, "new")
            .Set(e => e.Email, "new@example.test")
            .Apply();

        var sut = CreateRepository(context);
        await sut.UpdateAsync(entity);

        var rows = await context.Set<ChangeHistoryEntry>()
            .OrderBy(e => e.ChangeSetSequence)
            .ToListAsync();

        rows.Count.ShouldBe(2);
        rows.Select(e => e.ChangeSetId).Distinct().Count().ShouldBe(1);
        rows[0].PropertyName.ShouldBe(nameof(ChangeHistoryStubEntity.Name));
        rows[0].OldValue.ShouldBe("\"old\"");
        rows[0].NewValue.ShouldBe("\"new\"");
        rows[0].CaptureSource.ShouldBe(ChangeHistoryCaptureSource.EntityChange.ToString());
        EntityChangeHistoryAccessor.GetPendingChangeSets(entity).ShouldBeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_WithEntityChangeOnly_IgnoresDirectMutation()
    {
        using var context = CreateContext();
        var entity = new ChangeHistoryStubEntity { Id = Guid.NewGuid(), Name = "old" };
        context.Entities.Add(entity);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        entity.Name = "new";

        var options = new ChangeHistoryOptions();
        options.Track<ChangeHistoryStubEntity>()
            .CaptureDirectMutations(ChangeHistoryCaptureStrategy.EntityChangeOnly);

        var sut = CreateRepository(context, options);
        await sut.UpdateAsync(entity);

        var rows = await context.Set<ChangeHistoryEntry>().ToListAsync();
        rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_WithRepositorySnapshot_CapturesDirectMutation()
    {
        using var context = CreateContext();
        var entity = new ChangeHistoryStubEntity { Id = Guid.NewGuid(), Name = "old", Email = "old@example.test" };
        context.Entities.Add(entity);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        entity.Name = "new";

        var options = new ChangeHistoryOptions();
        options.Track<ChangeHistoryStubEntity>()
            .CaptureDirectMutations(ChangeHistoryCaptureStrategy.RepositorySnapshot, ChangeHistoryCaptureMode.Required);

        var sut = CreateRepository(context, options);
        await sut.UpdateAsync(entity);

        var row = await context.Set<ChangeHistoryEntry>().SingleAsync();
        row.PropertyName.ShouldBe(nameof(ChangeHistoryStubEntity.Name));
        row.OldValue.ShouldBe("\"old\"");
        row.NewValue.ShouldBe("\"new\"");
        row.CaptureSource.ShouldBe(ChangeHistoryCaptureSource.RepositorySnapshot.ToString());
    }

    [Fact]
    public async Task UpdateAsync_WithExcludedProperty_DoesNotPersistRow()
    {
        using var context = CreateContext();
        var entity = new ChangeHistoryStubEntity { Id = Guid.NewGuid(), Name = "old" };
        context.Entities.Add(entity);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        entity.Name = "new";

        var options = new ChangeHistoryOptions();
        options.Track<ChangeHistoryStubEntity>()
            .CaptureDirectMutations(ChangeHistoryCaptureStrategy.RepositorySnapshot, ChangeHistoryCaptureMode.Required)
            .Exclude(e => e.Name);

        var sut = CreateRepository(context, options);
        await sut.UpdateAsync(entity);

        var rows = await context.Set<ChangeHistoryEntry>().ToListAsync();
        rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_WithRedactedProperty_PersistsRedactedValues()
    {
        using var context = CreateContext();
        var entity = new ChangeHistoryStubEntity { Id = Guid.NewGuid(), Email = "old@example.test" };
        context.Entities.Add(entity);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        entity.Email = "new@example.test";

        var options = new ChangeHistoryOptions();
        options.Track<ChangeHistoryStubEntity>()
            .CaptureDirectMutations(ChangeHistoryCaptureStrategy.RepositorySnapshot, ChangeHistoryCaptureMode.Required)
            .Redact(e => e.Email);

        var sut = CreateRepository(context, options);
        await sut.UpdateAsync(entity);

        var row = await context.Set<ChangeHistoryEntry>().SingleAsync();
        row.OldValue.ShouldBe("\"***REDACTED***\"");
        row.NewValue.ShouldBe("\"***REDACTED***\"");
        row.OldValueHash.ShouldNotBeNull();
        row.NewValueHash.ShouldNotBeNull();
        row.IsRestoreable.ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateAsync_WithHashOnlyProperty_PersistsOnlyHashes()
    {
        using var context = CreateContext();
        var entity = new ChangeHistoryStubEntity { Id = Guid.NewGuid(), Email = "old@example.test" };
        context.Entities.Add(entity);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        entity.Email = "new@example.test";

        var options = new ChangeHistoryOptions();
        options.Track<ChangeHistoryStubEntity>()
            .CaptureDirectMutations(ChangeHistoryCaptureStrategy.RepositorySnapshot, ChangeHistoryCaptureMode.Required)
            .HashOnly(e => e.Email);

        var sut = CreateRepository(context, options);
        await sut.UpdateAsync(entity);

        var row = await context.Set<ChangeHistoryEntry>().SingleAsync();
        row.OldValue.ShouldBeNull();
        row.NewValue.ShouldBeNull();
        row.OldValueHash.ShouldNotBeNull();
        row.NewValueHash.ShouldNotBeNull();
        row.OldValueHash.ShouldNotBe(row.NewValueHash);
        row.IsRestoreable.ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateAsync_WithSensitivePropertyName_UsesHashOnlyByDefault()
    {
        using var context = CreateContext();
        var entity = new ChangeHistoryStubEntity { Id = Guid.NewGuid(), ApiToken = "old-token" };
        context.Entities.Add(entity);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        entity.ApiToken = "new-token";

        var options = new ChangeHistoryOptions();
        options.Track<ChangeHistoryStubEntity>()
            .CaptureDirectMutations(ChangeHistoryCaptureStrategy.RepositorySnapshot, ChangeHistoryCaptureMode.Required);

        var sut = CreateRepository(context, options);
        await sut.UpdateAsync(entity);

        var row = await context.Set<ChangeHistoryEntry>().SingleAsync();
        row.PropertyName.ShouldBe(nameof(ChangeHistoryStubEntity.ApiToken));
        row.OldValue.ShouldBeNull();
        row.NewValue.ShouldBeNull();
        row.OldValueHash.ShouldNotBeNull();
        row.NewValueHash.ShouldNotBeNull();
        row.IsRestoreable.ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateAsync_WithOversizedValuePolicy_TruncatesStoredValues()
    {
        using var context = CreateContext();
        var entity = new ChangeHistoryStubEntity { Id = Guid.NewGuid(), Name = "old-long-value" };
        context.Entities.Add(entity);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        entity.Name = "new-long-value";

        var options = new ChangeHistoryOptions()
            .UseOversizedValuePolicy(ChangeHistoryOversizedValuePolicy.Truncate, 6);
        options.Track<ChangeHistoryStubEntity>()
            .CaptureDirectMutations(ChangeHistoryCaptureStrategy.RepositorySnapshot, ChangeHistoryCaptureMode.Required);

        var sut = CreateRepository(context, options);
        await sut.UpdateAsync(entity);

        var row = await context.Set<ChangeHistoryEntry>().SingleAsync();
        row.OldValue.Length.ShouldBe(6);
        row.NewValue.Length.ShouldBe(6);
        row.OldValueHash.ShouldNotBeNull();
        row.NewValueHash.ShouldNotBeNull();
        row.IsRestoreable.ShouldBeFalse();
    }

    [Fact]
    public async Task InsertAsync_WithCreateCapture_PersistsInitialValues()
    {
        using var context = CreateContext();
        var entity = new ChangeHistoryStubEntity { Id = Guid.NewGuid(), Name = "created", Email = "created@example.test" };
        var options = new ChangeHistoryOptions();
        options.Track<ChangeHistoryStubEntity>()
            .CaptureCreates();

        var sut = CreateRepository(context, options);
        await sut.InsertAsync(entity);

        var rows = await context.Set<ChangeHistoryEntry>()
            .OrderBy(e => e.ChangeSetSequence)
            .ToListAsync();

        rows.Count.ShouldBe(2);
        rows.Select(e => e.ChangeSetId).Distinct().Count().ShouldBe(1);
        rows.ShouldAllBe(e => e.Operation == ChangeHistoryOperation.Create.ToString());
        rows.ShouldAllBe(e => e.CaptureSource == ChangeHistoryCaptureSource.Create.ToString());
        rows.ShouldAllBe(e => e.OldValue == null);
        rows.ShouldAllBe(e => e.IsRestoreable == false);
    }

    [Fact]
    public async Task InsertAsync_WithConfiguredCollection_CapturesAddedItemSnapshots()
    {
        using var context = CreateGraphIdentityContext();
        var tagId = Guid.NewGuid();
        var entity = new ChangeHistoryStubEntity
        {
            Id = Guid.NewGuid(),
            Tags = [new ChangeHistoryTag { Id = tagId, Value = "created" }]
        };
        var options = new ChangeHistoryOptions();
        options.Track<ChangeHistoryStubEntity>()
            .CaptureCreates()
            .CaptureCollection<ChangeHistoryTag>(e => e.Tags);

        var inner = new EntityFrameworkRepositoryWrapper<ChangeHistoryStubEntity, ChangeHistoryGraphIdentityStubDbContext>(
            NullLoggerFactory.Instance,
            context);
        var sut = new RepositoryChangeHistoryBehavior<ChangeHistoryStubEntity, ChangeHistoryGraphIdentityStubDbContext>(
            NullLoggerFactory.Instance,
            context,
            inner,
            options);
        await sut.InsertAsync(entity);

        var rows = await context.Set<ChangeHistoryEntry>().ToListAsync();

        rows.ShouldContain(e => e.PropertyPath == $"Tags[{tagId}]" && e.CollectionAction == "Added" && e.NewValue != null);
        rows.ShouldContain(e => e.PropertyPath == $"Tags[{tagId}].Value" && e.CollectionAction == "Added" && e.NewValue == "\"created\"");
    }

    [Fact]
    public async Task InsertSetAsync_WithCreateCapture_PersistsInitialValuesForEachEntity()
    {
        using var context = CreateContext();
        var entities = new[]
        {
            new ChangeHistoryStubEntity { Id = Guid.NewGuid(), Name = "created-1", Email = "one@example.test" },
            new ChangeHistoryStubEntity { Id = Guid.NewGuid(), Name = "created-2", Email = "two@example.test" }
        };
        var options = new ChangeHistoryOptions();
        options.Track<ChangeHistoryStubEntity>()
            .CaptureCreates();

        var sut = CreateRepository(context, options);
        var result = (await sut.InsertSetAsync(entities)).ToList();

        result.Count.ShouldBe(2);
        var rows = await context.Set<ChangeHistoryEntry>()
            .OrderBy(e => e.EntityId)
            .ThenBy(e => e.ChangeSetSequence)
            .ToListAsync();
        rows.Count.ShouldBe(4);
        rows.Select(e => e.EntityId).Distinct().Count().ShouldBe(2);
        rows.Select(e => e.ChangeSetId).Distinct().Count().ShouldBe(2);
        rows.ShouldAllBe(e => e.Operation == ChangeHistoryOperation.Create.ToString());
        rows.ShouldAllBe(e => e.CaptureSource == ChangeHistoryCaptureSource.Create.ToString());
    }

    [Fact]
    public async Task UpdateSetAsync_WithBulkCapture_PersistsPerEntityRows()
    {
        using var context = CreateContext();
        var entity1 = new ChangeHistoryStubEntity { Id = Guid.NewGuid(), Name = "old-1", Email = "one@example.test" };
        var entity2 = new ChangeHistoryStubEntity { Id = Guid.NewGuid(), Name = "old-2", Email = "two@example.test" };
        var inner = new InMemoryRepository([entity1, entity2]);
        var options = new ChangeHistoryOptions();
        options.Track<ChangeHistoryStubEntity>()
            .CaptureUpdateSet(ChangeHistoryCaptureMode.Required, maxAffectedRows: 10);
        var sut = new RepositoryChangeHistoryBehavior<ChangeHistoryStubEntity, ChangeHistoryStubDbContext>(
            NullLoggerFactory.Instance,
            context,
            inner,
            options);

        var affected = await sut.UpdateSetAsync(set => set.Set(e => e.Name, "new"));

        affected.ShouldBe(2);
        var rows = await context.Set<ChangeHistoryEntry>()
            .OrderBy(e => e.EntityId)
            .ToListAsync();
        rows.Count.ShouldBe(2);
        rows.Select(e => e.ChangeSetId).Distinct().Count().ShouldBe(2);
        rows.Select(e => e.BulkOperationId).Distinct().Count().ShouldBe(1);
        rows.ShouldAllBe(e => e.Operation == ChangeHistoryOperation.BulkUpdate.ToString());
        rows.ShouldAllBe(e => e.CaptureSource == ChangeHistoryCaptureSource.UpdateSet.ToString());
        rows.ShouldAllBe(e => e.AffectedEntityCount == 2);
        rows.ShouldAllBe(e => e.PropertyName == nameof(ChangeHistoryStubEntity.Name));
        rows.ShouldAllBe(e => e.NewValue == "\"new\"");
    }

    [Fact]
    public async Task UpdateSetAsync_WhenBestEffortExceedsLimit_PersistsSummaryRow()
    {
        using var context = CreateContext();
        var entity1 = new ChangeHistoryStubEntity { Id = Guid.NewGuid(), Name = "old-1" };
        var entity2 = new ChangeHistoryStubEntity { Id = Guid.NewGuid(), Name = "old-2" };
        var inner = new InMemoryRepository([entity1, entity2]);
        var options = new ChangeHistoryOptions();
        options.Track<ChangeHistoryStubEntity>()
            .CaptureUpdateSet(ChangeHistoryCaptureMode.BestEffort, maxAffectedRows: 1);
        var sut = new RepositoryChangeHistoryBehavior<ChangeHistoryStubEntity, ChangeHistoryStubDbContext>(
            NullLoggerFactory.Instance,
            context,
            inner,
            options);

        var affected = await sut.UpdateSetAsync(set => set.Set(e => e.Name, "new"));

        affected.ShouldBe(2);
        var row = await context.Set<ChangeHistoryEntry>().SingleAsync();
        row.CaptureStatus.ShouldBe(ChangeHistoryCaptureStatus.Summary.ToString());
        row.CaptureMessage.ShouldContain("exceeding the limit");
        row.AffectedEntityCount.ShouldBe(2);
        row.IsRestoreable.ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateAsync_WhenRequiredRepositorySnapshotBaselineIsMissing_ThrowsBeforeUpdate()
    {
        using var context = CreateContext();
        var entity = new ChangeHistoryStubEntity { Id = Guid.NewGuid(), Name = "new" };
        var options = new ChangeHistoryOptions();
        options.Track<ChangeHistoryStubEntity>()
            .CaptureDirectMutations(ChangeHistoryCaptureStrategy.RepositorySnapshot, ChangeHistoryCaptureMode.Required);
        var sut = CreateRepository(context, options);

        var action = async () => await sut.UpdateAsync(entity);

        await action.ShouldThrowAsync<InvalidOperationException>();
        (await context.Set<ChangeHistoryEntry>().ToListAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_WithOwnedPath_CapturesOwnedScalarChange()
    {
        using var context = CreateContext();
        var baseline = new ChangeHistoryStubEntity
        {
            Id = Guid.NewGuid(),
            Name = "customer",
            BillingAddress = new ChangeHistoryAddress { Street = "old street", City = "old city" }
        };
        var entity = CloneEntity(baseline);
        entity.BillingAddress.City = "new city";
        var inner = new InMemoryRepository([baseline]);
        var options = new ChangeHistoryOptions();
        options.Track<ChangeHistoryStubEntity>()
            .CaptureDirectMutations(ChangeHistoryCaptureStrategy.RepositorySnapshot, ChangeHistoryCaptureMode.Required)
            .CaptureOwned(e => e.BillingAddress);
        var sut = new RepositoryChangeHistoryBehavior<ChangeHistoryStubEntity, ChangeHistoryStubDbContext>(
            NullLoggerFactory.Instance,
            context,
            inner,
            options);

        await sut.UpdateAsync(entity);
        await context.SaveChangesAsync();

        var row = await context.Set<ChangeHistoryEntry>().SingleAsync();
        row.PropertyName.ShouldBe("BillingAddress.City");
        row.PropertyPath.ShouldBe("BillingAddress.City");
        row.PathKind.ShouldBe(ChangeHistoryCapturePathKind.Owned.ToString());
        row.OldValue.ShouldBe("\"old city\"");
        row.NewValue.ShouldBe("\"new city\"");
    }

    [Fact]
    public async Task UpdateAsync_WithCollectionPath_CapturesItemChangesAndMembership()
    {
        using var context = CreateContext();
        var keptId = Guid.NewGuid();
        var removedId = Guid.NewGuid();
        var addedId = Guid.NewGuid();
        var baseline = new ChangeHistoryStubEntity
        {
            Id = Guid.NewGuid(),
            Tags =
            [
                new ChangeHistoryTag { Id = keptId, Value = "old" },
                new ChangeHistoryTag { Id = removedId, Value = "removed" }
            ]
        };
        var entity = new ChangeHistoryStubEntity
        {
            Id = baseline.Id,
            Tags =
            [
                new ChangeHistoryTag { Id = keptId, Value = "new" },
                new ChangeHistoryTag { Id = addedId, Value = "added" }
            ]
        };
        var inner = new InMemoryRepository([baseline]);
        var options = new ChangeHistoryOptions();
        options.Track<ChangeHistoryStubEntity>()
            .CaptureDirectMutations(ChangeHistoryCaptureStrategy.RepositorySnapshot, ChangeHistoryCaptureMode.Required)
            .CaptureCollection(e => e.Tags, tag => tag.Id);
        var sut = new RepositoryChangeHistoryBehavior<ChangeHistoryStubEntity, ChangeHistoryStubDbContext>(
            NullLoggerFactory.Instance,
            context,
            inner,
            options);

        await sut.UpdateAsync(entity);
        await context.SaveChangesAsync();

        var rows = await context.Set<ChangeHistoryEntry>().ToListAsync();
        rows.ShouldContain(e => e.PropertyPath == $"Tags[{keptId}].Value" && e.OldValue == "\"old\"" && e.NewValue == "\"new\"" && e.CollectionAction == null);
        rows.ShouldContain(e => e.CollectionItemId == addedId.ToString() && e.CollectionAction == "Added" && e.OldValue == null);
        rows.ShouldContain(e => e.CollectionItemId == removedId.ToString() && e.CollectionAction == "Removed" && e.NewValue == null);
        rows.ShouldAllBe(e => e.PathKind == ChangeHistoryCapturePathKind.Collection.ToString());
    }

    [Fact]
    public async Task UpdateAsync_WithCollectionPathAndEfKeyMetadata_InfersIdentity()
    {
        using var context = CreateGraphIdentityContext();
        var tagId = Guid.NewGuid();
        var baseline = new ChangeHistoryStubEntity
        {
            Id = Guid.NewGuid(),
            Tags = [new ChangeHistoryTag { Id = tagId, Value = "old" }]
        };
        var entity = CloneEntity(baseline);
        entity.Tags[0].Value = "new";
        var inner = new InMemoryRepository([baseline]);
        var options = new ChangeHistoryOptions();
        options.Track<ChangeHistoryStubEntity>()
            .CaptureDirectMutations(ChangeHistoryCaptureStrategy.RepositorySnapshot, ChangeHistoryCaptureMode.Required)
            .CaptureCollection<ChangeHistoryTag>(e => e.Tags);
        var sut = new RepositoryChangeHistoryBehavior<ChangeHistoryStubEntity, ChangeHistoryGraphIdentityStubDbContext>(
            NullLoggerFactory.Instance,
            context,
            inner,
            options);

        await sut.UpdateAsync(entity);
        await context.SaveChangesAsync();

        var row = await context.Set<ChangeHistoryEntry>().SingleAsync(e => e.PropertyPath == $"Tags[{tagId}].Value");
        row.OldValue.ShouldBe("\"old\"");
        row.NewValue.ShouldBe("\"new\"");
        row.CollectionItemId.ShouldBe(tagId.ToString());
    }

    [Fact]
    public async Task UpdateAsync_WithCollectionCleared_CapturesClearedAction()
    {
        using var context = CreateContext();
        var tagId = Guid.NewGuid();
        var baseline = new ChangeHistoryStubEntity
        {
            Id = Guid.NewGuid(),
            Tags = [new ChangeHistoryTag { Id = tagId, Value = "old" }]
        };
        var entity = CloneEntity(baseline);
        entity.Tags.Clear();
        var inner = new InMemoryRepository([baseline]);
        var options = new ChangeHistoryOptions();
        options.Track<ChangeHistoryStubEntity>()
            .CaptureDirectMutations(ChangeHistoryCaptureStrategy.RepositorySnapshot, ChangeHistoryCaptureMode.Required)
            .CaptureCollection(e => e.Tags, tag => tag.Id);
        var sut = new RepositoryChangeHistoryBehavior<ChangeHistoryStubEntity, ChangeHistoryStubDbContext>(
            NullLoggerFactory.Instance,
            context,
            inner,
            options);

        await sut.UpdateAsync(entity);
        await context.SaveChangesAsync();

        var row = await context.Set<ChangeHistoryEntry>().SingleAsync(e => e.CollectionItemId == tagId.ToString());
        row.CollectionAction.ShouldBe("Cleared");
    }

    [Fact]
    public async Task UpdateAsync_WithCollectionReplaced_CapturesReplacedAction()
    {
        using var context = CreateContext();
        var oldId = Guid.NewGuid();
        var newId = Guid.NewGuid();
        var baseline = new ChangeHistoryStubEntity
        {
            Id = Guid.NewGuid(),
            Tags = [new ChangeHistoryTag { Id = oldId, Value = "old" }]
        };
        var entity = new ChangeHistoryStubEntity
        {
            Id = baseline.Id,
            Tags = [new ChangeHistoryTag { Id = newId, Value = "new" }]
        };
        var inner = new InMemoryRepository([baseline]);
        var options = new ChangeHistoryOptions();
        options.Track<ChangeHistoryStubEntity>()
            .CaptureDirectMutations(ChangeHistoryCaptureStrategy.RepositorySnapshot, ChangeHistoryCaptureMode.Required)
            .CaptureCollection(e => e.Tags, tag => tag.Id);
        var sut = new RepositoryChangeHistoryBehavior<ChangeHistoryStubEntity, ChangeHistoryStubDbContext>(
            NullLoggerFactory.Instance,
            context,
            inner,
            options);

        await sut.UpdateAsync(entity);
        await context.SaveChangesAsync();

        var rows = await context.Set<ChangeHistoryEntry>().ToListAsync();
        rows.ShouldContain(e => e.CollectionItemId == oldId.ToString() && e.CollectionAction == "Replaced");
        rows.ShouldContain(e => e.CollectionItemId == newId.ToString() && e.CollectionAction == "Replaced");
    }

    [Fact]
    public async Task UpdateAsync_WithGraphPath_CapturesNestedGraphChanges()
    {
        using var context = CreateContext();
        var orderId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var baseline = new ChangeHistoryStubEntity
        {
            Id = Guid.NewGuid(),
            Orders =
            [
                new ChangeHistoryOrder
                {
                    Id = orderId,
                    Number = "SO-1",
                    Items = [new ChangeHistoryOrderItem { Id = itemId, Quantity = 1, Sku = "ABC" }]
                }
            ]
        };
        var entity = CloneEntity(baseline);
        entity.Orders[0].Items[0].Quantity = 5;
        var inner = new InMemoryRepository([baseline]);
        var options = new ChangeHistoryOptions();
        options.Track<ChangeHistoryStubEntity>()
            .CaptureDirectMutations(ChangeHistoryCaptureStrategy.RepositorySnapshot, ChangeHistoryCaptureMode.Required)
            .CaptureGraph("Orders", graph => graph
                .UseIdentity<ChangeHistoryOrder, Guid>("Orders", order => order.Id)
                .UseIdentity<ChangeHistoryOrderItem, Guid>("Orders.Items", item => item.Id)
                .UseRestorePlan("OrderItemsRestore"));
        var sut = new RepositoryChangeHistoryBehavior<ChangeHistoryStubEntity, ChangeHistoryStubDbContext>(
            NullLoggerFactory.Instance,
            context,
            inner,
            options);

        await sut.UpdateAsync(entity);
        await context.SaveChangesAsync();

        var row = await context.Set<ChangeHistoryEntry>().SingleAsync(e => e.PropertyName.EndsWith("Quantity", StringComparison.Ordinal));
        row.PropertyPath.ShouldBe($"Orders[{orderId}].Items[{itemId}].Quantity");
        row.PathKind.ShouldBe(ChangeHistoryCapturePathKind.Graph.ToString());
        row.OldValue.ShouldBe("1");
        row.NewValue.ShouldBe("5");
        row.CollectionItemId.ShouldBe(itemId.ToString());
        row.RestorePlanName.ShouldBe("OrderItemsRestore");
    }

    [Fact]
    public async Task UpdateAsync_WithEfChangeTracker_CapturesConfiguredOwnedAndCollectionPaths()
    {
        using var context = CreateTrackedPathContext();
        var tagId = Guid.NewGuid();
        var entity = new ChangeHistoryStubEntity
        {
            Id = Guid.NewGuid(),
            BillingAddress = new ChangeHistoryAddress { Street = "main", City = "old city" },
            Tags = [new ChangeHistoryTag { Id = tagId, Value = "old" }]
        };
        context.Set<ChangeHistoryStubEntity>().Add(entity);
        await context.SaveChangesAsync();

        entity.BillingAddress.City = "new city";
        entity.Tags[0].Value = "new";
        var options = new ChangeHistoryOptions();
        options.Track<ChangeHistoryStubEntity>()
            .CaptureDirectMutations(ChangeHistoryCaptureStrategy.EfChangeTracker, ChangeHistoryCaptureMode.Required)
            .CaptureOwned(e => e.BillingAddress)
            .CaptureCollection<ChangeHistoryTag>(e => e.Tags);
        var inner = new EntityFrameworkRepositoryWrapper<ChangeHistoryStubEntity, ChangeHistoryTrackedPathStubDbContext>(
            NullLoggerFactory.Instance,
            context);
        var sut = new RepositoryChangeHistoryBehavior<ChangeHistoryStubEntity, ChangeHistoryTrackedPathStubDbContext>(
            NullLoggerFactory.Instance,
            context,
            inner,
            options);

        await sut.UpdateAsync(entity);

        var rows = await context.Set<ChangeHistoryEntry>().ToListAsync();
        rows.ShouldContain(e => e.PropertyPath == "BillingAddress.City" && e.OldValue == "\"old city\"" && e.NewValue == "\"new city\"");
        rows.ShouldContain(e => e.PropertyPath == $"Tags[{tagId}].Value" && e.OldValue == "\"old\"" && e.NewValue == "\"new\"");
    }

    [Fact]
    public async Task UpsertAsync_WhenEntityExists_CapturesUpdateStyleHistory()
    {
        using var context = CreateContext();
        var baseline = new ChangeHistoryStubEntity { Id = Guid.NewGuid(), Name = "old" };
        var entity = CloneEntity(baseline);
        entity.Name = "new";
        var inner = new InMemoryRepository([baseline]);
        var options = new ChangeHistoryOptions();
        options.Track<ChangeHistoryStubEntity>()
            .CaptureDirectMutations(ChangeHistoryCaptureStrategy.RepositorySnapshot, ChangeHistoryCaptureMode.Required)
            .CaptureCreates();
        var sut = new RepositoryChangeHistoryBehavior<ChangeHistoryStubEntity, ChangeHistoryStubDbContext>(
            NullLoggerFactory.Instance,
            context,
            inner,
            options);

        var result = await sut.UpsertAsync(entity);
        await context.SaveChangesAsync();

        result.action.ShouldBe(RepositoryActionResult.Updated);
        var row = await context.Set<ChangeHistoryEntry>().SingleAsync();
        row.Operation.ShouldBe(ChangeHistoryOperation.Update.ToString());
        row.CaptureSource.ShouldBe(ChangeHistoryCaptureSource.RepositorySnapshot.ToString());
        row.OldValue.ShouldBe("\"old\"");
        row.NewValue.ShouldBe("\"new\"");
    }

    [Fact]
    public async Task UpdateAsync_WithGraphCollectionWithoutIdentity_ThrowsValidationError()
    {
        using var context = CreateContext();
        var baseline = new ChangeHistoryStubEntity
        {
            Id = Guid.NewGuid(),
            Orders = [new ChangeHistoryOrder { Id = Guid.NewGuid(), Number = "SO-1" }]
        };
        var entity = CloneEntity(baseline);
        entity.Orders[0].Number = "SO-2";
        var inner = new InMemoryRepository([baseline]);
        var options = new ChangeHistoryOptions();
        options.Track<ChangeHistoryStubEntity>()
            .CaptureDirectMutations(ChangeHistoryCaptureStrategy.RepositorySnapshot, ChangeHistoryCaptureMode.Required)
            .CaptureGraph("Orders");
        var sut = new RepositoryChangeHistoryBehavior<ChangeHistoryStubEntity, ChangeHistoryStubDbContext>(
            NullLoggerFactory.Instance,
            context,
            inner,
            options);

        await Should.ThrowAsync<InvalidOperationException>(() => sut.UpdateAsync(entity));
    }

    [Fact]
    public async Task HandleAsync_WithValidatedSetterRestore_RestoresEntityAndPersistsRestoreRows()
    {
        using var context = CreateContext();
        var entity = new ChangeHistoryStubEntity { Id = Guid.NewGuid(), Name = "new" };
        var originalChangeSetId = Guid.NewGuid();
        context.Entities.Add(entity);
        context.Set<ChangeHistoryEntry>().Add(CreateHistoryEntry(entity, originalChangeSetId, nameof(ChangeHistoryStubEntity.Name), "old", "new"));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var options = new ChangeHistoryOptions();
        options.Track<ChangeHistoryStubEntity>()
            .AllowRestore(e => e.Name)
            .UseValidatedSetter();
        var handler = CreateRestoreHandler(context, options);

        var result = await handler.HandleAsync(new ChangeHistoryRestoreCommand<ChangeHistoryStubEntity>(entity.Id, originalChangeSetId, "restore reason"));

        result.IsSuccess.ShouldBeTrue();
        var restored = await context.Entities.SingleAsync(e => e.Id == entity.Id);
        restored.Name.ShouldBe("old");
        var restoreRow = await context.Set<ChangeHistoryEntry>().SingleAsync(e => e.Operation == ChangeHistoryOperation.Restore.ToString());
        restoreRow.OldValue.ShouldBe("\"new\"");
        restoreRow.NewValue.ShouldBe("\"old\"");
        restoreRow.CaptureSource.ShouldBe(ChangeHistoryCaptureSource.Restore.ToString());
        restoreRow.RestoreExecutionMode.ShouldBe(ChangeHistoryRestoreExecutionMode.ValidatedSetter.ToString());
        restoreRow.Properties.ShouldContain(originalChangeSetId.ToString());
    }

    [Fact]
    public async Task HandleAsync_WithRepositoryChangeHistoryBehavior_RestoresEntityAndPersistsRestoreRows()
    {
        using var context = CreateContext();
        var entity = new ChangeHistoryStubEntity { Id = Guid.NewGuid(), Name = "new" };
        var originalChangeSetId = Guid.NewGuid();
        context.Entities.Add(entity);
        context.Set<ChangeHistoryEntry>().Add(CreateHistoryEntry(entity, originalChangeSetId, nameof(ChangeHistoryStubEntity.Name), "old", "new"));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var options = new ChangeHistoryOptions();
        options.Track<ChangeHistoryStubEntity>()
            .CaptureDirectMutations(ChangeHistoryCaptureStrategy.RepositorySnapshot, ChangeHistoryCaptureMode.Required)
            .AllowRestore(e => e.Name)
            .UseValidatedSetter();
        var handler = CreateRestoreHandler(context, options, repository: CreateRepository(context, options));

        var result = await handler.HandleAsync(new ChangeHistoryRestoreCommand<ChangeHistoryStubEntity>(entity.Id, originalChangeSetId, "restore reason"));

        result.IsSuccess.ShouldBeTrue();
        var restored = await context.Entities.SingleAsync(e => e.Id == entity.Id);
        restored.Name.ShouldBe("old");
        var restoreRows = await context.Set<ChangeHistoryEntry>()
            .Where(e => e.Operation == ChangeHistoryOperation.Restore.ToString())
            .ToListAsync();
        restoreRows.Count.ShouldBe(1);
    }

    [Fact]
    public async Task HandleAsync_WithPointInTimeRestore_RestoresLaterChangesToSelectedPoint()
    {
        using var context = CreateContext();
        var entity = new ChangeHistoryStubEntity
        {
            Id = Guid.NewGuid(),
            Name = "d",
            Email = "new@example.test"
        };
        var firstChangeSetId = Guid.NewGuid();
        var secondChangeSetId = Guid.NewGuid();
        var thirdChangeSetId = Guid.NewGuid();
        var fourthChangeSetId = Guid.NewGuid();
        var changedAt = DateTimeOffset.UtcNow.AddMinutes(-4);
        var first = CreateHistoryEntry(entity, firstChangeSetId, nameof(ChangeHistoryStubEntity.Name), "old", "b");
        first.ChangedDate = changedAt;
        var second = CreateHistoryEntry(entity, secondChangeSetId, nameof(ChangeHistoryStubEntity.Name), "b", "c");
        second.ChangedDate = changedAt.AddMinutes(1);
        var third = CreateHistoryEntry(entity, thirdChangeSetId, nameof(ChangeHistoryStubEntity.Email), "old@example.test", "new@example.test");
        third.ChangedDate = changedAt.AddMinutes(2);
        var fourth = CreateHistoryEntry(entity, fourthChangeSetId, nameof(ChangeHistoryStubEntity.Name), "c", "d");
        fourth.ChangedDate = changedAt.AddMinutes(3);
        context.Entities.Add(entity);
        context.Set<ChangeHistoryEntry>().AddRange(first, second, third, fourth);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var options = new ChangeHistoryOptions();
        options.Track<ChangeHistoryStubEntity>()
            .AllowRestore(e => e.Name)
            .UseValidatedSetter()
            .AllowRestore(e => e.Email)
            .UseValidatedSetter();
        var handler = CreateRestoreHandler(context, options);

        var result = await handler.HandleAsync(new ChangeHistoryRestoreCommand<ChangeHistoryStubEntity>(
            entity.Id,
            firstChangeSetId,
            "restore to selected point",
            RestoreMode: ChangeHistoryRestoreMode.PointInTime));

        result.IsSuccess.ShouldBeTrue();
        result.Value.RestoredPropertyCount.ShouldBe(2);
        var restored = await context.Entities.SingleAsync(e => e.Id == entity.Id);
        restored.Name.ShouldBe("old");
        restored.Email.ShouldBe("old@example.test");
        var restoreRows = await context.Set<ChangeHistoryEntry>()
            .Where(e => e.Operation == ChangeHistoryOperation.Restore.ToString())
            .ToListAsync();
        restoreRows.Select(e => e.PropertyName).ShouldBe([nameof(ChangeHistoryStubEntity.Name), nameof(ChangeHistoryStubEntity.Email)], ignoreOrder: true);
    }

    [Fact]
    public async Task HandleAsync_WithFailingDomainMethod_DoesNotMutateOrPersistRestoreRows()
    {
        using var context = CreateContext();
        var entity = new ChangeHistoryStubEntity { Id = Guid.NewGuid(), Name = "new" };
        var originalChangeSetId = Guid.NewGuid();
        context.Entities.Add(entity);
        context.Set<ChangeHistoryEntry>().Add(CreateHistoryEntry(entity, originalChangeSetId, nameof(ChangeHistoryStubEntity.Name), "blocked", "new"));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var options = new ChangeHistoryOptions();
        options.Track<ChangeHistoryStubEntity>()
            .AllowRestore(e => e.Name)
            .UseDomainMethod((stub, value) => stub.RestoreName(value));
        var handler = CreateRestoreHandler(context, options);

        var result = await handler.HandleAsync(new ChangeHistoryRestoreCommand<ChangeHistoryStubEntity>(entity.Id, originalChangeSetId));

        result.IsFailure.ShouldBeTrue();
        var unchanged = await context.Entities.SingleAsync(e => e.Id == entity.Id);
        unchanged.Name.ShouldBe("new");
        (await context.Set<ChangeHistoryEntry>().CountAsync(e => e.Operation == ChangeHistoryOperation.Restore.ToString())).ShouldBe(0);
    }

    [Fact]
    public async Task HandleAsync_WithTypedDomainHandler_RestoresEntity()
    {
        using var context = CreateContext();
        var entity = new ChangeHistoryStubEntity { Id = Guid.NewGuid(), Name = "new" };
        var originalChangeSetId = Guid.NewGuid();
        context.Entities.Add(entity);
        context.Set<ChangeHistoryEntry>().Add(CreateHistoryEntry(entity, originalChangeSetId, nameof(ChangeHistoryStubEntity.Name), "old", "new"));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var services = new ServiceCollection()
            .AddSingleton<NameRestoreHandler>()
            .BuildServiceProvider();

        var options = new ChangeHistoryOptions();
        options.Track<ChangeHistoryStubEntity>()
            .AllowRestore(e => e.Name)
            .UseDomainHandler<NameRestoreHandler>();
        var handler = CreateRestoreHandler(context, options, services);

        var result = await handler.HandleAsync(new ChangeHistoryRestoreCommand<ChangeHistoryStubEntity>(entity.Id, originalChangeSetId));

        result.IsSuccess.ShouldBeTrue();
        var restored = await context.Entities.SingleAsync(e => e.Id == entity.Id);
        restored.Name.ShouldBe("old");
        var restoreRow = await context.Set<ChangeHistoryEntry>().SingleAsync(e => e.Operation == ChangeHistoryOperation.Restore.ToString());
        restoreRow.DomainRestoreHandlerName.ShouldBe(nameof(NameRestoreHandler));
    }

    [Fact]
    public async Task HandleAsync_WithRestoreAuthorizerFailure_DoesNotRestoreEntity()
    {
        using var context = CreateContext();
        var entity = new ChangeHistoryStubEntity { Id = Guid.NewGuid(), Name = "new" };
        var originalChangeSetId = Guid.NewGuid();
        context.Entities.Add(entity);
        context.Set<ChangeHistoryEntry>().Add(CreateHistoryEntry(entity, originalChangeSetId, nameof(ChangeHistoryStubEntity.Name), "old", "new"));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var services = new ServiceCollection()
            .AddSingleton<DenyRestoreAuthorizer>()
            .BuildServiceProvider();

        var options = new ChangeHistoryOptions();
        options.Track<ChangeHistoryStubEntity>()
            .UseRestoreAuthorizer<DenyRestoreAuthorizer>()
            .AllowRestore(e => e.Name)
            .UseValidatedSetter();
        var handler = CreateRestoreHandler(context, options, services);

        var result = await handler.HandleAsync(new ChangeHistoryRestoreCommand<ChangeHistoryStubEntity>(entity.Id, originalChangeSetId));

        result.IsFailure.ShouldBeTrue();
        var unchanged = await context.Entities.SingleAsync(e => e.Id == entity.Id);
        unchanged.Name.ShouldBe("new");
        (await context.Set<ChangeHistoryEntry>().CountAsync(e => e.Operation == ChangeHistoryOperation.Restore.ToString())).ShouldBe(0);
    }

    [Fact]
    public async Task HandleAsync_WithOwnedRestorePlan_RestoresOwnedPath()
    {
        using var context = CreateContext();
        var entity = new ChangeHistoryStubEntity
        {
            Id = Guid.NewGuid(),
            BillingAddress = new ChangeHistoryAddress { City = "new city" }
        };
        var originalChangeSetId = Guid.NewGuid();
        context.Set<ChangeHistoryEntry>().Add(new ChangeHistoryEntry
        {
            Id = Guid.NewGuid(),
            ChangeSetId = originalChangeSetId,
            ChangeSetSequence = 0,
            EntityType = nameof(ChangeHistoryStubEntity),
            EntityClrType = typeof(ChangeHistoryStubEntity).AssemblyQualifiedName,
            EntityId = entity.Id.ToString(),
            EntityIdType = typeof(Guid).AssemblyQualifiedName,
            PropertyName = "BillingAddress.City",
            PropertyPath = "BillingAddress.City",
            PathKind = ChangeHistoryCapturePathKind.Owned.ToString(),
            ValueClrType = typeof(string).AssemblyQualifiedName,
            OldValue = "\"old city\"",
            NewValue = "\"new city\"",
            Operation = ChangeHistoryOperation.Update.ToString(),
            CaptureStrategy = ChangeHistoryCaptureStrategy.RepositorySnapshot.ToString(),
            CaptureSource = ChangeHistoryCaptureSource.RepositorySnapshot.ToString(),
            CaptureStatus = ChangeHistoryCaptureStatus.Captured.ToString(),
            IsRestoreable = true,
            RestorePlanName = nameof(AddressRestorePlan),
            ChangedDate = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();
        var repository = new InMemoryRepository([entity]);
        var services = new ServiceCollection()
            .AddSingleton<AddressRestorePlan>()
            .BuildServiceProvider();
        var options = new ChangeHistoryOptions();
        options.Track<ChangeHistoryStubEntity>()
            .CaptureOwned(e => e.BillingAddress, path => path.UseRestorePlan<AddressRestorePlan>());
        var handler = new ChangeHistoryRestoreCommandHandler<ChangeHistoryStubEntity, ChangeHistoryStubDbContext>(
            context,
            repository,
            options,
            services);

        var result = await handler.HandleAsync(new ChangeHistoryRestoreCommand<ChangeHistoryStubEntity>(entity.Id, originalChangeSetId));

        result.IsSuccess.ShouldBeTrue();
        var restored = await repository.FindOneAsync(entity.Id);
        restored.BillingAddress.City.ShouldBe("old city");
        var restoreRow = await context.Set<ChangeHistoryEntry>().SingleAsync(e => e.Operation == ChangeHistoryOperation.Restore.ToString());
        restoreRow.RestoreExecutionMode.ShouldBe(ChangeHistoryRestoreExecutionMode.RestorePlan.ToString());
        restoreRow.RestorePlanName.ShouldBe(nameof(AddressRestorePlan));
    }

    [Fact]
    public async Task HandleAsync_WithGraphRestorePlan_RestoresGraphAndPersistsRestoreRows()
    {
        using var context = CreateContext();
        var orderId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var entity = new ChangeHistoryStubEntity
        {
            Id = Guid.NewGuid(),
            Orders =
            [
                new ChangeHistoryOrder
                {
                    Id = orderId,
                    Items = [new ChangeHistoryOrderItem { Id = itemId, Quantity = 5 }]
                }
            ]
        };
        var originalChangeSetId = Guid.NewGuid();
        context.Set<ChangeHistoryEntry>().Add(new ChangeHistoryEntry
        {
            Id = Guid.NewGuid(),
            ChangeSetId = originalChangeSetId,
            ChangeSetSequence = 0,
            EntityType = nameof(ChangeHistoryStubEntity),
            EntityClrType = typeof(ChangeHistoryStubEntity).AssemblyQualifiedName,
            EntityId = entity.Id.ToString(),
            EntityIdType = typeof(Guid).AssemblyQualifiedName,
            PropertyName = $"Orders[{orderId}].Items[{itemId}].Quantity",
            PropertyPath = $"Orders[{orderId}].Items[{itemId}].Quantity",
            PathKind = ChangeHistoryCapturePathKind.Graph.ToString(),
            CollectionItemId = itemId.ToString(),
            ValueClrType = typeof(int).AssemblyQualifiedName,
            OldValue = "1",
            NewValue = "5",
            Operation = ChangeHistoryOperation.GraphChanged.ToString(),
            CaptureStrategy = ChangeHistoryCaptureStrategy.RepositorySnapshot.ToString(),
            CaptureSource = ChangeHistoryCaptureSource.RepositorySnapshot.ToString(),
            CaptureStatus = ChangeHistoryCaptureStatus.Captured.ToString(),
            IsRestoreable = true,
            RestorePlanName = nameof(OrderItemsRestorePlan),
            ChangedDate = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var repository = new InMemoryRepository([entity]);
        var services = new ServiceCollection()
            .AddSingleton<OrderItemsRestorePlan>()
            .BuildServiceProvider();
        var options = new ChangeHistoryOptions();
        options.Track<ChangeHistoryStubEntity>()
            .CaptureGraph("Orders", graph => graph
                .UseIdentity<ChangeHistoryOrder, Guid>("Orders", order => order.Id)
                .UseIdentity<ChangeHistoryOrderItem, Guid>("Orders.Items", item => item.Id)
                .UseRestorePlan<OrderItemsRestorePlan>());
        var handler = new ChangeHistoryRestoreCommandHandler<ChangeHistoryStubEntity, ChangeHistoryStubDbContext>(
            context,
            repository,
            options,
            services);

        var result = await handler.HandleAsync(new ChangeHistoryRestoreCommand<ChangeHistoryStubEntity>(entity.Id, originalChangeSetId));

        result.IsSuccess.ShouldBeTrue();
        var restored = await repository.FindOneAsync(entity.Id);
        restored.Orders[0].Items[0].Quantity.ShouldBe(1);
        var restoreRow = await context.Set<ChangeHistoryEntry>().SingleAsync(e => e.Operation == ChangeHistoryOperation.Restore.ToString());
        restoreRow.RestoreExecutionMode.ShouldBe(ChangeHistoryRestoreExecutionMode.RestorePlan.ToString());
        restoreRow.RestorePlanName.ShouldBe(nameof(OrderItemsRestorePlan));
        restoreRow.OldValue.ShouldBe("5");
        restoreRow.NewValue.ShouldBe("1");
    }

    [Fact]
    public async Task FindAllAsync_WithFilters_ReturnsMatchingHistoryRows()
    {
        using var context = CreateContext();
        var entity = new ChangeHistoryStubEntity { Id = Guid.NewGuid(), Name = "new" };
        var changeSetId = Guid.NewGuid();
        var bulkOperationId = Guid.NewGuid();
        var matching = CreateHistoryEntry(entity, changeSetId, nameof(ChangeHistoryStubEntity.Name), "old", "new");
        matching.BulkOperationId = bulkOperationId;
        matching.ChangedByUserId = "user-1";
        matching.Operation = ChangeHistoryOperation.BulkUpdate.ToString();
        matching.CaptureSource = ChangeHistoryCaptureSource.UpdateSet.ToString();
        matching.CaptureStrategy = ChangeHistoryCaptureStrategy.RepositorySnapshot.ToString();
        matching.CaptureStatus = ChangeHistoryCaptureStatus.Failed.ToString();
        matching.CaptureMessage = "The source row could not be loaded.";
        matching.RestorePlanName = "OrderItemsRestore";
        matching.ChangedDate = DateTimeOffset.UtcNow.AddMinutes(-5);
        context.Set<ChangeHistoryEntry>().Add(matching);
        var other = CreateHistoryEntry(new ChangeHistoryStubEntity { Id = Guid.NewGuid() }, Guid.NewGuid(), nameof(ChangeHistoryStubEntity.Email), "a", "b");
        other.ChangedByUserId = "user-2";
        other.ChangedDate = DateTimeOffset.UtcNow;
        context.Set<ChangeHistoryEntry>().Add(other);
        await context.SaveChangesAsync();
        var sut = new ChangeHistoryQueryService<ChangeHistoryStubDbContext>(context);

        var result = await sut.FindAllAsync(new ChangeHistoryFindAllQuery
        {
            EntityType = nameof(ChangeHistoryStubEntity),
            EntityId = entity.Id.ToString(),
            PropertyName = nameof(ChangeHistoryStubEntity.Name),
            ChangeSetId = changeSetId,
            BulkOperationId = bulkOperationId,
            ChangedByUserId = "user-1",
            Operation = ChangeHistoryOperation.BulkUpdate.ToString(),
            CaptureSource = ChangeHistoryCaptureSource.UpdateSet.ToString(),
            CaptureStrategy = ChangeHistoryCaptureStrategy.RepositorySnapshot.ToString(),
            CaptureStatus = ChangeHistoryCaptureStatus.Failed.ToString()
        });

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(1);
        var row = result.Value.Single();
        row.ChangeSetId.ShouldBe(changeSetId);
        row.CaptureStrategy.ShouldBe(ChangeHistoryCaptureStrategy.RepositorySnapshot.ToString());
        row.CaptureStatus.ShouldBe(ChangeHistoryCaptureStatus.Failed.ToString());
        row.CaptureMessage.ShouldBe("The source row could not be loaded.");
        row.RestorePlanName.ShouldBe("OrderItemsRestore");
        row.EntityClrType.ShouldBe(typeof(ChangeHistoryStubEntity).AssemblyQualifiedName);
        row.EntityIdType.ShouldBe(typeof(Guid).AssemblyQualifiedName);
        row.ValueClrType.ShouldBe(typeof(string).AssemblyQualifiedName);
        row.ChangedDateTicks.ShouldBe(matching.ChangedDateTicks);
    }

    [Fact]
    public async Task FindAllRequestHandler_WithReadAuthorizerFailure_ReturnsFailureBeforeQuery()
    {
        using var context = CreateContext();
        var options = new ChangeHistoryOptions()
            .UseReadAuthorizationPolicy("History.Read");
        var handler = new ChangeHistoryFindAllRequestHandler<ChangeHistoryStubDbContext>(
            new ChangeHistoryQueryService<ChangeHistoryStubDbContext>(context),
            options,
            new DenyReadAuthorizer());

        var result = await ((IRequestHandler<ChangeHistoryFindAllRequest<ChangeHistoryStubDbContext>, ChangeHistoryFindAllResult>)handler)
            .HandleAsync(new ChangeHistoryFindAllRequest<ChangeHistoryStubDbContext>
            {
                EntityType = nameof(ChangeHistoryStubEntity),
                IncludeValues = true
            }, new SendOptions(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(error => error.Message.Contains("read denied", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FindAllAsync_WithPagingAndOrdering_ReturnsRequestedPage()
    {
        using var context = CreateContext();
        var entity = new ChangeHistoryStubEntity { Id = Guid.NewGuid() };
        var one = CreateHistoryEntry(entity, Guid.NewGuid(), "One", "a", "b");
        one.ChangedDate = DateTimeOffset.UtcNow.AddMinutes(-3);
        var two = CreateHistoryEntry(entity, Guid.NewGuid(), "Two", "a", "b");
        two.ChangedDate = DateTimeOffset.UtcNow.AddMinutes(-2);
        var three = CreateHistoryEntry(entity, Guid.NewGuid(), "Three", "a", "b");
        three.ChangedDate = DateTimeOffset.UtcNow.AddMinutes(-1);
        context.Set<ChangeHistoryEntry>().AddRange(one, two, three);
        await context.SaveChangesAsync();
        var sut = new ChangeHistoryQueryService<ChangeHistoryStubDbContext>(context);

        var result = await sut.FindAllAsync(new ChangeHistoryFindAllQuery
        {
            EntityId = entity.Id.ToString(),
            Page = 2,
            PageSize = 1,
            OrderAscending = true
        });

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(3);
        result.CurrentPage.ShouldBe(2);
        result.PageSize.ShouldBe(1);
        result.Value.Single().PropertyName.ShouldBe("Two");
    }

    [Fact]
    public async Task FindAllChangeSetsAsync_GroupsRowsByChangeSet()
    {
        using var context = CreateContext();
        var entity = new ChangeHistoryStubEntity { Id = Guid.NewGuid() };
        var changeSetId = Guid.NewGuid();
        context.Set<ChangeHistoryEntry>().AddRange(
            CreateHistoryEntry(entity, changeSetId, nameof(ChangeHistoryStubEntity.Name), "old", "new"),
            CreateHistoryEntry(entity, changeSetId, nameof(ChangeHistoryStubEntity.Email), "old@example.test", "new@example.test"));
        await context.SaveChangesAsync();
        var sut = new ChangeHistoryQueryService<ChangeHistoryStubDbContext>(context);

        var result = await sut.FindAllChangeSetsAsync(new ChangeHistoryFindAllQuery
        {
            EntityType = nameof(ChangeHistoryStubEntity),
            EntityId = entity.Id.ToString()
        });

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(1);
        result.Value.Single().ChangeSetId.ShouldBe(changeSetId);
        result.Value.Single().Rows.Count.ShouldBe(2);
    }

    [Fact]
    public async Task FindOneChangeSetAsync_WhenValuesExcluded_ReturnsHashesWithoutValues()
    {
        using var context = CreateContext();
        var entity = new ChangeHistoryStubEntity { Id = Guid.NewGuid() };
        var changeSetId = Guid.NewGuid();
        var entry = CreateHistoryEntry(entity, changeSetId, nameof(ChangeHistoryStubEntity.Name), "old", "new");
        entry.OldValueHash = "old-hash";
        entry.NewValueHash = "new-hash";
        context.Set<ChangeHistoryEntry>().Add(entry);
        await context.SaveChangesAsync();
        var sut = new ChangeHistoryQueryService<ChangeHistoryStubDbContext>(context);

        var result = await sut.FindOneChangeSetAsync(new ChangeHistoryFindOneChangeSetQuery
        {
            ChangeSetId = changeSetId,
            EntityType = nameof(ChangeHistoryStubEntity),
            EntityId = entity.Id.ToString(),
            IncludeValues = false
        });

        result.IsSuccess.ShouldBeTrue();
        var row = result.Value.Rows.Single();
        row.OldValue.ShouldBeNull();
        row.NewValue.ShouldBeNull();
        row.OldValueHash.ShouldNotBeNull();
        row.NewValueHash.ShouldNotBeNull();
    }

    [Fact]
    public void AddChangeHistory_WithIncompleteDomainLogicRestorePolicy_ThrowsDuringConfiguration()
    {
        var services = new ServiceCollection();

        var action = () => services.AddChangeHistory(options => options
            .Track<ChangeHistoryStubEntity>()
            .AllowRestore(e => e.Name));

        action.ShouldThrow<InvalidOperationException>()
            .Message.ShouldContain("does not define a domain method or handler");
    }

    [Fact]
    public void ChangeHistoryEntry_ConfiguresRecommendedPersistenceMappings()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(ChangeHistoryEntry));

        entityType.FindProperty(nameof(ChangeHistoryEntry.BulkOperationId)).ShouldNotBeNull();
        entityType.FindProperty(nameof(ChangeHistoryEntry.AffectedEntityCount)).ShouldNotBeNull();
        entityType.FindProperty(nameof(ChangeHistoryEntry.Properties)).ShouldNotBeNull();
        entityType.FindProperty(nameof(ChangeHistoryEntry.ActivityParentId)).ShouldNotBeNull();
        HasIndex(entityType, nameof(ChangeHistoryEntry.EntityType), nameof(ChangeHistoryEntry.EntityId), nameof(ChangeHistoryEntry.ChangedDateTicks)).ShouldBeTrue();
        HasIndex(entityType, nameof(ChangeHistoryEntry.ChangeSetId), nameof(ChangeHistoryEntry.ChangeSetSequence)).ShouldBeTrue();
        HasIndex(entityType, nameof(ChangeHistoryEntry.BulkOperationId)).ShouldBeTrue();
        HasIndex(entityType, nameof(ChangeHistoryEntry.EntityType), nameof(ChangeHistoryEntry.EntityId), nameof(ChangeHistoryEntry.PropertyName), nameof(ChangeHistoryEntry.ChangedDateTicks)).ShouldBeTrue();
        HasIndex(entityType, nameof(ChangeHistoryEntry.ChangedByUserId), nameof(ChangeHistoryEntry.ChangedDateTicks)).ShouldBeTrue();
        HasIndex(entityType, nameof(ChangeHistoryEntry.CorrelationId)).ShouldBeTrue();
        HasIndex(entityType, nameof(ChangeHistoryEntry.ModuleName), nameof(ChangeHistoryEntry.ChangedDateTicks)).ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_WithCollectionRowsWithoutPlan_ReplaysInverseMembership()
    {
        using var context = CreateContext();
        var keptId = Guid.NewGuid();
        var addedId = Guid.NewGuid();
        var removedId = Guid.NewGuid();
        var entity = new ChangeHistoryStubEntity
        {
            Id = Guid.NewGuid(),
            Tags =
            [
                new ChangeHistoryTag { Id = keptId, Value = "new" },
                new ChangeHistoryTag { Id = addedId, Value = "added" }
            ]
        };
        var originalChangeSetId = Guid.NewGuid();
        context.Set<ChangeHistoryEntry>().AddRange(
            CreateCollectionHistoryEntry(entity, originalChangeSetId, keptId, "old", "new"),
            CreateCollectionHistoryEntry(entity, originalChangeSetId, addedId, null, "added", "Added"),
            CreateCollectionHistoryEntry(entity, originalChangeSetId, removedId, "removed", null, "Removed"));
        await context.SaveChangesAsync();
        var repository = new InMemoryRepository([entity]);
        var options = new ChangeHistoryOptions();
        options.Track<ChangeHistoryStubEntity>()
            .CaptureCollection(e => e.Tags, tag => tag.Id);
        var handler = new ChangeHistoryRestoreCommandHandler<ChangeHistoryStubEntity, ChangeHistoryStubDbContext>(
            context,
            repository,
            options);

        var result = await handler.HandleAsync(new ChangeHistoryRestoreCommand<ChangeHistoryStubEntity>(entity.Id, originalChangeSetId));

        result.IsSuccess.ShouldBeTrue();
        var restored = await repository.FindOneAsync(entity.Id);
        restored.Tags.ShouldContain(tag => tag.Id == keptId && tag.Value == "old");
        restored.Tags.ShouldNotContain(tag => tag.Id == addedId);
        restored.Tags.ShouldContain(tag => tag.Id == removedId && tag.Value == "removed");
    }

    [Fact]
    public async Task HandleAsync_WhenRestorePlanFails_RollsBackPlanMutation()
    {
        using var context = CreateContext();
        var entity = new ChangeHistoryStubEntity
        {
            Id = Guid.NewGuid(),
            BillingAddress = new ChangeHistoryAddress { City = "new city" }
        };
        context.Entities.Add(entity);
        var originalChangeSetId = Guid.NewGuid();
        context.Set<ChangeHistoryEntry>().Add(new ChangeHistoryEntry
        {
            Id = Guid.NewGuid(),
            ChangeSetId = originalChangeSetId,
            ChangeSetSequence = 0,
            EntityType = nameof(ChangeHistoryStubEntity),
            EntityClrType = typeof(ChangeHistoryStubEntity).AssemblyQualifiedName,
            EntityId = entity.Id.ToString(),
            EntityIdType = typeof(Guid).AssemblyQualifiedName,
            PropertyName = "BillingAddress.City",
            PropertyPath = "BillingAddress.City",
            PathKind = ChangeHistoryCapturePathKind.Owned.ToString(),
            ValueClrType = typeof(string).AssemblyQualifiedName,
            OldValue = "\"old city\"",
            NewValue = "\"new city\"",
            Operation = ChangeHistoryOperation.Update.ToString(),
            CaptureStrategy = ChangeHistoryCaptureStrategy.RepositorySnapshot.ToString(),
            CaptureSource = ChangeHistoryCaptureSource.RepositorySnapshot.ToString(),
            CaptureStatus = ChangeHistoryCaptureStatus.Captured.ToString(),
            IsRestoreable = true,
            RestorePlanName = nameof(FailingAddressRestorePlan),
            ChangedDate = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();
        var services = new ServiceCollection()
            .AddSingleton<FailingAddressRestorePlan>()
            .BuildServiceProvider();
        var options = new ChangeHistoryOptions();
        options.Track<ChangeHistoryStubEntity>()
            .CaptureOwned(e => e.BillingAddress, path => path.UseRestorePlan<FailingAddressRestorePlan>());
        var handler = CreateRestoreHandler(context, options, services);

        var result = await handler.HandleAsync(new ChangeHistoryRestoreCommand<ChangeHistoryStubEntity>(entity.Id, originalChangeSetId));

        result.IsFailure.ShouldBeTrue();
        entity.BillingAddress.City.ShouldBe("new city");
        (await context.Set<ChangeHistoryEntry>().CountAsync(e => e.Operation == ChangeHistoryOperation.Restore.ToString())).ShouldBe(0);
    }

    [Fact]
    public async Task HandleAsync_WithActivityMetadata_CapturesModuleAndActivityParentId()
    {
        using var context = CreateContext();
        var entity = new ChangeHistoryStubEntity { Id = Guid.NewGuid(), Name = "new" };
        var originalChangeSetId = Guid.NewGuid();
        context.Entities.Add(entity);
        context.Set<ChangeHistoryEntry>().Add(CreateHistoryEntry(entity, originalChangeSetId, nameof(ChangeHistoryStubEntity.Name), "old", "new"));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var options = new ChangeHistoryOptions();
        options.Track<ChangeHistoryStubEntity>()
            .AllowRestore(e => e.Name)
            .UseValidatedSetter();
        var handler = CreateRestoreHandler(context, options);
        using var activity = new Activity("change-history-test");
        activity.SetBaggage(ModuleConstants.ModuleNameKey, "Sales");
        activity.Start();

        var result = await handler.HandleAsync(new ChangeHistoryRestoreCommand<ChangeHistoryStubEntity>(entity.Id, originalChangeSetId));

        result.IsSuccess.ShouldBeTrue();
        var restoreRow = await context.Set<ChangeHistoryEntry>().SingleAsync(e => e.Operation == ChangeHistoryOperation.Restore.ToString());
        restoreRow.ModuleName.ShouldBe("Sales");
        restoreRow.ActivityParentId.ShouldBe(activity.Id);
        restoreRow.Properties.ShouldContain(ModuleConstants.ActivityParentIdKey);
        restoreRow.Properties.ShouldContain(originalChangeSetId.ToString());
    }

    [Fact]
    public async Task UpdateAsync_WithGraphPathAndEfPrimaryKeyMetadata_CapturesWithoutExplicitIdentityRule()
    {
        using var context = CreateGraphIdentityContext();
        var orderId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var baseline = new ChangeHistoryStubEntity
        {
            Id = Guid.NewGuid(),
            Orders =
            [
                new ChangeHistoryOrder
                {
                    Id = orderId,
                    Items = [new ChangeHistoryOrderItem { Id = itemId, Quantity = 1, Sku = "ABC" }]
                }
            ]
        };
        var entity = CloneEntity(baseline);
        entity.Orders[0].Items[0].Quantity = 5;
        var inner = new InMemoryRepository([baseline]);
        var options = new ChangeHistoryOptions();
        options.Track<ChangeHistoryStubEntity>()
            .CaptureDirectMutations(ChangeHistoryCaptureStrategy.RepositorySnapshot, ChangeHistoryCaptureMode.Required)
            .CaptureGraph("Orders");
        var sut = new RepositoryChangeHistoryBehavior<ChangeHistoryStubEntity, ChangeHistoryGraphIdentityStubDbContext>(
            NullLoggerFactory.Instance,
            context,
            inner,
            options);

        await sut.UpdateAsync(entity);
        await context.SaveChangesAsync();

        var row = await context.Set<ChangeHistoryEntry>().SingleAsync(e => e.PropertyName.EndsWith("Quantity", StringComparison.Ordinal));
        row.PropertyPath.ShouldBe($"Orders[{orderId}].Items[{itemId}].Quantity");
        row.CollectionItemId.ShouldBe(itemId.ToString());
    }

    private static ChangeHistoryStubDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ChangeHistoryStubDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ChangeHistoryStubDbContext(options);
    }

    private static bool HasIndex(IEntityType entityType, params string[] propertyNames)
        => entityType.GetIndexes().Any(index => index.Properties.Select(property => property.Name).SequenceEqual(propertyNames));

    private static RepositoryChangeHistoryBehavior<ChangeHistoryStubEntity, ChangeHistoryStubDbContext> CreateRepository(
        ChangeHistoryStubDbContext context,
        ChangeHistoryOptions options = null)
    {
        var inner = new EntityFrameworkRepositoryWrapper<ChangeHistoryStubEntity, ChangeHistoryStubDbContext>(
            NullLoggerFactory.Instance,
            context);

        return new RepositoryChangeHistoryBehavior<ChangeHistoryStubEntity, ChangeHistoryStubDbContext>(
            NullLoggerFactory.Instance,
            context,
            inner,
            options);
    }

    private static ChangeHistoryRestoreCommandHandler<ChangeHistoryStubEntity, ChangeHistoryStubDbContext> CreateRestoreHandler(
        ChangeHistoryStubDbContext context,
        ChangeHistoryOptions options,
        IServiceProvider serviceProvider = null,
        IGenericRepository<ChangeHistoryStubEntity> repository = null)
    {
        repository ??= new EntityFrameworkRepositoryWrapper<ChangeHistoryStubEntity, ChangeHistoryStubDbContext>(
            NullLoggerFactory.Instance,
            context);

        return new ChangeHistoryRestoreCommandHandler<ChangeHistoryStubEntity, ChangeHistoryStubDbContext>(
            context,
            repository,
            options,
            serviceProvider);
    }

    private static ChangeHistoryEntry CreateHistoryEntry(
        ChangeHistoryStubEntity entity,
        Guid changeSetId,
        string propertyName,
        string oldValue,
        string newValue)
        => new()
        {
            Id = Guid.NewGuid(),
            ChangeSetId = changeSetId,
            ChangeSetSequence = 0,
            EntityType = nameof(ChangeHistoryStubEntity),
            EntityClrType = typeof(ChangeHistoryStubEntity).AssemblyQualifiedName,
            EntityId = entity.Id.ToString(),
            EntityIdType = typeof(Guid).AssemblyQualifiedName,
            PropertyName = propertyName,
            PathKind = "Scalar",
            ValueClrType = typeof(string).AssemblyQualifiedName,
            OldValue = $"\"{oldValue}\"",
            NewValue = $"\"{newValue}\"",
            Operation = ChangeHistoryOperation.Update.ToString(),
            CaptureStrategy = ChangeHistoryCaptureStrategy.EntityChangeOnly.ToString(),
            CaptureSource = ChangeHistoryCaptureSource.EntityChange.ToString(),
            CaptureStatus = ChangeHistoryCaptureStatus.Captured.ToString(),
            IsRestoreable = true,
            ChangedDate = DateTimeOffset.UtcNow
        };

    private static ChangeHistoryEntry CreateCollectionHistoryEntry(
        ChangeHistoryStubEntity entity,
        Guid changeSetId,
        Guid itemId,
        string oldValue,
        string newValue,
        string action = null)
        => new()
        {
            Id = Guid.NewGuid(),
            ChangeSetId = changeSetId,
            ChangeSetSequence = 0,
            EntityType = nameof(ChangeHistoryStubEntity),
            EntityClrType = typeof(ChangeHistoryStubEntity).AssemblyQualifiedName,
            EntityId = entity.Id.ToString(),
            EntityIdType = typeof(Guid).AssemblyQualifiedName,
            PropertyName = $"Tags[{itemId}].Value",
            PropertyPath = $"Tags[{itemId}].Value",
            PathKind = ChangeHistoryCapturePathKind.Collection.ToString(),
            CollectionAction = action,
            CollectionItemId = itemId.ToString(),
            ValueClrType = typeof(string).AssemblyQualifiedName,
            OldValue = oldValue is null ? null : $"\"{oldValue}\"",
            NewValue = newValue is null ? null : $"\"{newValue}\"",
            Operation = ChangeHistoryOperation.CollectionChanged.ToString(),
            CaptureStrategy = ChangeHistoryCaptureStrategy.RepositorySnapshot.ToString(),
            CaptureSource = ChangeHistoryCaptureSource.RepositorySnapshot.ToString(),
            CaptureStatus = ChangeHistoryCaptureStatus.Captured.ToString(),
            IsRestoreable = true,
            ChangedDate = DateTimeOffset.UtcNow
        };

    private sealed class ChangeHistoryStubDbContext(DbContextOptions<ChangeHistoryStubDbContext> options) : DbContext(options)
    {
        public DbSet<ChangeHistoryStubEntity> Entities { get; set; }

        public DbSet<ChangeHistoryEntry> ChangeHistory { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ChangeHistoryStubEntity>().HasKey(e => e.Id);
            modelBuilder.Entity<ChangeHistoryStubEntity>().Ignore(e => e.BillingAddress);
            modelBuilder.Entity<ChangeHistoryStubEntity>().Ignore(e => e.Tags);
            modelBuilder.Entity<ChangeHistoryStubEntity>().Ignore(e => e.Orders);
            modelBuilder.Entity<ChangeHistoryStubEntity>().Ignore(e => e.Status);
        }
    }

    private static ChangeHistoryGraphIdentityStubDbContext CreateGraphIdentityContext()
    {
        var options = new DbContextOptionsBuilder<ChangeHistoryGraphIdentityStubDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ChangeHistoryGraphIdentityStubDbContext(options);
    }

    private static ChangeHistoryTrackedPathStubDbContext CreateTrackedPathContext()
    {
        var options = new DbContextOptionsBuilder<ChangeHistoryTrackedPathStubDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ChangeHistoryTrackedPathStubDbContext(options);
    }

    private sealed class ChangeHistoryGraphIdentityStubDbContext(DbContextOptions<ChangeHistoryGraphIdentityStubDbContext> options) : DbContext(options)
    {
        public DbSet<ChangeHistoryEntry> ChangeHistory { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ChangeHistoryStubEntity>().HasKey(e => e.Id);
            modelBuilder.Entity<ChangeHistoryStubEntity>().Ignore(e => e.BillingAddress);
            modelBuilder.Entity<ChangeHistoryStubEntity>().Ignore(e => e.Tags);
            modelBuilder.Entity<ChangeHistoryStubEntity>().Ignore(e => e.Orders);
            modelBuilder.Entity<ChangeHistoryStubEntity>().Ignore(e => e.Status);
            modelBuilder.Entity<ChangeHistoryTag>().HasKey(e => e.Id);
            modelBuilder.Entity<ChangeHistoryOrder>().HasKey(e => e.Id);
            modelBuilder.Entity<ChangeHistoryOrderItem>().HasKey(e => e.Id);
        }
    }

    private sealed class ChangeHistoryTrackedPathStubDbContext(DbContextOptions<ChangeHistoryTrackedPathStubDbContext> options) : DbContext(options)
    {
        public DbSet<ChangeHistoryEntry> ChangeHistory { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ChangeHistoryStubEntity>(builder =>
            {
                builder.HasKey(e => e.Id);
                builder.OwnsOne(e => e.BillingAddress);
                builder.OwnsMany(e => e.Tags, tags =>
                {
                    tags.WithOwner().HasForeignKey("ChangeHistoryStubEntityId");
                    tags.HasKey(e => e.Id);
                });
                builder.Ignore(e => e.Orders);
                builder.Ignore(e => e.Status);
            });
        }
    }

    private sealed class ChangeHistoryStubEntity : Entity<Guid>
    {
        public string Name { get; set; }

        public string Email { get; set; }

        public string ApiToken { get; set; }

        public ChangeHistoryStubStatus Status { get; set; } = ChangeHistoryStubStatus.Active;

        public ChangeHistoryAddress BillingAddress { get; set; }

        public List<ChangeHistoryTag> Tags { get; set; } = [];

        public List<ChangeHistoryOrder> Orders { get; set; } = [];

        public Result RestoreName(string value)
        {
            if (value == "blocked")
            {
                return Result.Failure(new ValidationError("Name restore is blocked."));
            }

            this.Name = value;

            return Result.Success();
        }
    }

    private sealed class ChangeHistoryAddress
    {
        public string Street { get; set; }

        public string City { get; set; }
    }

    private sealed class ChangeHistoryTag
    {
        public Guid Id { get; set; }

        public string Value { get; set; }
    }

    private sealed class ChangeHistoryOrder
    {
        public Guid Id { get; set; }

        public string Number { get; set; }

        public List<ChangeHistoryOrderItem> Items { get; set; } = [];
    }

    private sealed class ChangeHistoryOrderItem
    {
        public Guid Id { get; set; }

        public string Sku { get; set; }

        public int Quantity { get; set; }
    }

    private sealed class ChangeHistoryStubStatus : Enumeration
    {
        public static readonly ChangeHistoryStubStatus Active = new(1, "Active");

        private ChangeHistoryStubStatus(int id, string value)
            : base(id, value)
        {
        }
    }

    private sealed class NameRestoreHandler : IChangeHistoryRestoreHandler<ChangeHistoryStubEntity>
    {
        public Task<Result> RestoreAsync(
            ChangeHistoryStubEntity entity,
            ChangeHistoryRestoreContext context,
            CancellationToken cancellationToken = default)
        {
            entity.Name = (string)context.Value;

            return Task.FromResult(Result.Success());
        }
    }

    private sealed class OrderItemsRestorePlan : IChangeHistoryGraphRestorePlan<ChangeHistoryStubEntity>
    {
        public Task<Result> RestoreAsync(
            ChangeHistoryStubEntity entity,
            IReadOnlyList<ChangeHistoryGraphRestoreValue> values,
            CancellationToken cancellationToken = default)
        {
            var quantity = values.Single(v => v.PropertyPath.EndsWith("Quantity", StringComparison.Ordinal));
            entity.Orders[0].Items[0].Quantity = (int)quantity.Value;

            return Task.FromResult(Result.Success());
        }
    }

    private sealed class AddressRestorePlan : IChangeHistoryGraphRestorePlan<ChangeHistoryStubEntity>
    {
        public Task<Result> RestoreAsync(
            ChangeHistoryStubEntity entity,
            IReadOnlyList<ChangeHistoryGraphRestoreValue> values,
            CancellationToken cancellationToken = default)
        {
            entity.BillingAddress ??= new ChangeHistoryAddress();
            entity.BillingAddress.City = (string)values.Single(v => v.PropertyPath == "BillingAddress.City").Value;

            return Task.FromResult(Result.Success());
        }
    }

    private sealed class FailingAddressRestorePlan : IChangeHistoryGraphRestorePlan<ChangeHistoryStubEntity>
    {
        public Task<Result> RestoreAsync(
            ChangeHistoryStubEntity entity,
            IReadOnlyList<ChangeHistoryGraphRestoreValue> values,
            CancellationToken cancellationToken = default)
        {
            entity.BillingAddress.City = "mutated before failure";

            return Task.FromResult(Result.Failure(new ValidationError("Restore plan failed.")));
        }
    }

    private sealed class DenyRestoreAuthorizer : IChangeHistoryRestoreAuthorizer<ChangeHistoryStubEntity>
    {
        public Task<Result> AuthorizeAsync(
            ChangeHistoryStubEntity entity,
            ChangeHistoryRestoreAuthorizationContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Failure(new ValidationError("Restore denied.")));
    }

    private sealed class DenyReadAuthorizer : IChangeHistoryReadAuthorizer<ChangeHistoryStubDbContext>
    {
        public Task<Result> AuthorizeAsync(
            ChangeHistoryReadAuthorizationContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Failure(new ValidationError($"ChangeHistory read denied for policy {context.Policy}.")));
    }

    private sealed class InMemoryRepository(IReadOnlyCollection<ChangeHistoryStubEntity> entities) : IGenericRepository<ChangeHistoryStubEntity>
    {
        private readonly List<ChangeHistoryStubEntity> entities = entities.Select(Clone).ToList();

        public Task<ChangeHistoryStubEntity> InsertAsync(ChangeHistoryStubEntity entity, CancellationToken cancellationToken = default)
        {
            this.entities.Add(Clone(entity));

            return Task.FromResult(entity);
        }

        public Task<IEnumerable<ChangeHistoryStubEntity>> InsertSetAsync(
            IEnumerable<ChangeHistoryStubEntity> entities,
            CancellationToken cancellationToken = default)
        {
            var items = entities.SafeNull().Where(e => e is not null).ToList();
            this.entities.AddRange(items.Select(Clone));

            return Task.FromResult<IEnumerable<ChangeHistoryStubEntity>>(items);
        }

        public Task<ChangeHistoryStubEntity> UpdateAsync(ChangeHistoryStubEntity entity, CancellationToken cancellationToken = default)
        {
            var existing = this.entities.Find(e => e.Id == entity.Id);
            if (existing is not null)
            {
                existing.Name = entity.Name;
                existing.Email = entity.Email;
                existing.ApiToken = entity.ApiToken;
                existing.BillingAddress = CloneAddress(entity.BillingAddress);
                existing.Tags = entity.Tags.Select(CloneTag).ToList();
                existing.Orders = entity.Orders.Select(CloneOrder).ToList();
            }

            return Task.FromResult(entity);
        }

        public async Task<(ChangeHistoryStubEntity entity, RepositoryActionResult action)> UpsertAsync(ChangeHistoryStubEntity entity, CancellationToken cancellationToken = default)
        {
            var existing = this.entities.Any(e => e.Id == entity.Id);
            await this.UpdateAsync(entity, cancellationToken);

            return (entity, existing ? RepositoryActionResult.Updated : RepositoryActionResult.Inserted);
        }

        public Task<long> UpdateSetAsync(Action<IEntityUpdateSet<ChangeHistoryStubEntity>> set, IFindOptions<ChangeHistoryStubEntity> options = null, CancellationToken cancellationToken = default)
            => this.UpdateSetAsync([], set, options, cancellationToken);

        public Task<long> UpdateSetAsync(ISpecification<ChangeHistoryStubEntity> specification, Action<IEntityUpdateSet<ChangeHistoryStubEntity>> set, IFindOptions<ChangeHistoryStubEntity> options = null, CancellationToken cancellationToken = default)
            => this.UpdateSetAsync([specification], set, options, cancellationToken);

        public Task<long> UpdateSetAsync(IEnumerable<ISpecification<ChangeHistoryStubEntity>> specifications, Action<IEntityUpdateSet<ChangeHistoryStubEntity>> set, IFindOptions<ChangeHistoryStubEntity> options = null, CancellationToken cancellationToken = default)
        {
            var updateSet = new EntityUpdateSet<ChangeHistoryStubEntity>();
            set(updateSet);
            var matched = this.ApplySpecifications(specifications).ToArray();
            foreach (var entity in matched)
            {
                updateSet.Apply(entity);
            }

            return Task.FromResult((long)matched.Length);
        }

        public Task<IEnumerable<ChangeHistoryStubEntity>> FindAllAsync(IFindOptions<ChangeHistoryStubEntity> options = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<ChangeHistoryStubEntity>>(this.entities.Select(Clone).ToArray());

        public Task<IEnumerable<ChangeHistoryStubEntity>> FindAllAsync(ISpecification<ChangeHistoryStubEntity> specification, IFindOptions<ChangeHistoryStubEntity> options = null, CancellationToken cancellationToken = default)
            => this.FindAllAsync([specification], options, cancellationToken);

        public Task<IEnumerable<ChangeHistoryStubEntity>> FindAllAsync(IEnumerable<ISpecification<ChangeHistoryStubEntity>> specifications, IFindOptions<ChangeHistoryStubEntity> options = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<ChangeHistoryStubEntity>>(this.ApplySpecifications(specifications).Select(Clone).ToArray());

        public Task<ChangeHistoryStubEntity> FindOneAsync(object id, IFindOptions<ChangeHistoryStubEntity> options = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Clone(this.entities.Find(e => e.Id == (Guid)id)));

        public Task<ChangeHistoryStubEntity> FindOneAsync(ISpecification<ChangeHistoryStubEntity> specification, IFindOptions<ChangeHistoryStubEntity> options = null, CancellationToken cancellationToken = default)
            => this.FindOneAsync([specification], options, cancellationToken);

        public Task<ChangeHistoryStubEntity> FindOneAsync(IEnumerable<ISpecification<ChangeHistoryStubEntity>> specifications, IFindOptions<ChangeHistoryStubEntity> options = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Clone(this.ApplySpecifications(specifications).FirstOrDefault()));

        public Task<bool> ExistsAsync(object id, CancellationToken cancellationToken = default)
            => Task.FromResult(this.entities.Any(e => e.Id == (Guid)id));

        public Task<long> CountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult((long)this.entities.Count);

        public Task<long> CountAsync(ISpecification<ChangeHistoryStubEntity> specification, CancellationToken cancellationToken = default)
            => this.CountAsync([specification], cancellationToken);

        public Task<long> CountAsync(IEnumerable<ISpecification<ChangeHistoryStubEntity>> specifications, CancellationToken cancellationToken = default)
            => Task.FromResult((long)this.ApplySpecifications(specifications).Count());

        public Task<RepositoryActionResult> DeleteAsync(object id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<RepositoryActionResult> DeleteAsync(ChangeHistoryStubEntity entity, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<long> DeleteSetAsync(IFindOptions<ChangeHistoryStubEntity> options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<long> DeleteSetAsync(ISpecification<ChangeHistoryStubEntity> specification, IFindOptions<ChangeHistoryStubEntity> options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<long> DeleteSetAsync(IEnumerable<ISpecification<ChangeHistoryStubEntity>> specifications, IFindOptions<ChangeHistoryStubEntity> options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(Expression<Func<ChangeHistoryStubEntity, TProjection>> projection, IFindOptions<ChangeHistoryStubEntity> options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(ISpecification<ChangeHistoryStubEntity> specification, Expression<Func<ChangeHistoryStubEntity, TProjection>> projection, IFindOptions<ChangeHistoryStubEntity> options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(IEnumerable<ISpecification<ChangeHistoryStubEntity>> specifications, Expression<Func<ChangeHistoryStubEntity, TProjection>> projection, IFindOptions<ChangeHistoryStubEntity> options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        private IEnumerable<ChangeHistoryStubEntity> ApplySpecifications(IEnumerable<ISpecification<ChangeHistoryStubEntity>> specifications)
        {
            var query = this.entities.AsEnumerable();
            foreach (var specification in specifications.SafeNull())
            {
                query = query.Where(specification.ToExpression().Compile());
            }

            return query;
        }

        private static ChangeHistoryStubEntity Clone(ChangeHistoryStubEntity entity)
            => CloneEntity(entity);
    }

    private static ChangeHistoryStubEntity CloneEntity(ChangeHistoryStubEntity entity)
        => entity is null ? null : new ChangeHistoryStubEntity
        {
            Id = entity.Id,
            Name = entity.Name,
            Email = entity.Email,
            ApiToken = entity.ApiToken,
            BillingAddress = CloneAddress(entity.BillingAddress),
            Tags = entity.Tags.Select(CloneTag).ToList(),
            Orders = entity.Orders.Select(CloneOrder).ToList()
        };

    private static ChangeHistoryAddress CloneAddress(ChangeHistoryAddress address)
        => address is null ? null : new ChangeHistoryAddress { Street = address.Street, City = address.City };

    private static ChangeHistoryTag CloneTag(ChangeHistoryTag tag)
        => tag is null ? null : new ChangeHistoryTag { Id = tag.Id, Value = tag.Value };

    private static ChangeHistoryOrder CloneOrder(ChangeHistoryOrder order)
        => order is null ? null : new ChangeHistoryOrder
        {
            Id = order.Id,
            Number = order.Number,
            Items = order.Items.Select(CloneOrderItem).ToList()
        };

    private static ChangeHistoryOrderItem CloneOrderItem(ChangeHistoryOrderItem item)
        => item is null ? null : new ChangeHistoryOrderItem { Id = item.Id, Sku = item.Sku, Quantity = item.Quantity };
}
